# MathStick — Online — auth, database, matchmaking, matches

> **Generated file — do not edit.** Regenerated from the sources listed below.
> The code in `Assets/` is the only source of truth; this exists so the whole
> project can be handed to a tool that reads documents rather than a repo.

> Firebase auth and database, the lobby, real 1v1 matches and the local bot opponent.

---

## `Assets/Scripts/AuthManager.cs`

```csharp
using UnityEngine;
using Firebase;
using Firebase.Auth;
using System;
using System.Threading.Tasks;

public class AuthManager : MonoBehaviour
{
    public static AuthManager Instance { get; private set; }

    FirebaseAuth auth;
    FirebaseUser currentUser;

    public FirebaseUser CurrentUser => currentUser;
    public bool IsLoggedIn => currentUser != null;
    public bool IsGuest => currentUser != null && currentUser.IsAnonymous;
    public string DisplayName => currentUser?.DisplayName ?? GenerateGuestName();

    public event Action OnLoginSuccess;
    public event Action<string> OnLoginFailed;
    public event Action OnLogout;

    void Awake()
    {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else { Destroy(gameObject); return; }
    }

    public void InitFirebase(Action onReady)
    {
        try
        {
            FirebaseApp.CheckAndFixDependenciesAsync().ContinueWith(task => {
                var status = task.Result;
                if (status == DependencyStatus.Available)
                {
                    auth = FirebaseAuth.DefaultInstance;
                    auth.StateChanged += OnAuthStateChanged;
                    Debug.Log("✓ Firebase Auth initialized successfully");

                    // The database layer can only come up once dependencies resolve,
                    // and it must be built on the main thread.
                    UnityMainThreadDispatcher.Enqueue(() =>
                    {
                        FirebaseDBManager.Instance?.Initialize();
                        onReady?.Invoke();
                    });
                }
                else
                {
                    Debug.LogError("✗ Firebase dependency error: " + status);
                }
            });
        }
        catch (Exception e)
        {
            Debug.LogError("✗ Firebase init exception: " + e.Message);
        }
    }

    void OnAuthStateChanged(object sender, EventArgs e)
    {
        if (auth == null) return;

        currentUser = auth.CurrentUser;
        if (currentUser == null) return;

        // Presence and the user row can only be written once we have a uid,
        // which is why this hangs off the auth state rather than InitFirebase.
        UnityMainThreadDispatcher.Enqueue(() =>
        {
            FirebaseDBManager.Instance?.GoOnline();
        });
    }

    public void LoginEmail(string email, string password)
    {
        if (auth == null) { OnLoginFailed?.Invoke("Firebase not initialized"); return; }
        auth.SignInWithEmailAndPasswordAsync(email, password).ContinueWith(task => {
            UnityMainThreadDispatcher.Enqueue(() => {
                if (task.IsCanceled)
                    OnLoginFailed?.Invoke("Login canceled");
                else if (task.IsFaulted)
                {
                    var errorMsg = GetFirebaseErrorMessage(task.Exception);
                    Debug.LogError("✗ Login failed: " + errorMsg);
                    OnLoginFailed?.Invoke(errorMsg);
                }
                else
                {
                    currentUser = task.Result.User;
                    Debug.Log("✓ Login successful: " + currentUser.Email);
                    OnLoginSuccess?.Invoke();
                }
            });
        });
    }

    public void RegisterEmail(string email, string password, string displayName)
    {
        if (auth == null) { OnLoginFailed?.Invoke("Firebase not initialized"); return; }
        auth.CreateUserWithEmailAndPasswordAsync(email, password).ContinueWith(task => {
            UnityMainThreadDispatcher.Enqueue(() => {
                if (task.IsCanceled)
                    OnLoginFailed?.Invoke("Registration canceled");
                else if (task.IsFaulted)
                {
                    var errorMsg = GetFirebaseErrorMessage(task.Exception);
                    Debug.LogError("✗ Register failed: " + errorMsg);
                    OnLoginFailed?.Invoke(errorMsg);
                }
                else
                {
                    currentUser = task.Result.User;
                    var profile = new UserProfile { DisplayName = displayName };
                    currentUser.UpdateUserProfileAsync(profile).ContinueWith(t => {
                        UnityMainThreadDispatcher.Enqueue(() => {
                            if (t.IsFaulted)
                            {
                                Debug.LogError("✗ Profile update failed: " + GetFirebaseErrorMessage(t.Exception));
                            }
                            else
                            {
                                Debug.Log("✓ Registration successful: " + currentUser.Email);
                            }
                            OnLoginSuccess?.Invoke();
                        });
                    });
                }
            });
        });
    }

    public void ResetPassword(string email)
    {
        if (auth == null) { OnLoginFailed?.Invoke("Firebase not initialized"); return; }
        auth.SendPasswordResetEmailAsync(email).ContinueWith(task => {
            UnityMainThreadDispatcher.Enqueue(() => {
                if (task.IsFaulted)
                {
                    var errorMsg = GetFirebaseErrorMessage(task.Exception);
                    Debug.LogError("✗ Reset password failed: " + errorMsg);
                    OnLoginFailed?.Invoke(errorMsg);
                }
                else
                {
                    Debug.Log("✓ Password reset email sent to: " + email);
                    OnLoginFailed?.Invoke("Reset link sent! Check your email.");
                }
            });
        });
    }

    public void LoginWithGoogle()
    {
        if (auth == null) { OnLoginFailed?.Invoke("Firebase not initialized"); return; }

        var bridge = GoogleSignInBridge.Instance;
        if (bridge == null)
        {
            OnLoginFailed?.Invoke("Google Sign-In not available.");
            return;
        }

        // Subscribe to bridge events (one-shot)
        Action<string> onToken = null;
        Action<string> onFail = null;

        onToken = (idToken) => {
            bridge.OnIdTokenReceived -= onToken;
            bridge.OnSignInFailed -= onFail;
            // Use the Google ID token to sign into Firebase
            var credential = GoogleAuthProvider.GetCredential(idToken, null);
            auth.SignInWithCredentialAsync(credential).ContinueWith(task => {
                UnityMainThreadDispatcher.Enqueue(() => {
                    if (task.IsCanceled)
                        OnLoginFailed?.Invoke("Google login canceled");
                    else if (task.IsFaulted)
                    {
                        var errorMsg = GetFirebaseErrorMessage(task.Exception);
                        Debug.LogError("Google Firebase auth failed: " + errorMsg);
                        OnLoginFailed?.Invoke(errorMsg);
                    }
                    else
                    {
                        currentUser = auth.CurrentUser;
                        Debug.Log("Google login successful: " + currentUser.Email);
                        OnLoginSuccess?.Invoke();
                    }
                });
            });
        };

        onFail = (error) => {
            bridge.OnIdTokenReceived -= onToken;
            bridge.OnSignInFailed -= onFail;
            OnLoginFailed?.Invoke(error);
        };

        bridge.OnIdTokenReceived += onToken;
        bridge.OnSignInFailed += onFail;
        bridge.SignIn();
    }

    public void LoginWithFacebook()
    {
        if (auth == null) { OnLoginFailed?.Invoke("Firebase not initialized"); return; }

        var bridge = FacebookSignInBridge.Instance;
        if (bridge == null)
        {
            OnLoginFailed?.Invoke("Facebook Login not available.");
            return;
        }

        Action<string> onToken = null;
        Action<string> onFail = null;

        onToken = (accessToken) => {
            bridge.OnAccessTokenReceived -= onToken;
            bridge.OnLoginFailed -= onFail;
            var credential = FacebookAuthProvider.GetCredential(accessToken);
            auth.SignInWithCredentialAsync(credential).ContinueWith(task => {
                UnityMainThreadDispatcher.Enqueue(() => {
                    if (task.IsCanceled)
                        OnLoginFailed?.Invoke("Facebook login canceled");
                    else if (task.IsFaulted)
                    {
                        var errorMsg = GetFirebaseErrorMessage(task.Exception);
                        Debug.LogError("Facebook Firebase auth failed: " + errorMsg);
                        OnLoginFailed?.Invoke(errorMsg);
                    }
                    else
                    {
                        currentUser = auth.CurrentUser;
                        Debug.Log("Facebook login successful: " + currentUser.Email);
                        OnLoginSuccess?.Invoke();
                    }
                });
            });
        };

        onFail = (error) => {
            bridge.OnAccessTokenReceived -= onToken;
            bridge.OnLoginFailed -= onFail;
            OnLoginFailed?.Invoke(error);
        };

        bridge.OnAccessTokenReceived += onToken;
        bridge.OnLoginFailed += onFail;
        bridge.SignIn();
    }

    /// <summary>
    /// Login as guest using Firebase Anonymous Auth.
    /// Creates a temporary account with a random name like "Player_84721".
    /// Guest can later link to email/Google/Facebook to keep their progress.
    /// </summary>
    public void LoginAsGuest()
    {
        if (auth == null) { OnLoginFailed?.Invoke("Firebase not initialized"); return; }

        auth.SignInAnonymouslyAsync().ContinueWith(task => {
            UnityMainThreadDispatcher.Enqueue(() => {
                if (task.IsCanceled)
                    OnLoginFailed?.Invoke("Guest login canceled");
                else if (task.IsFaulted)
                {
                    var errorMsg = GetFirebaseErrorMessage(task.Exception);
                    Debug.LogError("✗ Guest login failed: " + errorMsg);
                    OnLoginFailed?.Invoke(errorMsg);
                }
                else
                {
                    currentUser = auth.CurrentUser;
                    string guestName = GenerateGuestName();

                    // Set display name for guest
                    var profile = new UserProfile { DisplayName = guestName };
                    currentUser.UpdateUserProfileAsync(profile).ContinueWith(t => {
                        UnityMainThreadDispatcher.Enqueue(() => {
                            if (t.IsFaulted)
                                Debug.LogError("✗ Guest profile update failed: " + GetFirebaseErrorMessage(t.Exception));
                            else
                                Debug.Log("✓ Guest login successful: " + guestName);
                            OnLoginSuccess?.Invoke();
                        });
                    });
                }
            });
        });
    }

    /// <summary>
    /// Link guest account to email/password so they keep their progress.
    /// </summary>
    public void LinkGuestToEmail(string email, string password, string displayName)
    {
        if (auth == null || currentUser == null) { OnLoginFailed?.Invoke("Not logged in"); return; }
        if (!currentUser.IsAnonymous) { OnLoginFailed?.Invoke("Not a guest account"); return; }

        var credential = EmailAuthProvider.GetCredential(email, password);
        currentUser.LinkWithCredentialAsync(credential).ContinueWith(task => {
            UnityMainThreadDispatcher.Enqueue(() => {
                if (task.IsFaulted)
                {
                    var errorMsg = GetFirebaseErrorMessage(task.Exception);
                    Debug.LogError("✗ Link failed: " + errorMsg);
                    OnLoginFailed?.Invoke(errorMsg);
                }
                else
                {
                    currentUser = auth.CurrentUser;
                    var profile = new UserProfile { DisplayName = displayName };
                    currentUser.UpdateUserProfileAsync(profile).ContinueWith(t => {
                        UnityMainThreadDispatcher.Enqueue(() => {
                            Debug.Log("✓ Account linked: " + email);
                            OnLoginSuccess?.Invoke();
                        });
                    });
                }
            });
        });
    }

    string GenerateGuestName()
    {
        // Use a saved name if we already generated one, otherwise create new
        string saved = PlayerPrefs.GetString("GuestName", "");
        if (!string.IsNullOrEmpty(saved)) return saved;

        string name = "Player_" + UnityEngine.Random.Range(10000, 99999);
        PlayerPrefs.SetString("GuestName", name);
        PlayerPrefs.Save();
        return name;
    }

    public void Logout()
    {
        if (auth != null)
            auth.SignOut();
        currentUser = null;
        PlayerPrefs.DeleteKey("GuestName");
        OnLogout?.Invoke();
    }

    string GetFirebaseErrorMessage(System.Exception exception)
    {
        if (exception == null) return "Unknown error";

        // Log full details for debugging
        Debug.LogError("Full exception: " + exception);
        if (exception.InnerException != null)
            Debug.LogError("Inner exception: " + exception.InnerException);

        var firebaseEx = exception.InnerException as Firebase.FirebaseException;
        if (firebaseEx != null)
        {
            Debug.LogError("Firebase error code: " + firebaseEx.ErrorCode);
            var code = (Firebase.Auth.AuthError)firebaseEx.ErrorCode;
            return code switch
            {
                Firebase.Auth.AuthError.UserNotFound => "Account doesn't exist. Please sign up.",
                Firebase.Auth.AuthError.WrongPassword => "Incorrect password.",
                Firebase.Auth.AuthError.InvalidEmail => "Invalid email format.",
                Firebase.Auth.AuthError.EmailAlreadyInUse => "Email already in use.",
                Firebase.Auth.AuthError.WeakPassword => "Password too weak (min 6 chars).",
                Firebase.Auth.AuthError.AccountExistsWithDifferentCredentials => "Account exists with different credentials.",
                _ => $"Firebase error: {code}"
            };
        }
        return exception.InnerException?.Message ?? exception.Message ?? "Unknown error";
    }
}
```

