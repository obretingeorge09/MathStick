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
