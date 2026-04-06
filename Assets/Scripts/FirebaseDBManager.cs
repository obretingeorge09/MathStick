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

    public DatabaseReference Root => root;
    public bool IsInitialized => initialized;

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

            if (AuthManager.Instance != null && AuthManager.Instance.IsLoggedIn)
                SetupPresence();
        }
        catch (Exception e)
        {
            Debug.LogError("FirebaseDB init failed: " + e.Message);
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