## `Assets/Scripts/FirebaseDBManager.cs`

```csharp
using UnityEngine;
using Firebase;
using Firebase.Database;
using System;
using System.Collections.Generic;

public class FirebaseDBManager : MonoBehaviour
{
    public static FirebaseDBManager Instance { get; private set; }

    DatabaseReference root;
    DatabaseReference presenceRef;
    bool initialized = false;

    // Difference between server clock and this device's clock, in milliseconds.
    // Kept live via the .info/serverTimeOffset special node so daily streaks
    // cannot be farmed by changing the device date.
    long serverTimeOffsetMs = 0;

    public DatabaseReference Root => root;
    public bool IsInitialized => initialized;

    /// <summary>Server-side UTC time, or device UTC time if the offset isn't known yet.</summary>
    public DateTime ServerNowUtc =>
        DateTimeOffset.FromUnixTimeMilliseconds(
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + serverTimeOffsetMs).UtcDateTime;

    /// <summary>Server date as "yyyy-MM-dd" — the key daily streaks are compared on.</summary>
    public string ServerDateKey => ServerNowUtc.ToString("yyyy-MM-dd");

    /// <summary>Server month as "yyyy-MM" — the key monthly leaderboards are stored under.</summary>
    public string ServerMonthKey => ServerNowUtc.ToString("yyyy-MM");

    void Awake()
    {
        if (Instance == null) { Instance = this; }
        else { Destroy(this); return; }
    }

    /// <summary>
    /// Called after Firebase Auth is ready (from AuthManager.InitFirebase callback).
    /// </summary>
    public void Initialize()
    {
        try
        {
            root = FirebaseDatabase.DefaultInstance.RootReference;
            initialized = true;
            Debug.Log("FirebaseDB initialized");

            TrackServerTimeOffset();

            if (AuthManager.Instance != null && AuthManager.Instance.IsLoggedIn)
                SetupPresence();
        }
        catch (Exception e)
        {
            Debug.LogError("FirebaseDB init failed: " + e.Message);
        }
    }

    /// <summary>
    /// Subscribes to Firebase's .info/serverTimeOffset so ServerNowUtc stays accurate
    /// even if the player changes their device clock.
    /// </summary>
    void TrackServerTimeOffset()
    {
        try
        {
            FirebaseDatabase.DefaultInstance
                .GetReference(".info/serverTimeOffset")
                .ValueChanged += (sender, e) =>
                {
                    if (e.DatabaseError != null || e.Snapshot == null || !e.Snapshot.Exists) return;

                    long offset;
                    if (long.TryParse(e.Snapshot.Value.ToString(), out offset))
                        serverTimeOffsetMs = offset;
                    else
                    {
                        double d;
                        if (double.TryParse(e.Snapshot.Value.ToString(), out d))
                            serverTimeOffsetMs = (long)d;
                    }
                };
        }
        catch (Exception e)
        {
            Debug.LogWarning("Server time offset unavailable, using device clock: " + e.Message);
        }
    }

    /// <summary>
    /// Sets up online presence tracking for the current user.
    /// Writes to presence/{uid} and registers onDisconnect to remove it.
    /// Also writes user profile data to users/{uid}.
    /// </summary>
    public void SetupPresence()
    {
        if (!initialized || root == null) return;

        var user = AuthManager.Instance.CurrentUser;
        if (user == null) return;

        string uid = user.UserId;

        // Write user profile
        var userRef = root.Child("users").Child(uid);
        userRef.Child("displayName").SetValueAsync(AuthManager.Instance.DisplayName);
        userRef.Child("online").SetValueAsync(true);
        userRef.Child("lastSeen").SetValueAsync(ServerValue.Timestamp);

        // Presence node
        presenceRef = root.Child("presence").Child(uid);
        presenceRef.SetValueAsync(true);

        // On disconnect: remove presence and set online=false
        presenceRef.OnDisconnect().RemoveValue();
        userRef.Child("online").OnDisconnect().SetValue(false);
        userRef.Child("lastSeen").OnDisconnect().SetValue(ServerValue.Timestamp);

        Debug.Log("Presence setup for: " + uid);
    }

    /// <summary>
    /// Remove presence immediately (app quit/pause).
    /// </summary>
    public void GoOffline()
    {
        if (presenceRef != null)
        {
            presenceRef.RemoveValueAsync();
            presenceRef = null;
        }

        if (root != null && AuthManager.Instance != null && AuthManager.Instance.CurrentUser != null)
        {
            string uid = AuthManager.Instance.CurrentUser.UserId;
            root.Child("users").Child(uid).Child("online").SetValueAsync(false);
            root.Child("users").Child(uid).Child("lastSeen").SetValueAsync(ServerValue.Timestamp);
        }
    }

    /// <summary>
    /// Re-establish presence after returning from pause.
    /// </summary>
    public void GoOnline()
    {
        if (initialized && AuthManager.Instance != null && AuthManager.Instance.IsLoggedIn)
            SetupPresence();
    }

    /// <summary>
    /// Shorthand for root.Child(path).
    /// </summary>
    public DatabaseReference GetRef(string path)
    {
        return root?.Child(path);
    }

    void OnApplicationPause(bool paused)
    {
        // A fullscreen ad pauses the app without the player going anywhere.
        // Dropping presence here would let the opponent's disconnect grace
        // period forfeit a live match while an ad is on screen.
        if (AdManager.Instance != null && AdManager.Instance.IsShowing) return;

        if (paused)
            GoOffline();
        else
            GoOnline();
    }

    void OnApplicationQuit()
    {
        GoOffline();
    }
}
```

## `Assets/Scripts/LobbyManager.cs`

