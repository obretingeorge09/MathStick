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
    public string DisplayName => currentUser?.DisplayName ?? "Player";

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
                    onReady?.Invoke();
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
        if (auth != null)
            currentUser = auth.CurrentUser;
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

    public void Logout()
    {
        if (auth != null)
            auth.SignOut();
        currentUser = null;
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
