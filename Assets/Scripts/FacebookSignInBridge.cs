using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// Pure C# bridge for Facebook Login on Android.
/// Uses AndroidJavaObject to call Facebook SDK APIs.
/// Requires facebook-login dependency in gradle and FacebookAppId in AndroidManifest.
/// </summary>
public class FacebookSignInBridge : MonoBehaviour
{
    public static FacebookSignInBridge Instance { get; private set; }

    public event Action<string> OnAccessTokenReceived; // Facebook access token
    public event Action<string> OnLoginFailed;

    [Tooltip("Facebook App ID from developers.facebook.com")]
    public string facebookAppId = "";

#if UNITY_ANDROID && !UNITY_EDITOR
    AndroidJavaObject callbackManager;
    AndroidJavaObject loginManager;
    bool initialized;
    bool pendingLogin;
#endif

    void Awake()
    {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else { Destroy(gameObject); return; }
    }

    /// <summary>
    /// Initialize Facebook SDK. Call after scene is ready.
    /// </summary>
    public void Initialize()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (string.IsNullOrEmpty(facebookAppId))
        {
            Debug.LogWarning("Facebook Login: No App ID configured");
            return;
        }

        try
        {
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            {
                var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");

                // Initialize Facebook SDK
                using (var fbSdk = new AndroidJavaClass("com.facebook.FacebookSdk"))
                {
                    fbSdk.CallStatic("setApplicationId", facebookAppId);
                    fbSdk.CallStatic("sdkInitialize", activity);
                }

                // Create CallbackManager
                using (var cbmFactory = new AndroidJavaClass("com.facebook.CallbackManager$Factory"))
                {
                    callbackManager = cbmFactory.CallStatic<AndroidJavaObject>("create");
                }

                // Get LoginManager instance
                using (var lmClass = new AndroidJavaClass("com.facebook.login.LoginManager"))
                {
                    loginManager = lmClass.CallStatic<AndroidJavaObject>("getInstance");
                }

                // Register callback
                loginManager.Call("registerCallback", callbackManager, new FacebookCallback(this));

                initialized = true;
                Debug.Log("Facebook Login initialized with App ID: " + facebookAppId);
            }
        }
        catch (Exception e)
        {
            Debug.LogError("Facebook Login init error: " + e.Message);
        }
#endif
    }

    /// <summary>
    /// Start Facebook Login flow.
    /// </summary>
    public void SignIn()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (string.IsNullOrEmpty(facebookAppId))
        {
            OnLoginFailed?.Invoke("Facebook Login not configured.\nSet up Facebook App ID.");
            return;
        }

        if (!initialized)
        {
            Initialize();
            if (!initialized)
            {
                OnLoginFailed?.Invoke("Facebook Login failed to initialize.");
                return;
            }
        }

        try
        {
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            {
                var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                var permissions = new AndroidJavaObject("java.util.ArrayList");
                permissions.Call<bool>("add", "public_profile");
                permissions.Call<bool>("add", "email");

                loginManager.Call("logInWithReadPermissions", activity, permissions);
                pendingLogin = true;
            }
        }
        catch (Exception e)
        {
            Debug.LogError("Facebook Login error: " + e.Message);
            OnLoginFailed?.Invoke("Facebook Login failed: " + e.Message);
        }
#elif UNITY_EDITOR
        OnLoginFailed?.Invoke("Facebook Login only works on Android device.");
#else
        OnLoginFailed?.Invoke("Facebook Login not supported on this platform.");
#endif
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    void OnApplicationPause(bool paused)
    {
        if (!paused && pendingLogin)
        {
            // Check for access token when app resumes
            CheckAccessToken();
        }
    }

    void CheckAccessToken()
    {
        try
        {
            using (var atClass = new AndroidJavaClass("com.facebook.AccessToken"))
            {
                var currentToken = atClass.CallStatic<AndroidJavaObject>("getCurrentAccessToken");
                if (currentToken != null)
                {
                    string tokenString = currentToken.Call<string>("getToken");
                    if (!string.IsNullOrEmpty(tokenString))
                    {
                        pendingLogin = false;
                        Debug.Log("Facebook Login success, token length: " + tokenString.Length);
                        OnAccessTokenReceived?.Invoke(tokenString);
                        return;
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError("Facebook token check error: " + e.Message);
        }
    }

    /// <summary>
    /// AndroidJavaProxy for com.facebook.FacebookCallback<LoginResult>
    /// </summary>
    class FacebookCallback : AndroidJavaProxy
    {
        FacebookSignInBridge bridge;

        public FacebookCallback(FacebookSignInBridge bridge)
            : base("com.facebook.FacebookCallback")
        {
            this.bridge = bridge;
        }

        // onSuccess(LoginResult loginResult)
        void onSuccess(AndroidJavaObject loginResult)
        {
            try
            {
                var accessToken = loginResult.Call<AndroidJavaObject>("getAccessToken");
                string token = accessToken.Call<string>("getToken");
                bridge.pendingLogin = false;
                Debug.Log("Facebook callback onSuccess, token length: " + token.Length);
                UnityMainThreadDispatcher.Enqueue(() => {
                    bridge.OnAccessTokenReceived?.Invoke(token);
                });
            }
            catch (Exception e)
            {
                Debug.LogError("Facebook onSuccess error: " + e.Message);
                UnityMainThreadDispatcher.Enqueue(() => {
                    bridge.OnLoginFailed?.Invoke("Facebook Login error: " + e.Message);
                });
            }
        }

        // onCancel()
        void onCancel()
        {
            bridge.pendingLogin = false;
            UnityMainThreadDispatcher.Enqueue(() => {
                bridge.OnLoginFailed?.Invoke("Facebook Login cancelled.");
            });
        }

        // onError(FacebookException error)
        void onError(AndroidJavaObject error)
        {
            bridge.pendingLogin = false;
            string msg = error != null ? error.Call<string>("getMessage") : "Unknown error";
            UnityMainThreadDispatcher.Enqueue(() => {
                bridge.OnLoginFailed?.Invoke("Facebook Login failed: " + msg);
            });
        }
    }
#endif
}