```csharp
using UnityEngine;
using Firebase.Database;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class LobbyManager : MonoBehaviour
{
    public static LobbyManager Instance { get; private set; }

    // Events for UI binding
    public event Action<List<OnlineUser>> OnOnlineUsersUpdated;
    public event Action<InviteData> OnInviteReceived;
    public event Action<string> OnMatchFound; // matchId
    public event Action<string> OnError;
    public event Action<string> OnInviteAccepted; // matchId — fires on sender side when invite is accepted
    public event Action OnInviteDeclined;
    public event Action<GameMode, int> OnBotFallback; // no human found — play a bot instead

    /// <summary>
    /// How long to look for a human before falling back to a bot opponent.
    /// Without this the queue dead-ends whenever nobody else is online.
    /// </summary>
    public float botFallbackSeconds = 5f;

    List<OnlineUser> onlineUsers = new List<OnlineUser>();
    List<OnlineUser> friendsList = new List<OnlineUser>();
    HashSet<string> friendUids = new HashSet<string>();
    bool isListeningUsers = false;
    bool isSearchingRandom = false;
    Coroutine matchmakingCo;

    public event Action<List<OnlineUser>> OnFriendsUpdated;

    // Current invite being sent (for tracking response)
    string pendingInviteTargetUid;
    string pendingInviteId;
    DatabaseReference pendingInviteRef;

    // Matchmaking settings
    GameMode queuedMode;
    int queuedFirstTo;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(this); return; }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Online Users
    // ═══════════════════════════════════════════════════════════════════

    public void StartListeningOnlineUsers()
    {
        if (isListeningUsers) return;
        var db = FirebaseDBManager.Instance;
        if (db == null || !db.IsInitialized) return;

        isListeningUsers = true;
        db.GetRef("presence").ValueChanged += OnPresenceChanged;
    }

    public void StopListeningOnlineUsers()
    {
        if (!isListeningUsers) return;
        isListeningUsers = false;

        var db = FirebaseDBManager.Instance;
        if (db != null && db.IsInitialized)
            db.GetRef("presence").ValueChanged -= OnPresenceChanged;
    }

    void OnPresenceChanged(object sender, ValueChangedEventArgs e)
    {
        if (e.DatabaseError != null)
        {
            Debug.LogError("Presence read error: " + e.DatabaseError.Message);
            return;
        }

        UnityMainThreadDispatcher.Enqueue(() =>
        {
            onlineUsers.Clear();
            if (e.Snapshot == null || !e.Snapshot.Exists) return;

            string myUid = AuthManager.Instance?.CurrentUser?.UserId;
            var uids = new List<string>();

            foreach (var child in e.Snapshot.Children)
            {
                if (child.Key == myUid) continue; // skip self
                uids.Add(child.Key);
            }

            // Fetch display names for all online users
            StartCoroutine(FetchUserNames(uids));
        });
    }

    IEnumerator FetchUserNames(List<string> uids)
    {
        var users = new List<OnlineUser>();
        var db = FirebaseDBManager.Instance;

        foreach (var uid in uids)
        {
            var task = db.GetRef("users").Child(uid).Child("displayName").GetValueAsync();
            yield return new WaitUntil(() => task.IsCompleted);

            string name = "Player";
            if (task.Result != null && task.Result.Exists)
                name = task.Result.Value.ToString();

            users.Add(new OnlineUser { uid = uid, displayName = name, isFriend = friendUids.Contains(uid), isOnline = true });
        }

        onlineUsers = users;
        OnOnlineUsersUpdated?.Invoke(onlineUsers);
    }

    public List<OnlineUser> SearchUsers(string query)
    {
        if (string.IsNullOrEmpty(query))
            return new List<OnlineUser>(onlineUsers);

        string q = query.ToLower();
        return onlineUsers.Where(u => u.displayName.ToLower().Contains(q)).ToList();
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Random Matchmaking
    // ═══════════════════════════════════════════════════════════════════

    public void EnterRandomQueue(GameMode mode, int firstTo)
    {
        if (isSearchingRandom) return;
        var db = FirebaseDBManager.Instance;
        if (db == null || !db.IsInitialized) return;

        string myUid = AuthManager.Instance.CurrentUser.UserId;
        string myName = AuthManager.Instance.DisplayName;
        queuedMode = mode;
        queuedFirstTo = firstTo;
        isSearchingRandom = true;

        var data = new Dictionary<string, object>
        {
            ["displayName"] = myName,
            ["timestamp"] = ServerValue.Timestamp,
            ["mode"] = mode.ToString(),
            ["firstTo"] = firstTo
        };

        db.GetRef("matchmaking").Child(myUid).SetValueAsync(data).ContinueWith(t =>
        {
            UnityMainThreadDispatcher.Enqueue(() =>
            {
                if (t.IsFaulted)
                {
                    isSearchingRandom = false;
                    OnError?.Invoke("Failed to join queue");
                    return;
                }
                matchmakingCo = StartCoroutine(PollForMatch());
            });
        });
    }

    public void LeaveRandomQueue()
    {
        if (!isSearchingRandom) return;
        isSearchingRandom = false;

        if (matchmakingCo != null) { StopCoroutine(matchmakingCo); matchmakingCo = null; }

        var db = FirebaseDBManager.Instance;
        if (db != null && db.IsInitialized)
        {
            string myUid = AuthManager.Instance?.CurrentUser?.UserId;
            if (myUid != null)
                db.GetRef("matchmaking").Child(myUid).RemoveValueAsync();
        }
    }

    IEnumerator PollForMatch()
    {
        var db = FirebaseDBManager.Instance;
        string myUid = AuthManager.Instance.CurrentUser.UserId;
        float searching = 0f;

        while (isSearchingRandom)
        {
            // Poll fast enough that the bot fallback fires close to its stated
            // delay; the jitter keeps two clients from scanning in lockstep.
            float wait = 1f + UnityEngine.Random.Range(0f, 0.3f);
            yield return new WaitForSeconds(wait);
            if (!isSearchingRandom) yield break;

            searching += wait;

            // Were WE claimed since the last poll? The claimant writes the
            // matchId into OUR queue entry — finding it there is the handshake.
            // Without this check the claimed side never learns the match id:
            // its entry just vanished, it kept polling an empty queue, and the
            // bot fallback quietly swallowed every real 1v1 pairing.
            var mineTask = db.GetRef("matchmaking").Child(myUid).GetValueAsync();
            yield return new WaitUntil(() => mineTask.IsCompleted);
            if (!isSearchingRandom) yield break;

            if (!mineTask.IsFaulted && mineTask.Result != null && mineTask.Result.Exists)
            {
                var claimedId = mineTask.Result.Child("matchId");
                if (claimedId != null && claimedId.Value != null)
                {
                    string foundMatchId = claimedId.Value.ToString();
                    isSearchingRandom = false;
                    matchmakingCo = null;
                    db.GetRef("matchmaking").Child(myUid).RemoveValueAsync();

                    OnMatchFound?.Invoke(foundMatchId);
                    yield break;
                }
            }

            // Nobody showed up — hand the player a bot rather than an empty queue
            if (searching >= botFallbackSeconds)
            {
                // Don't StopCoroutine ourselves here — just unwind and let the
                // handle go, otherwise the event below would never fire.
                isSearchingRandom = false;
                matchmakingCo = null;
                db.GetRef("matchmaking").Child(myUid).RemoveValueAsync();

                OnBotFallback?.Invoke(queuedMode, queuedFirstTo);
                yield break;
            }

            var task = db.GetRef("matchmaking").GetValueAsync();
            yield return new WaitUntil(() => task.IsCompleted);

            if (task.IsFaulted || task.Result == null) continue;

            foreach (var child in task.Result.Children)
            {
                if (child.Key == myUid) continue;

                string oppMode = child.Child("mode").Value?.ToString() ?? "";
                int oppFirstTo = 3;
                if (child.Child("firstTo").Value != null)
                    int.TryParse(child.Child("firstTo").Value.ToString(), out oppFirstTo);

                // Check compatibility
                bool modeOk = queuedMode.ToString() == oppMode
                    || queuedMode.ToString() == "Random"
                    || oppMode == "Random";
                bool firstToOk = queuedFirstTo == oppFirstTo;

                if (modeOk && firstToOk)
                {
                    // Found a match — try to claim it via transaction
                    string oppUid = child.Key;
                    string oppName = child.Child("displayName").Value?.ToString() ?? "Player";

                    // Resolve mode if one is Random
                    GameMode resolvedMode = queuedMode;
                    if (queuedMode.ToString() == "Random" && oppMode != "Random")
                        Enum.TryParse(oppMode, out resolvedMode);
                    else if (queuedMode.ToString() == "Random")
                        resolvedMode = (GameMode)UnityEngine.Random.Range(0, 3); // Easy/Medium/Hard

                    yield return TryClaimMatch(oppUid, oppName, resolvedMode, queuedFirstTo);
                    if (!isSearchingRandom) yield break; // match was created
                }
            }
        }
    }

    IEnumerator TryClaimMatch(string oppUid, string oppName, GameMode mode, int firstTo)
    {
        var db = FirebaseDBManager.Instance;
        string myUid = AuthManager.Instance.CurrentUser.UserId;

        // The match id is minted BEFORE the claim so it can ride inside it —
        // push ids are generated client-side, no server round-trip involved.
        string matchId = db.GetRef("matches").Push().Key;

        // Claim = a CAS on the OPPONENT's queue entry alone. That single node
        // is the contention point: whichever host's transaction lands first
        // wins, the other aborts and keeps searching. Scoping it to the child
        // (rather than transacting on the whole matchmaking node) matters:
        //  - the security rules only grant child-level writes here — a root
        //    transaction would need a root .write that lets any client wipe
        //    the entire queue;
        //  - writing {matchId, hostUid} instead of deleting is the handshake
        //    the claimed side's poll is looking for (see PollForMatch).
        var txTask = db.GetRef("matchmaking").Child(oppUid).RunTransaction(mutableData =>
        {
            if (mutableData.Value == null)
                return TransactionResult.Abort(); // opponent already taken

            mutableData.Value = new Dictionary<string, object>
            {
                ["matchId"] = matchId,
                ["hostUid"] = myUid
            };
            return TransactionResult.Success(mutableData);
        });

        yield return new WaitUntil(() => txTask.IsCompleted);

        if (txTask.IsFaulted || txTask.IsCanceled)
        {
            // Failed — someone else claimed this opponent, keep searching
            yield break;
        }

        // Won the claim: leave the queue ourselves (owner delete, always
        // allowed by the rules), then create the match at the id we handed over.
        db.GetRef("matchmaking").Child(myUid).RemoveValueAsync();

        // Success — create match
        isSearchingRandom = false;
        CreateMatch(oppUid, oppName, mode, firstTo, true, matchId);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Invites
    // ═══════════════════════════════════════════════════════════════════

    public void SendInvite(string targetUid, GameMode mode, int firstTo)
    {
        var db = FirebaseDBManager.Instance;
        if (db == null || !db.IsInitialized) return;

        string myUid = AuthManager.Instance.CurrentUser.UserId;
        string myName = AuthManager.Instance.DisplayName;

        var inviteRef = db.GetRef("invites").Child(targetUid).Push();
        pendingInviteId = inviteRef.Key;
        pendingInviteTargetUid = targetUid;

        var data = new Dictionary<string, object>
        {
            ["fromUid"] = myUid,
            ["fromName"] = myName,
            ["mode"] = mode.ToString(),
            ["firstTo"] = firstTo,
            ["timestamp"] = ServerValue.Timestamp,
            ["status"] = "pending"
        };

        inviteRef.SetValueAsync(data).ContinueWith(t =>
        {
            UnityMainThreadDispatcher.Enqueue(() =>
            {
                if (t.IsFaulted)
                {
                    OnError?.Invoke("Failed to send invite");
                    return;
                }
                // Listen for status change
                pendingInviteRef = inviteRef;
                inviteRef.Child("status").ValueChanged += OnSentInviteStatusChanged;
            });
        });
    }

    void OnSentInviteStatusChanged(object sender, ValueChangedEventArgs e)
    {
        if (e.Snapshot == null || !e.Snapshot.Exists) return;
        string status = e.Snapshot.Value.ToString();

        UnityMainThreadDispatcher.Enqueue(() =>
        {
            if (status == "accepted")
            {
                // Clean up listener
                if (pendingInviteRef != null)
                    pendingInviteRef.Child("status").ValueChanged -= OnSentInviteStatusChanged;

                // Read matchId from the invite node
                pendingInviteRef.Child("matchId").GetValueAsync().ContinueWith(t =>
                {
                    UnityMainThreadDispatcher.Enqueue(() =>
                    {
                        string matchId = t.Result?.Value?.ToString();
                        if (matchId != null)
                            OnInviteAccepted?.Invoke(matchId);
                    });
                });
            }
            else if (status == "declined")
            {
                if (pendingInviteRef != null)
                    pendingInviteRef.Child("status").ValueChanged -= OnSentInviteStatusChanged;
                OnInviteDeclined?.Invoke();
            }
        });
    }

    public void StartListeningInvites()
    {
        var db = FirebaseDBManager.Instance;
        if (db == null || !db.IsInitialized) return;

        string myUid = AuthManager.Instance?.CurrentUser?.UserId;
        if (myUid == null) return;

        db.GetRef("invites").Child(myUid).ChildAdded += OnInviteChildAdded;
    }

    public void StopListeningInvites()
    {
        var db = FirebaseDBManager.Instance;
        if (db == null || !db.IsInitialized) return;

        string myUid = AuthManager.Instance?.CurrentUser?.UserId;
        if (myUid == null) return;

        db.GetRef("invites").Child(myUid).ChildAdded -= OnInviteChildAdded;
    }

    void OnInviteChildAdded(object sender, ChildChangedEventArgs e)
    {
        if (e.Snapshot == null || !e.Snapshot.Exists) return;

        string status = e.Snapshot.Child("status").Value?.ToString() ?? "";
        if (status != "pending") return;

        UnityMainThreadDispatcher.Enqueue(() =>
        {
            GameMode mode = GameMode.Easy;
            Enum.TryParse(e.Snapshot.Child("mode").Value?.ToString() ?? "Easy", out mode);

            int firstTo = 3;
            if (e.Snapshot.Child("firstTo").Value != null)
                int.TryParse(e.Snapshot.Child("firstTo").Value.ToString(), out firstTo);

            var invite = new InviteData
            {
                inviteId = e.Snapshot.Key,
                fromUid = e.Snapshot.Child("fromUid").Value?.ToString() ?? "",
                fromName = e.Snapshot.Child("fromName").Value?.ToString() ?? "Player",
                mode = mode,
                firstTo = firstTo,
                status = "pending"
            };

            OnInviteReceived?.Invoke(invite);
        });
    }

    public void AcceptInvite(InviteData invite)
    {
        var db = FirebaseDBManager.Instance;
        if (db == null || !db.IsInitialized) return;

        string myUid = AuthManager.Instance.CurrentUser.UserId;
        string myName = AuthManager.Instance.DisplayName;

        // Create match first
        string matchId = CreateMatch(invite.fromUid, invite.fromName, invite.mode, invite.firstTo);

        // Update invite with accepted status and matchId
        var inviteRef = db.GetRef("invites").Child(myUid).Child(invite.inviteId);
        inviteRef.Child("status").SetValueAsync("accepted");
        inviteRef.Child("matchId").SetValueAsync(matchId);
    }

    public void DeclineInvite(InviteData invite)
    {
        var db = FirebaseDBManager.Instance;
        if (db == null || !db.IsInitialized) return;

        string myUid = AuthManager.Instance.CurrentUser.UserId;
        db.GetRef("invites").Child(myUid).Child(invite.inviteId)
            .Child("status").SetValueAsync("declined");
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Match Creation
    // ═══════════════════════════════════════════════════════════════════

    /// <param name="notify">
    /// When false the caller joins the match itself instead of going through
    /// OnMatchFound — used by rematches, where both sides already know each other.
    /// </param>
    public string CreateMatch(string opponentUid, string opponentName, GameMode mode, int firstTo, bool notify = true, string presetMatchId = null)
    {
        var db = FirebaseDBManager.Instance;
        string myUid = AuthManager.Instance.CurrentUser.UserId;
        string myName = AuthManager.Instance.DisplayName;

        // A queue claim mints the id up front so it can travel inside the
        // claim write (push ids are client-generated); everyone else gets a
        // fresh one here.
        var matchRef = presetMatchId != null
            ? db.GetRef("matches").Child(presetMatchId)
            : db.GetRef("matches").Push();
        string matchId = matchRef.Key;

        var matchData = new Dictionary<string, object>
        {
            ["hostUid"] = myUid,
            ["players"] = new Dictionary<string, object>
            {
                [myUid] = new Dictionary<string, object> { ["name"] = myName, ["ready"] = true },
                [opponentUid] = new Dictionary<string, object> { ["name"] = opponentName, ["ready"] = false }
            },
            ["settings"] = new Dictionary<string, object>
            {
                ["mode"] = mode.ToString(),
                ["firstTo"] = firstTo
            },
            ["scores"] = new Dictionary<string, object>
            {
                [myUid] = 0,
                [opponentUid] = 0
            },
            ["currentRound"] = 1,
            ["state"] = "waiting",
            ["winner"] = ""
        };

        matchRef.SetValueAsync(matchData).ContinueWith(t =>
        {
            UnityMainThreadDispatcher.Enqueue(() =>
            {
                if (t.IsFaulted)
                {
                    OnError?.Invoke("Failed to create match");
                    return;
                }
                if (notify) OnMatchFound?.Invoke(matchId);
            });
        });

        return matchId;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Friends System
    // ═══════════════════════════════════════════════════════════════════

    public void LoadFriends()
    {
        var db = FirebaseDBManager.Instance;
        if (db == null || !db.IsInitialized) return;
        string myUid = AuthManager.Instance?.CurrentUser?.UserId;
        if (myUid == null) return;

        db.GetRef("friends").Child(myUid).GetValueAsync().ContinueWith(t =>
        {
            UnityMainThreadDispatcher.Enqueue(() =>
            {
                friendUids.Clear();
                if (t.Result != null && t.Result.Exists)
                {
                    foreach (var child in t.Result.Children)
                        friendUids.Add(child.Key);
                }
                RefreshFriendsList();
            });
        });
    }

    public void AddFriend(string friendUid, string friendName)
    {
        var db = FirebaseDBManager.Instance;
        if (db == null || !db.IsInitialized) return;
        string myUid = AuthManager.Instance?.CurrentUser?.UserId;
        string myName = AuthManager.Instance?.DisplayName ?? "Player";
        if (myUid == null || friendUid == myUid) return;

        // Add both ways
        db.GetRef("friends").Child(myUid).Child(friendUid).SetValueAsync(friendName);
        db.GetRef("friends").Child(friendUid).Child(myUid).SetValueAsync(myName);

        friendUids.Add(friendUid);
        RefreshFriendsList();
    }

    public void RemoveFriend(string friendUid)
    {
        var db = FirebaseDBManager.Instance;
        if (db == null || !db.IsInitialized) return;
        string myUid = AuthManager.Instance?.CurrentUser?.UserId;
        if (myUid == null) return;

        db.GetRef("friends").Child(myUid).Child(friendUid).RemoveValueAsync();
        db.GetRef("friends").Child(friendUid).Child(myUid).RemoveValueAsync();

        friendUids.Remove(friendUid);
        RefreshFriendsList();
    }

    public bool IsFriend(string uid) => friendUids.Contains(uid);

    void RefreshFriendsList()
    {
        if (friendUids.Count == 0)
        {
            friendsList.Clear();
            OnFriendsUpdated?.Invoke(friendsList);
            return;
        }

        StartCoroutine(FetchFriendsWithStatus());
    }

    IEnumerator FetchFriendsWithStatus()
    {
        var db = FirebaseDBManager.Instance;
        var friends = new List<OnlineUser>();

        foreach (var uid in friendUids)
        {
            // Get name
            var nameTask = db.GetRef("users").Child(uid).Child("displayName").GetValueAsync();
            yield return new WaitUntil(() => nameTask.IsCompleted);
            string name = "Player";
            if (nameTask.Result != null && nameTask.Result.Exists)
                name = nameTask.Result.Value.ToString();

            // Check online status
            var presTask = db.GetRef("presence").Child(uid).GetValueAsync();
            yield return new WaitUntil(() => presTask.IsCompleted);
            bool online = presTask.Result != null && presTask.Result.Exists;

            friends.Add(new OnlineUser
            {
                uid = uid,
                displayName = name,
                isFriend = true,
                isOnline = online
            });
        }

        // Sort: online first, then alphabetical
        friends.Sort((a, b) =>
        {
            if (a.isOnline != b.isOnline) return b.isOnline.CompareTo(a.isOnline);
            return string.Compare(a.displayName, b.displayName, System.StringComparison.OrdinalIgnoreCase);
        });

        friendsList = friends;
        OnFriendsUpdated?.Invoke(friendsList);
    }

    void OnDestroy()
    {
        StopListeningOnlineUsers();
        StopListeningInvites();
        LeaveRandomQueue();
    }
}
```

