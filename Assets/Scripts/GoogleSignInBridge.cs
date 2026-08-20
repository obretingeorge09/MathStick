using UnityEngine;
using System;

/// <summary>
/// Pure C# bridge for Google Sign-In on Android.
/// Uses AndroidJavaObject to call play-services-auth APIs directly.
/// Uses the Task-based silentSignIn + signIn intent approach with OnSuccessListener/OnFailureListener.
/// </summary>
public class GoogleSignInBridge : MonoBehaviour
{
    public static GoogleSignInBridge Instance { get; private set; }

    public event Action<string> OnIdTokenReceived;
    public event Action<string> OnSignInFailed;

    string webClientId;

#if UNITY_ANDROID && !UNITY_EDITOR
    AndroidJavaObject signInClient;
#endif

    void Awake()
    {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else { Destroy(gameObject); return; }

        webClientId = GetWebClientId();
        if (string.IsNullOrEmpty(webClientId))
            Debug.LogWarning("Google Sign-In: web_client_id not found. Enable Google Sign-In in Firebase Console and re-download google-services.json");
    }

    public void Initialize()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (string.IsNullOrEmpty(webClientId))
        {
            Debug.LogWarning("Google Sign-In: Cannot init — no web_client_id");
            return;
        }

        try
        {
            using (var gsoBuilderClass = new AndroidJavaClass("com.google.android.gms.auth.api.signin.GoogleSignInOptions$Builder"))
            using (var gsoClass = new AndroidJavaClass("com.google.android.gms.auth.api.signin.GoogleSignInOptions"))
            {
                var defaultSignIn = gsoClass.GetStatic<AndroidJavaObject>("DEFAULT_SIGN_IN");
                var builder = new AndroidJavaObject("com.google.android.gms.auth.api.signin.GoogleSignInOptions$Builder", defaultSignIn);
                builder.Call<AndroidJavaObject>("requestIdToken", webClientId);
                builder.Call<AndroidJavaObject>("requestEmail");
                var gso = builder.Call<AndroidJavaObject>("build");

                using (var googleSignInClass = new AndroidJavaClass("com.google.android.gms.auth.api.signin.GoogleSignIn"))
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                {
                    var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                    signInClient = googleSignInClass.CallStatic<AndroidJavaObject>("getClient", activity, gso);
                }

                Debug.Log("Google Sign-In initialized");
            }
        }
        catch (Exception e)
        {
            Debug.LogError("Google Sign-In init error: " + e.Message);
        }
#endif
    }

    public void SignIn()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (string.IsNullOrEmpty(webClientId))
        {
            OnSignInFailed?.Invoke("Google Sign-In not configured.\nEnable it in Firebase Console.");
            return;
        }

        if (signInClient == null)
        {
            Initialize();
            if (signInClient == null)
            {
                OnSignInFailed?.Invoke("Google Sign-In failed to initialize.");
                return;
            }
        }

        try
        {
            // Sign out first to force account picker
            var signOutTask = signInClient.Call<AndroidJavaObject>("signOut");
            signOutTask.Call<AndroidJavaObject>("addOnCompleteListener", new TaskCompleteListener(_ => {
                // Now start sign-in via startActivityForResult
                StartSignInActivity();
            }));
        }
        catch (Exception e)
        {
            Debug.LogError("Google Sign-In error: " + e.Message);
            OnSignInFailed?.Invoke("Google Sign-In failed: " + e.Message);
        }
#elif UNITY_EDITOR
        OnSignInFailed?.Invoke("Google Sign-In only works on Android device.");
#else
        OnSignInFailed?.Invoke("Google Sign-In not supported on this platform.");
