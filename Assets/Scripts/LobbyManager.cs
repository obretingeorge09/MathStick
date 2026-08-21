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