## `Assets/Scripts/ArcadeMatchManager.cs`

```csharp
using UnityEngine;
using Firebase.Database;
using System;
using System.Collections;
using System.Collections.Generic;

public class ArcadeMatchManager : MonoBehaviour
{
    public static ArcadeMatchManager Instance { get; private set; }

    string matchId;
    string myUid;
    string opponentUid;
    string opponentName;
    bool isHost;
    int myScore;
    int opponentScore;
    int currentRound;
    int firstTo;
    GameMode matchMode;
    bool roundActive;

    // Events
    public event Action<int, int> OnScoreUpdated;          // myScore, oppScore
    public event Action<int, bool> OnRoundResult;          // round#, iWon
    public event Action<bool> OnMatchResult;               // iWon
    public event Action OnEquationReady;
    public event Action OnOpponentDisconnected;
    public event Action<string> OnError;
    public event Action<string> OnOpponentJoined;          // opponentName

    // Rematch
    public event Action OnRematchRequestedByOpponent;      // they asked first
    public event Action OnRematchDeclined;                 // they said no / left
    public event Action OnRematchTimedOut;
    public event Action OnRematchStarting;                 // both agreed, joining new match

    DatabaseReference matchRef;
    bool listening = false;
    bool waitingForOpponent = false;

    bool matchEnded = false;
    bool rematchListening = false;
    bool iWantRematch = false;
    bool nextMatchCreated = false;
    Coroutine rematchTimeoutCo;

    public bool IsInMatch => matchId != null;
    public bool IsHost => isHost;
    public string OpponentName => opponentName;
    public string OpponentUid => opponentUid;
    public bool MatchEnded => matchEnded;
    public int MyScore => myScore;
    public int OpponentScore => opponentScore;
    public int CurrentRound => currentRound;
    public int FirstTo => firstTo;
    public GameMode MatchMode => matchMode;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(this); return; }

        Messenger.AddListener(Message.ArcadeRoundWon, OnLocalPlayerSolved);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Join Match
    // ═══════════════════════════════════════════════════════════════════

    public void JoinMatch(string matchId)
    {
        var db = FirebaseDBManager.Instance;
        if (db == null || !db.IsInitialized) return;

        this.matchId = matchId;
        myUid = AuthManager.Instance.CurrentUser.UserId;
        myScore = 0;
        opponentScore = 0;
        currentRound = 1;
        roundActive = false;
        matchEnded = false;
        iWantRematch = false;
        nextMatchCreated = false;

        matchRef = db.GetRef("matches").Child(matchId);

        // Read match data
        matchRef.GetValueAsync().ContinueWith(t =>
        {
            UnityMainThreadDispatcher.Enqueue(() =>
            {
                if (t.IsFaulted || t.Result == null)
                {
                    OnError?.Invoke("Failed to read match data");
                    return;
                }

                var snap = t.Result;
                string hostUid = snap.Child("hostUid").Value?.ToString() ?? "";
                isHost = (hostUid == myUid);

                // Parse settings
                string modeStr = snap.Child("settings/mode").Value?.ToString() ?? "Easy";
                Enum.TryParse(modeStr, out matchMode);
                firstTo = 3;
                if (snap.Child("settings/firstTo").Value != null)
                    int.TryParse(snap.Child("settings/firstTo").Value.ToString(), out firstTo);

                // Find opponent
                foreach (var playerSnap in snap.Child("players").Children)
                {
                    if (playerSnap.Key != myUid)
                    {
                        opponentUid = playerSnap.Key;
                        opponentName = playerSnap.Child("name").Value?.ToString() ?? "Player";
                    }
                }

                // Mark self as ready
                matchRef.Child("players").Child(myUid).Child("ready").SetValueAsync(true);

                // Set game mode
                GameManager.Instance.SetMode(matchMode);
                GameManager.Instance.isArcadeMode = true;

                // Start listening
                StartListening();

                // If host, wait for opponent ready then generate equation
                if (isHost)
                {
                    waitingForOpponent = true;
                    matchRef.Child("players").Child(opponentUid).Child("ready")
                        .ValueChanged += OnOpponentReadyChanged;
                }
            });
        });
    }

    void OnOpponentReadyChanged(object sender, ValueChangedEventArgs e)
    {
        if (e.Snapshot == null || !e.Snapshot.Exists) return;
        bool ready = e.Snapshot.Value.ToString() == "True" || e.Snapshot.Value.ToString() == "true";

        if (ready && waitingForOpponent)
        {
            waitingForOpponent = false;
            matchRef.Child("players").Child(opponentUid).Child("ready")
                .ValueChanged -= OnOpponentReadyChanged;

            UnityMainThreadDispatcher.Enqueue(() =>
            {
                OnOpponentJoined?.Invoke(opponentName);
                GenerateAndPublishEquation();
            });
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Equation Generation & Publishing (Host only)
    // ═══════════════════════════════════════════════════════════════════

    void GenerateAndPublishEquation()
    {
        if (!isHost) return;

        // Use GameManager's generation logic
        var gm = GameManager.Instance;
        int[] solution = null;
        bool isMinus = false;

        // Try to generate a valid puzzle
        for (int attempt = 0; attempt < 200; attempt++)
        {
            if (matchMode == GameMode.Medium)
                solution = gm.GenerateCandidate3D();
            else if (matchMode == GameMode.Hard)
                solution = gm.GenerateCandidateHard();
            else
                solution = gm.GenerateCandidate();

            if (solution != null) break;
        }

        if (solution == null)
        {
            OnError?.Invoke("Failed to generate equation");
            return;
        }

        // Randomly assign plus/minus (for Easy/Medium)
        if (matchMode != GameMode.Hard)
            isMinus = UnityEngine.Random.Range(0, 2) == 1;

        // Write equation to Firebase
        var eqData = new Dictionary<string, object>
        {
            ["generated"] = true,
            ["isMinus"] = isMinus
        };

        // Write solution array
        var solDict = new Dictionary<string, object>();
        for (int i = 0; i < solution.Length; i++)
            solDict[i.ToString()] = solution[i];
        eqData["solution"] = solDict;

        string roundPath = "rounds/" + currentRound + "/equation";
        matchRef.Child(roundPath).SetValueAsync(eqData).ContinueWith(t =>
        {
            if (t.IsFaulted)
            {
                UnityMainThreadDispatcher.Enqueue(() => OnError?.Invoke("Failed to publish equation"));
            }
        });
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Listening
    // ═══════════════════════════════════════════════════════════════════

    void StartListening()
    {
        if (listening) return;
        listening = true;

        // Listen for equation (both host and guest)
        ListenForEquation(currentRound);

        // Listen for match state changes
        matchRef.Child("state").ValueChanged += OnMatchStateChanged;

        // Listen for opponent presence
        if (opponentUid != null)
        {
            var db = FirebaseDBManager.Instance;
            db.GetRef("presence").Child(opponentUid).ValueChanged += OnOpponentPresenceChanged;
        }
    }

    void StopListening()
    {
        if (!listening) return;
        listening = false;

        if (matchRef != null)
            matchRef.Child("state").ValueChanged -= OnMatchStateChanged;

        if (opponentUid != null)
        {
            var db = FirebaseDBManager.Instance;
            if (db != null && db.IsInitialized)
                db.GetRef("presence").Child(opponentUid).ValueChanged -= OnOpponentPresenceChanged;
        }
    }

    void ListenForEquation(int round)
    {
        string eqPath = "rounds/" + round + "/equation/generated";
        matchRef.Child(eqPath).ValueChanged += OnEquationGenerated;
    }

    void OnEquationGenerated(object sender, ValueChangedEventArgs e)
    {
        if (e.Snapshot == null || !e.Snapshot.Exists) return;
        bool generated = e.Snapshot.Value.ToString() == "True" || e.Snapshot.Value.ToString() == "true";
        if (!generated) return;

        // Remove this listener (one-shot)
        string eqPath = "rounds/" + currentRound + "/equation/generated";
        matchRef.Child(eqPath).ValueChanged -= OnEquationGenerated;

        // Read full equation data
        matchRef.Child("rounds/" + currentRound + "/equation").GetValueAsync().ContinueWith(t =>
        {
            UnityMainThreadDispatcher.Enqueue(() =>
            {
                if (t.IsFaulted || t.Result == null) return;

                var snap = t.Result;
                bool isMinus = snap.Child("isMinus").Value?.ToString() == "True"
                            || snap.Child("isMinus").Value?.ToString() == "true";

                // Read solution array
                var solSnap = snap.Child("solution");
                var solution = new List<int>();
                foreach (var child in solSnap.Children)
                {
                    int val = 0;
                    int.TryParse(child.Value.ToString(), out val);
                    solution.Add(val);
                }

                // Initialize local game with this equation
                GameManager.Instance.InitializeFromRemote(solution.ToArray(), isMinus, matchMode);

                // Set match state to playing
                if (isHost)
                    matchRef.Child("state").SetValueAsync("playing");

                roundActive = true;
                OnEquationReady?.Invoke();

                // The round is a race with no time limit, so the host watches
                // from the start rather than only after answering itself.
                if (isHost)
                    StartCoroutine(WatchForFirstCorrect(currentRound));
            });
        });
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Answer Submission
    // ═══════════════════════════════════════════════════════════════════

    void OnLocalPlayerSolved()
    {
        if (!roundActive || matchRef == null) return;
        roundActive = false;

        // Write answer with server timestamp
        var answerData = new Dictionary<string, object>
        {
            ["correct"] = true,
            ["timestamp"] = ServerValue.Timestamp
        };

        string ansPath = "rounds/" + currentRound + "/answers/" + myUid;
        matchRef.Child(ansPath).SetValueAsync(answerData).ContinueWith(t =>
        {
            UnityMainThreadDispatcher.Enqueue(() =>
            {
                if (t.IsFaulted)
                {
                    OnError?.Invoke("Failed to submit answer");
                    return;
                }

                // The host is already watching; nothing more to do here.
            });
        });
    }

    /// <summary>
    /// Called by TimerManager when time runs out in arcade mode.
    /// </summary>
    public void OnLocalPlayerTimeout()
    {
        if (!roundActive || matchRef == null) return;
        roundActive = false;

        var answerData = new Dictionary<string, object>
        {
            ["correct"] = false,
            ["timestamp"] = ServerValue.Timestamp
        };

        string ansPath = "rounds/" + currentRound + "/answers/" + myUid;
        matchRef.Child(ansPath).SetValueAsync(answerData);
    }

    /// <summary>
    /// Host-side round arbiter. Polls the answers node and settles the round
    /// the moment either player is correct — there is no clock to wait for.
    /// </summary>
    IEnumerator WatchForFirstCorrect(int round)
    {
        while (isHost && matchRef != null && round == currentRound && !matchEnded)
        {
            var task = matchRef.Child("rounds/" + round + "/answers").GetValueAsync();
            yield return new WaitUntil(() => task.IsCompleted);

            if (!task.IsFaulted && task.Result != null && task.Result.Exists)
            {
                foreach (var child in task.Result.Children)
                {
                    string v = child.Child("correct").Value?.ToString() ?? "";
                    if (v != "True" && v != "true") continue;

                    DetermineRoundWinner(task.Result);
                    yield break;
                }
            }

            yield return new WaitForSeconds(0.4f);
        }
    }

    void DetermineRoundWinner(DataSnapshot answersSnap)
    {
        if (!isHost) return;

        string winnerUid = null;

        bool myCorrect = false;
        bool oppCorrect = false;
        long myTime = long.MaxValue;
        long oppTime = long.MaxValue;

        var myAns = answersSnap?.Child(myUid);
        var oppAns = answersSnap?.Child(opponentUid);

        if (myAns != null && myAns.Exists)
        {
            myCorrect = myAns.Child("correct").Value?.ToString() == "True"
                      || myAns.Child("correct").Value?.ToString() == "true";
            if (myAns.Child("timestamp").Value != null)
                long.TryParse(myAns.Child("timestamp").Value.ToString(), out myTime);
        }

        if (oppAns != null && oppAns.Exists)
        {
            oppCorrect = oppAns.Child("correct").Value?.ToString() == "True"
                       || oppAns.Child("correct").Value?.ToString() == "true";
            if (oppAns.Child("timestamp").Value != null)
                long.TryParse(oppAns.Child("timestamp").Value.ToString(), out oppTime);
        }

        // Determine winner
        if (myCorrect && !oppCorrect)
            winnerUid = myUid;
        else if (!myCorrect && oppCorrect)
            winnerUid = opponentUid;
        else if (myCorrect && oppCorrect)
            winnerUid = myTime <= oppTime ? myUid : opponentUid; // earlier timestamp wins
        // else both wrong/timeout — no winner, redo round

        if (winnerUid != null)
        {
            // Update scores
            if (winnerUid == myUid) myScore++;
            else opponentScore++;

            var updates = new Dictionary<string, object>
            {
                ["rounds/" + currentRound + "/winner"] = winnerUid,
                ["scores/" + myUid] = myScore,
                ["scores/" + opponentUid] = opponentScore
            };

            // Check if match is over
            if (myScore >= firstTo || opponentScore >= firstTo)
            {
                updates["state"] = "matchEnd";
                updates["winner"] = myScore >= firstTo ? myUid : opponentUid;
            }
            else
            {
                updates["state"] = "roundEnd";
                updates["currentRound"] = currentRound + 1;
            }

            matchRef.UpdateChildrenAsync(updates);
        }
        else
        {
            // Draw — replay same round with new equation
            GenerateAndPublishEquation();
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  State Change Handlers
    // ═══════════════════════════════════════════════════════════════════

    void OnMatchStateChanged(object sender, ValueChangedEventArgs e)
    {
        if (e.Snapshot == null || !e.Snapshot.Exists) return;
        string state = e.Snapshot.Value.ToString();

        UnityMainThreadDispatcher.Enqueue(() =>
        {
            switch (state)
            {
                case "roundEnd":
                    HandleRoundEnd();
                    break;
                case "matchEnd":
                    HandleMatchEnd();
                    break;
                case "abandoned":
                    HandleAbandoned();
                    break;
            }
        });
    }

    void HandleRoundEnd()
    {
        // Read round winner and scores
        matchRef.Child("rounds/" + currentRound + "/winner").GetValueAsync().ContinueWith(t =>
        {
            UnityMainThreadDispatcher.Enqueue(() =>
            {
                string winner = t.Result?.Value?.ToString() ?? "";
                bool iWon = (winner == myUid);

                // Update local scores
                matchRef.Child("scores").GetValueAsync().ContinueWith(st =>
                {
                    UnityMainThreadDispatcher.Enqueue(() =>
                    {
                        if (st.Result != null)
                        {
                            if (st.Result.Child(myUid).Value != null)
                                int.TryParse(st.Result.Child(myUid).Value.ToString(), out myScore);
                            if (st.Result.Child(opponentUid).Value != null)
                                int.TryParse(st.Result.Child(opponentUid).Value.ToString(), out opponentScore);
                        }

                        OnScoreUpdated?.Invoke(myScore, opponentScore);
                        OnRoundResult?.Invoke(currentRound, iWon);

                        // Read new round number
                        matchRef.Child("currentRound").GetValueAsync().ContinueWith(rt =>
                        {
                            UnityMainThreadDispatcher.Enqueue(() =>
                            {
                                if (rt.Result?.Value != null)
                                    int.TryParse(rt.Result.Value.ToString(), out currentRound);

                                // After brief delay, host generates next equation
                                if (isHost)
                                    StartCoroutine(DelayedNextRound(3.2f));
                                else
                                    ListenForEquation(currentRound);
                            });
                        });
                    });
                });
            });
        });
    }

    IEnumerator DelayedNextRound(float delay)
    {
        yield return new WaitForSeconds(delay);
        ListenForEquation(currentRound);
        GenerateAndPublishEquation();
    }

    void HandleMatchEnd()
    {
        matchEnded = true;

        matchRef.GetValueAsync().ContinueWith(t =>
        {
            UnityMainThreadDispatcher.Enqueue(() =>
            {
                if (t.Result != null)
                {
                    string winner = t.Result.Child("winner").Value?.ToString() ?? "";
                    if (t.Result.Child("scores/" + myUid).Value != null)
                        int.TryParse(t.Result.Child("scores/" + myUid).Value.ToString(), out myScore);
                    if (t.Result.Child("scores/" + opponentUid).Value != null)
                        int.TryParse(t.Result.Child("scores/" + opponentUid).Value.ToString(), out opponentScore);

                    OnScoreUpdated?.Invoke(myScore, opponentScore);
                    OnMatchResult?.Invoke(winner == myUid);
                }

                // The match node stays alive so either side can offer a rematch
                StartRematchListening();

                Messenger.Broadcast(Message.ArcadeMatchEnded);
            });
        });
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Rematch
    //
    //  Both players write rematch/{uid} on the finished match. Once both
    //  say yes, the original host creates the next match and publishes its
    //  id under nextMatchId, which both sides are watching.
    // ═══════════════════════════════════════════════════════════════════

    void StartRematchListening()
    {
        if (rematchListening || matchRef == null) return;
        rematchListening = true;

        matchRef.Child("rematch").ValueChanged += OnRematchFlagsChanged;
        matchRef.Child("nextMatchId").ValueChanged += OnNextMatchIdChanged;
    }

    void StopRematchListening()
    {
        if (!rematchListening || matchRef == null) return;
        rematchListening = false;

        matchRef.Child("rematch").ValueChanged -= OnRematchFlagsChanged;
        matchRef.Child("nextMatchId").ValueChanged -= OnNextMatchIdChanged;

        if (rematchTimeoutCo != null) { StopCoroutine(rematchTimeoutCo); rematchTimeoutCo = null; }
    }

    /// <summary>Player pressed REMATCH.</summary>
    public void RequestRematch()
    {
        if (matchRef == null || !matchEnded || iWantRematch) return;

        iWantRematch = true;
        StartRematchListening();
        matchRef.Child("rematch").Child(myUid).SetValueAsync(true);

        if (rematchTimeoutCo != null) StopCoroutine(rematchTimeoutCo);
        rematchTimeoutCo = StartCoroutine(RematchTimeout(25f));
    }

    /// <summary>Player backed out of the rematch (or left the result screen).</summary>
    public void DeclineRematch()
    {
        if (matchRef == null || !matchEnded) return;

        iWantRematch = false;
        matchRef.Child("rematch").Child(myUid).SetValueAsync(false);
        StopRematchListening();
    }

    IEnumerator RematchTimeout(float seconds)
    {
        yield return new WaitForSeconds(seconds);

        if (matchEnded && iWantRematch)
        {
            StopRematchListening();
            OnRematchTimedOut?.Invoke();
        }
    }

    void OnRematchFlagsChanged(object sender, ValueChangedEventArgs e)
    {
        if (e.DatabaseError != null || e.Snapshot == null || opponentUid == null) return;

        var oppNode = e.Snapshot.Child(opponentUid);
        if (oppNode == null || !oppNode.Exists) return;

        string val = oppNode.Value?.ToString() ?? "";
        bool oppWants    = val == "True" || val == "true";
        bool oppDeclined = val == "False" || val == "false";

        UnityMainThreadDispatcher.Enqueue(() =>
        {
            if (oppDeclined)
            {
                StopRematchListening();
                OnRematchDeclined?.Invoke();
                return;
            }

            if (!oppWants) return;

            if (!iWantRematch)
            {
                OnRematchRequestedByOpponent?.Invoke();
                return;
            }

            // Both agreed — the original host builds the next match
            if (isHost) StartCoroutine(CreateNextMatch());
        });
    }

    IEnumerator CreateNextMatch()
    {
        if (nextMatchCreated) yield break;
        nextMatchCreated = true;

        var lobby = LobbyManager.Instance;
        if (lobby == null) yield break;

        string newId = lobby.CreateMatch(opponentUid, opponentName, matchMode, firstTo, false);

        // CreateMatch writes asynchronously — wait until the node is really there
        // before pointing the opponent at it, or their JoinMatch reads nothing.
        var db = FirebaseDBManager.Instance;
        float waited = 0f;
        while (waited < 10f)
        {
            var check = db.GetRef("matches").Child(newId).Child("hostUid").GetValueAsync();
            yield return new WaitUntil(() => check.IsCompleted);

            if (check.Result != null && check.Result.Exists) break;

            yield return new WaitForSeconds(0.3f);
            waited += 0.3f;
        }

        matchRef.Child("nextMatchId").SetValueAsync(newId);
    }

    void OnNextMatchIdChanged(object sender, ValueChangedEventArgs e)
    {
        if (e.DatabaseError != null || e.Snapshot == null || !e.Snapshot.Exists) return;

        string newId = e.Snapshot.Value?.ToString();
        if (string.IsNullOrEmpty(newId)) return;

        UnityMainThreadDispatcher.Enqueue(() =>
        {
            StopRematchListening();
            OnRematchStarting?.Invoke();

            Cleanup();
            JoinMatch(newId);
        });
    }

    void HandleAbandoned()
    {
        matchEnded = true;

        // If I didn't abandon, I win
        matchRef.Child("winner").GetValueAsync().ContinueWith(t =>
        {
            UnityMainThreadDispatcher.Enqueue(() =>
            {
                string winner = t.Result?.Value?.ToString() ?? "";
                if (!string.IsNullOrEmpty(winner))
                    OnMatchResult?.Invoke(winner == myUid);
                else
                    OnOpponentDisconnected?.Invoke();

                Messenger.Broadcast(Message.ArcadeMatchEnded);
            });
        });
    }

    void OnOpponentPresenceChanged(object sender, ValueChangedEventArgs e)
    {
        // Opponent went offline
        if (e.Snapshot == null || !e.Snapshot.Exists)
        {
            UnityMainThreadDispatcher.Enqueue(() =>
            {
                StartCoroutine(DisconnectGracePeriod());
            });
        }
    }

    IEnumerator DisconnectGracePeriod()
    {
        yield return new WaitForSeconds(15f);

        // Check if opponent is still offline
        var db = FirebaseDBManager.Instance;
        if (db == null || opponentUid == null) yield break;

        // The match is already decided — a late disconnect must not rewrite the winner
        if (matchEnded) yield break;

        var task = db.GetRef("presence").Child(opponentUid).GetValueAsync();
        yield return new WaitUntil(() => task.IsCompleted);

        if (task.Result == null || !task.Result.Exists)
        {
            // Opponent didn't reconnect — I win
            if (matchRef != null)
            {
                matchRef.Child("state").SetValueAsync("abandoned");
                matchRef.Child("winner").SetValueAsync(myUid);
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Leave / Cleanup
    // ═══════════════════════════════════════════════════════════════════

    public void LeaveMatch()
    {
        // Only forfeit a match that is still running — never rewrite a finished result
        if (matchRef != null && !matchEnded)
        {
            matchRef.Child("state").SetValueAsync("abandoned");
            matchRef.Child("winner").SetValueAsync(opponentUid ?? "");
        }
        else if (matchRef != null && matchEnded)
        {
            DeclineRematch();
        }
        Cleanup();
    }

    public void Cleanup()
    {
        StopRematchListening();
        StopListening();

        if (matchRef != null && opponentUid != null)
        {
            matchRef.Child("players").Child(opponentUid).Child("ready")
                .ValueChanged -= OnOpponentReadyChanged;
        }

        GameManager.Instance.isArcadeMode = false;
        matchId = null;
        matchRef = null;
        opponentUid = null;
        opponentName = null;
        roundActive = false;
        waitingForOpponent = false;
        matchEnded = false;
        iWantRematch = false;
        nextMatchCreated = false;
    }

    void OnDestroy()
    {
        Cleanup();
    }
}
```

