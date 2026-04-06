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

    List<OnlineUser> onlineUsers = new List<OnlineUser>();
    bool isListeningUsers = false;
    bool isSearchingRandom = false;
    Coroutine matchmakingCo;

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

            users.Add(new OnlineUser { uid = uid, displayName = name });
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

        while (isSearchingRandom)
        {
            // Add jitter to reduce collisions
            yield return new WaitForSeconds(2f + UnityEngine.Random.Range(0f, 0.5f));
            if (!isSearchingRandom) yield break;

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

        // Use transaction to atomically remove both from queue
        var txTask = db.GetRef("matchmaking").RunTransaction(mutableData =>
        {
            if (mutableData.Child(oppUid).Value == null)
                return TransactionResult.Abort(); // opponent already taken

            mutableData.Child(myUid).Value = null;
            mutableData.Child(oppUid).Value = null;
            return TransactionResult.Success(mutableData);
        });

        yield return new WaitUntil(() => txTask.IsCompleted);

        if (txTask.IsFaulted || txTask.IsCanceled)
        {
            // Failed — someone else claimed this opponent, keep searching
            yield break;
        }

        // Success — create match
        isSearchingRandom = false;
        CreateMatch(oppUid, oppName, mode, firstTo);
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

    string CreateMatch(string opponentUid, string opponentName, GameMode mode, int firstTo)
    {
        var db = FirebaseDBManager.Instance;
        string myUid = AuthManager.Instance.CurrentUser.UserId;
        string myName = AuthManager.Instance.DisplayName;

        var matchRef = db.GetRef("matches").Push();
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
                OnMatchFound?.Invoke(matchId);
            });
        });

        return matchId;
    }

    void OnDestroy()
    {
        StopListeningOnlineUsers();
        StopListeningInvites();
        LeaveRandomQueue();
    }
}
