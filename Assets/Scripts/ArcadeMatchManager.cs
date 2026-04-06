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

    DatabaseReference matchRef;
    bool listening = false;
    bool waitingForOpponent = false;

    public bool IsInMatch => matchId != null;
    public bool IsHost => isHost;
    public string OpponentName => opponentName;
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

                // If host, start checking for both answers
                if (isHost)
                    StartCoroutine(WaitForBothAnswers());
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

        if (isHost)
            StartCoroutine(WaitForBothAnswers());
    }

    IEnumerator WaitForBothAnswers()
    {
        // Wait for both answers to exist (with timeout)
        float timeout = 120f;
        float elapsed = 0f;

        while (elapsed < timeout)
        {
            var task = matchRef.Child("rounds/" + currentRound + "/answers").GetValueAsync();
            yield return new WaitUntil(() => task.IsCompleted);

            if (task.Result != null && task.Result.ChildrenCount >= 2)
            {
                DetermineRoundWinner(task.Result);
                yield break;
            }

            yield return new WaitForSeconds(0.5f);
            elapsed += 0.5f;
        }

        // Timeout — whoever answered correctly wins, otherwise draw
        var finalTask = matchRef.Child("rounds/" + currentRound + "/answers").GetValueAsync();
        yield return new WaitUntil(() => finalTask.IsCompleted);
        DetermineRoundWinner(finalTask.Result);
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
                                    StartCoroutine(DelayedNextRound(1.5f));
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

                Messenger.Broadcast(Message.ArcadeMatchEnded);
            });
        });
    }

    void HandleAbandoned()
    {
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
        if (matchRef != null)
        {
            matchRef.Child("state").SetValueAsync("abandoned");
            matchRef.Child("winner").SetValueAsync(opponentUid ?? "");
        }
        Cleanup();
    }

    public void Cleanup()
    {
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
    }

    void OnDestroy()
    {
        Cleanup();
    }
}