## `Assets/Scripts/BotMatchManager.cs`

```csharp
using UnityEngine;
using System;
using System.Collections;

/// <summary>
/// Runs a fully local 1v1 match against a simulated opponent.
///
/// This exists so matchmaking never dead-ends: when no human is found within
/// LobbyManager's search window, the player still gets a match instead of an
/// empty queue. The bot mirrors ArcadeMatchManager's event surface so
/// ArcadeGUIManager can drive the same panels for both kinds of match.
///
/// Nothing here touches Firebase — the whole match lives in memory.
/// </summary>
public class BotMatchManager : MonoBehaviour
{
    public static BotMatchManager Instance { get; private set; }

    // ── Events (mirror ArcadeMatchManager) ──────────────────────────────
    public event Action<int, int> OnScoreUpdated;   // myScore, botScore
    public event Action<int, bool> OnRoundResult;   // round#, iWon
    public event Action<bool> OnMatchResult;        // iWon
    public event Action OnEquationReady;
    public event Action<string> OnOpponentJoined;   // bot name
    public event Action<string> OnError;

    // ── Match state ─────────────────────────────────────────────────────
    bool active;
    string botName;
    int myScore, botScore;
    int currentRound;
    int firstTo;
    GameMode matchMode;
    bool roundActive;
    float botSkill;              // 0 = hopeless, 1 = near-instant
    Coroutine botSolveCo;
    Coroutine nextRoundCo;

    public bool IsInMatch => active;
    public string OpponentName => botName;
    public int MyScore => myScore;
    public int OpponentScore => botScore;
    public int CurrentRound => currentRound;
    public int FirstTo => firstTo;
    public GameMode MatchMode => matchMode;

    // ── Bot identity pool ───────────────────────────────────────────────
    // Deliberately in the same shape as real guest names ("Player_48213")
    // plus ordinary handles, so a bot opponent reads like any other player.
    static readonly string[] HANDLES =
    {
        "Alex", "Maya", "Kai", "Nova", "Rex", "Luna", "Zed", "Iris",
        "Milo", "Vera", "Otto", "Nyx", "Finn", "Sage", "Jax", "Wren",
        "Dario", "Elis", "Cato", "Runa", "Pip", "Tova", "Bram", "Ivy"
    };

    static readonly string[] SUFFIXES =
    {
        "", "", "", "_x", "99", "07", "_pro", "21", "_hd", "42", "_", "88"
    };

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(this); return; }

        Messenger.AddListener(Message.ArcadeRoundWon, OnLocalPlayerSolved);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Start
    // ═══════════════════════════════════════════════════════════════════

    public void StartBotMatch(GameMode mode, int firstToRounds)
    {
        if (active) return;

        // Random mode resolves to a concrete difficulty, same as a human match would
        matchMode = mode == GameMode.Random
            ? (GameMode)UnityEngine.Random.Range(0, 3)
            : mode;

        firstTo = firstToRounds;
        myScore = 0;
        botScore = 0;
        currentRound = 1;
        active = true;
        roundActive = false;
        botName = GenerateBotName();
        botSkill = RollBotSkill();

        GameManager.Instance.SetMode(matchMode);
        GameManager.Instance.isArcadeMode = true;

        OnOpponentJoined?.Invoke(botName);

        // Short pause so the "found an opponent" beat is visible
        nextRoundCo = StartCoroutine(BeginRoundAfter(1.2f));
    }

    static string GenerateBotName()
    {
        // Half the bots use the guest-style name real anonymous players get
        if (UnityEngine.Random.Range(0, 2) == 0)
            return "Player_" + UnityEngine.Random.Range(10000, 99999);

        return HANDLES[UnityEngine.Random.Range(0, HANDLES.Length)]
             + SUFFIXES[UnityEngine.Random.Range(0, SUFFIXES.Length)];
    }

    /// <summary>
    /// Bot strength tracks the player's rating so matches stay close.
    /// A per-match wobble keeps consecutive bots from feeling identical.
    /// </summary>
    float RollBotSkill()
    {
        int elo = PlayerStatsManager.Instance != null
            ? PlayerStatsManager.Instance.Elo
            : PlayerStatsManager.START_ELO;

        float baseSkill = Mathf.InverseLerp(800f, 1800f, elo);
        float wobble = UnityEngine.Random.Range(-0.18f, 0.18f);
        return Mathf.Clamp(baseSkill + wobble, 0.05f, 0.95f);
    }

    /// <summary>Roughly how long this difficulty takes a competent player.</summary>
    public static float NominalFor(GameMode mode)
    {
        switch (mode)
        {
            case GameMode.Easy:   return 30f;
            case GameMode.Medium: return 45f;
            case GameMode.Hard:   return 60f;
            default:              return 30f;
        }
    }

    /// <summary>
    /// How long the bot "thinks" this round. Rounds have no time limit any
    /// more, so the bot always answers eventually — otherwise a round the
    /// player cannot solve would never end. Beating it means being faster.
    /// </summary>
    float RollBotSolveTime()
    {
        float nominal = NominalFor(matchMode);

        float fastest = nominal * 0.30f;
        float slowest = nominal * 1.15f;

        float t = Mathf.Lerp(slowest, fastest, botSkill);
        return t * UnityEngine.Random.Range(0.8f, 1.2f);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Round flow
    // ═══════════════════════════════════════════════════════════════════

    IEnumerator BeginRoundAfter(float delay)
    {
        yield return new WaitForSeconds(delay);
        BeginRound();
    }

    void BeginRound()
    {
        if (!active) return;

        var gm = GameManager.Instance;
        int[] solution = null;

        for (int attempt = 0; attempt < 200; attempt++)
        {
            if (matchMode == GameMode.Medium)      solution = gm.GenerateCandidate3D();
            else if (matchMode == GameMode.Hard)   solution = gm.GenerateCandidateHard();
            else                                   solution = gm.GenerateCandidate();

            if (solution != null) break;
        }

        if (solution == null)
        {
            OnError?.Invoke("Failed to generate equation");
            return;
        }

        bool isMinus = matchMode != GameMode.Hard && UnityEngine.Random.Range(0, 2) == 1;

        gm.InitializeFromRemote(solution, isMinus, matchMode);

        roundActive = true;
        OnEquationReady?.Invoke();

        botSolveCo = StartCoroutine(BotSolvesAfter(RollBotSolveTime()));
    }

    IEnumerator BotSolvesAfter(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (!active || !roundActive) yield break;

        EndRound(false);
    }

    /// <summary>Player answered correctly (Message.ArcadeRoundWon).</summary>
    void OnLocalPlayerSolved()
    {
        if (!active || !roundActive) return;
        EndRound(true);
    }

    /// <summary>
    /// Legacy hook from the countdown timer. 1v1 rounds no longer expire, so
    /// this is a no-op kept only so a stale call cannot hand the bot a point.
    /// </summary>
    public void OnLocalPlayerTimeout() { }

    void EndRound(bool iWon)
    {
        roundActive = false;

        if (botSolveCo != null) { StopCoroutine(botSolveCo); botSolveCo = null; }

        var timer = FindObjectOfType<TimerManager>();
        if (timer != null) timer.StopArcadeTimer();

        if (iWon) myScore++;
        else      botScore++;

        OnScoreUpdated?.Invoke(myScore, botScore);
        OnRoundResult?.Invoke(currentRound, iWon);

        if (myScore >= firstTo || botScore >= firstTo)
        {
            nextRoundCo = StartCoroutine(FinishMatchAfter(1.6f, myScore >= firstTo));
            return;
        }

        currentRound++;
        nextRoundCo = StartCoroutine(BeginRoundAfter(3.2f));
    }

    IEnumerator FinishMatchAfter(float delay, bool iWon)
    {
        yield return new WaitForSeconds(delay);

        active = false;
        GameManager.Instance.isArcadeMode = false;

        OnMatchResult?.Invoke(iWon);
        Messenger.Broadcast(Message.ArcadeMatchEnded);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Rematch / cleanup
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>Bots always accept — a rematch starts immediately.</summary>
    public void Rematch()
    {
        GameMode mode = matchMode;
        int ft = firstTo;
        Cleanup();
        StartBotMatch(mode, ft);
    }

    public void Cleanup()
    {
        if (botSolveCo != null)  { StopCoroutine(botSolveCo);  botSolveCo = null; }
        if (nextRoundCo != null) { StopCoroutine(nextRoundCo); nextRoundCo = null; }

        var timer = FindObjectOfType<TimerManager>();
        if (timer != null) timer.StopArcadeTimer();

        if (active && GameManager.Instance != null)
            GameManager.Instance.isArcadeMode = false;

        active = false;
        roundActive = false;
        myScore = 0;
        botScore = 0;
        currentRound = 1;
    }

    void OnDestroy()
    {
        Cleanup();
    }
}
```

