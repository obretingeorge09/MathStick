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