#endif
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    void StartSignInActivity()
    {
        try
        {
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            {
                var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                var signInIntent = signInClient.Call<AndroidJavaObject>("getSignInIntent");
                const int RC_SIGN_IN = 9001;
                activity.Call("startActivityForResult", signInIntent, RC_SIGN_IN);
                // Result will come back via onActivityResult
                // We use a polling approach via Update to check for signed-in account
                pendingSignIn = true;
                pendingTimeout = 0;
                checkDelay = 0;
            }
        }
        catch (Exception e)
        {
            Debug.LogError("Google Sign-In activity error: " + e.Message);
            OnSignInFailed?.Invoke("Google Sign-In failed: " + e.Message);
        }
    }

    bool pendingSignIn;
    float checkDelay;
    float pendingTimeout;

    void Update()
    {
        if (!pendingSignIn) return;

        pendingTimeout += Time.unscaledDeltaTime;
        if (pendingTimeout > 60f)
        {
            CancelPendingSignIn();
            return;
        }

        // Small delay to let the activity return
        checkDelay += Time.unscaledDeltaTime;
        if (checkDelay < 0.5f) return;
        checkDelay = 0;

        // Check if the sign-in activity has completed by checking getLastSignedInAccount
        try
        {
            using (var googleSignInClass = new AndroidJavaClass("com.google.android.gms.auth.api.signin.GoogleSignIn"))
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            {
                var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                var account = googleSignInClass.CallStatic<AndroidJavaObject>("getLastSignedInAccount", activity);

                if (account != null)
                {
                    string idToken = account.Call<string>("getIdToken");
                    if (!string.IsNullOrEmpty(idToken))
                    {
                        pendingSignIn = false;
                        checkDelay = 0;
                        Debug.Log("Google Sign-In success, token length: " + idToken.Length);
                        OnIdTokenReceived?.Invoke(idToken);
                        return;
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError("Google Sign-In check error: " + e.Message);
        }
    }

    // Called when app resumes (after sign-in activity finishes)
    void OnApplicationPause(bool paused)
    {
        if (!paused && pendingSignIn)
        {
            // App resumed from sign-in activity — check result
            checkDelay = 0.4f; // Will check on next Update
        }
    }

    /// <summary>
    /// If the sign-in was cancelled, this gets called after a timeout.
    /// </summary>
    public void CancelPendingSignIn()
    {
        if (pendingSignIn)
        {
            pendingSignIn = false;
            checkDelay = 0;
            OnSignInFailed?.Invoke("Google Sign-In cancelled.");
        }
    }

    /// <summary>
    /// AndroidJavaProxy for com.google.android.gms.tasks.OnCompleteListener
    /// </summary>
    class TaskCompleteListener : AndroidJavaProxy
    {
        Action<AndroidJavaObject> callback;
        public TaskCompleteListener(Action<AndroidJavaObject> callback)
            : base("com.google.android.gms.tasks.OnCompleteListener")
        {
            this.callback = callback;
        }
        void onComplete(AndroidJavaObject task)
        {
            callback?.Invoke(task);
        }
    }
#endif

    string GetWebClientId()
    {
        try
        {
            var jsonAsset = Resources.Load<TextAsset>("google-services");
            if (jsonAsset == null)
            {
                string path = System.IO.Path.Combine(Application.streamingAssetsPath, "google-services.json");
#if UNITY_ANDROID && !UNITY_EDITOR
                using (var www = UnityEngine.Networking.UnityWebRequest.Get(path))
                {
                    www.SendWebRequest();
                    while (!www.isDone) { }
                    if (www.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
                        return ExtractWebClientId(www.downloadHandler.text);
                }
#else
                if (System.IO.File.Exists(path))
                    return ExtractWebClientId(System.IO.File.ReadAllText(path));
#endif
                return null;
            }
            return ExtractWebClientId(jsonAsset.text);
        }
        catch (Exception e)
        {
            Debug.LogError("Error reading google-services.json: " + e.Message);
            return null;
        }
    }

    string ExtractWebClientId(string json)
    {
        int idx = json.IndexOf("\"client_type\": 3");
        if (idx < 0) idx = json.IndexOf("\"client_type\":3");
        if (idx < 0) return null;

        int searchStart = json.LastIndexOf("\"client_id\"", idx);
        if (searchStart < 0) return null;

        int colonIdx = json.IndexOf(":", searchStart);
        int quoteStart = json.IndexOf("\"", colonIdx + 1);
        int quoteEnd = json.IndexOf("\"", quoteStart + 1);

        if (quoteStart >= 0 && quoteEnd > quoteStart)
            return json.Substring(quoteStart + 1, quoteEnd - quoteStart - 1);

        return null;
    }
}