## `Assets/Scripts/ArcadeModels.cs`

```csharp
using System;

[Serializable]
public class OnlineUser
{
    public string uid;
    public string displayName;
    public bool isFriend;
    public bool isOnline;
}

[Serializable]
public class InviteData
{
    public string inviteId;
    public string fromUid;
    public string fromName;
    public GameMode mode;
    public int firstTo;
    public string status; // "pending", "accepted", "declined"
}

[Serializable]
public class MatchSettings
{
    public GameMode mode;
    public int firstTo;
}

[Serializable]
public class MatchData
{
    public string matchId;
    public string hostUid;
    public string opponentUid;
    public string opponentName;
    public MatchSettings settings;
    public string state; // "waiting", "playing", "roundEnd", "matchEnd", "abandoned"
}
```

## `Assets/Scripts/GoogleSignInBridge.cs`

```csharp
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
```

## `Assets/Scripts/FacebookSignInBridge.cs`

```csharp
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
```

## `Assets/Scripts/UnityMainThreadDispatcher.cs`

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

public class UnityMainThreadDispatcher : MonoBehaviour
{
    static readonly Queue<Action> queue = new Queue<Action>();
    static UnityMainThreadDispatcher instance;

    public static void Enqueue(Action action)
    {
        lock (queue) queue.Enqueue(action);
    }

    void Awake()
    {
        if (instance == null) { instance = this; DontDestroyOnLoad(gameObject); }
        else Destroy(gameObject);
    }

    void Update()
    {
        while (queue.Count > 0)
        {
            Action action;
            lock (queue) action = queue.Dequeue();
            action?.Invoke();
        }
    }
}
```

## `Assets/Scripts/HeartbeatManager.cs`

```csharp
using UnityEngine;

public class HeartbeatManager : MonoBehaviour
{
    AudioSource heartbeatSource;
    AudioClip heartbeatClip;
    float currentTimer = 60f;
    float maxTimer = 60f;
    bool isActive = false;
    float nextBeatTime = 0f;

    void Awake()
    {
        heartbeatSource = gameObject.AddComponent<AudioSource>();
        heartbeatSource.spatialBlend = 0f;  // 2D sound
        heartbeatClip = CreateHeartbeatClip();

        Messenger.AddListener<float>(Message.OnSetTimer,    t => currentTimer = t);
        Messenger.AddListener<float>(Message.OnSetTimerMax, m => maxTimer = m);
        Messenger.AddListener<string>(Message.OnEndFadeToTransparent, OnFadeTransparent);
        Messenger.AddListener(Message.GameWon,  Stop);
        Messenger.AddListener(Message.GameLost, Stop);
    }

    void OnFadeTransparent(string name)
    {
        // Arcade rounds run a stopwatch (max = 0): there is no countdown to
        // dramatise, and a zero max reads as ratio 0 = permanent 180 BPM panic.
        if (GameManager.Instance != null && GameManager.Instance.isArcadeMode)
            return;

        if (name == "fadeToTransparentBeforeGameStarts")
            StartBeating();
    }

    void StartBeating() { isActive = true; nextBeatTime = Time.time; }
    void Stop() { isActive = false; }

    void Update()
    {
        if (!isActive) return;
        if (AudioManager.Instance != null && AudioManager.Instance.IsMuted) return;
        if (Time.time < nextBeatTime) return;

        // No countdown (the arcade stopwatch broadcasts max = 0) → no beat.
        // Without this, ratio read 0 = max panic at full volume, forever —
        // e.g. after leaving a training round mid-game and entering arcade.
        if (maxTimer <= 0f) return;

        float ratio = Mathf.Clamp01(currentTimer / maxTimer);
        float bpm = 40f + Mathf.Pow(1f - ratio, 0.8f) * 140f;  // 40 BPM idle → 180 BPM panic
        float interval = 60f / bpm;
        float volume = Mathf.Lerp(0.50f, 0.85f, 1f - ratio);   // loud from start → very loud

        heartbeatSource.PlayOneShot(heartbeatClip, volume);
        nextBeatTime = Time.time + interval;
    }

    AudioClip CreateHeartbeatClip()
    {
        const int sampleRate = 44100;
        const float clipDuration = 0.5f;
        int total = (int)(sampleRate * clipDuration);
        float[] data = new float[total];

        // Lub: low thump at t=0.00s, freq=55Hz, dur=0.12s
        AddThump(data, sampleRate, 0.00f, 55f, 0.12f, 1.0f);
        // Dub: slightly higher, softer thump at t=0.15s, freq=70Hz, dur=0.08s
        AddThump(data, sampleRate, 0.15f, 70f, 0.08f, 0.55f);

        var clip = AudioClip.Create("heartbeat", total, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    void AddThump(float[] data, int sampleRate, float startSec, float freq, float duration, float amplitude)
    {
        int start = (int)(startSec * sampleRate);
        int len   = (int)(duration * sampleRate);
        for (int i = 0; i < len && start + i < data.Length; i++)
        {
            float t   = (float)i / sampleRate;
            float env = Mathf.Exp(-t / (duration * 0.35f));  // fast decay
            float s   = Mathf.Sin(2f * Mathf.PI * freq * t) * env
                      + 0.3f * Mathf.Sin(2f * Mathf.PI * freq * 2f * t) * env;  // 2nd harmonic
            data[start + i] += s * amplitude;
        }
    }
}
```

## `Assets/Scripts/UserProfile.cs`

```csharp
// UserProfile is provided by Firebase.Auth
// This file serves as a placeholder for game-specific user data extensions
// For now, we use Firebase.Auth.UserProfile directly
```
