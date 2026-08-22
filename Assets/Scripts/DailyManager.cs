using UnityEngine;
using Firebase.Database;
using System;
using System.Collections.Generic;

public enum ChallengeType
{
    PlayMatches,
    WinMatches,
    WinRounds,
    WinOnHard
}

[Serializable]
public class DailyChallenge
{
    public ChallengeType type;
    public int    target;
    public int    progress;
    public bool   claimed;
    public int    reward;

    public bool Completed => progress >= target;

    public string Description
    {
        get
        {
            switch (type)
            {
                case ChallengeType.PlayMatches: return "PLAY " + target + " MATCHES";
                case ChallengeType.WinMatches:  return "WIN " + target + (target == 1 ? " MATCH" : " MATCHES");
                case ChallengeType.WinRounds:   return "WIN " + target + " ROUNDS";
                case ChallengeType.WinOnHard:   return "WIN " + target + " ON HARD";
                default: return "";
            }
        }
    }
}

/// <summary>
/// Daily streak and the three rotating challenges.
///
/// Dates come from Firebase's server clock (see FirebaseDBManager.ServerDateKey)
/// rather than the device, so the streak cannot be farmed by rolling the phone's
/// date forward. State lives at daily/{uid}.
/// </summary>
public class DailyManager : MonoBehaviour
{
    public static DailyManager Instance { get; private set; }

    public event Action OnDailyChanged;

    public int  Streak        { get; private set; }
    public int  LongestStreak { get; private set; }
    public bool IsLoaded      { get; private set; }

    public List<DailyChallenge> Challenges { get; private set; } = new List<DailyChallenge>();

    string lastLoginDate     = "";
    string streakClaimedDate = "";
    string challengeDate     = "";

    /// <summary>Opening the app is enough — there is nothing to earn first.</summary>
    public bool CanClaimStreak => IsLoaded && streakClaimedDate != Today;

    /// <summary>
    /// Day 1 pays 50 and each consecutive day adds 10, capping at 150 on day 11.
    /// Uncapped growth would make a long streak worth more than playing.
    /// </summary>
    public int StreakReward => 50 + 10 * Mathf.Clamp(Streak - 1, 0, 10);

    /// <summary>Whether the app has already been counted for today.</summary>
    public bool LoggedInToday => lastLoginDate == Today;

    static string Today => FirebaseDBManager.Instance != null
        ? FirebaseDBManager.Instance.ServerDateKey
        : DateTime.UtcNow.ToString("yyyy-MM-dd");

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(this); return; }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Challenge generation
    // ═══════════════════════════════════════════════════════════════════

    // Deliberately modest targets — these must be finishable in one sitting,
    // otherwise they read as a chore instead of a reason to come back.
    static readonly DailyChallenge[] POOL =
    {
        new DailyChallenge { type = ChallengeType.PlayMatches, target = 3, reward = 15 },
        new DailyChallenge { type = ChallengeType.PlayMatches, target = 5, reward = 25 },
        new DailyChallenge { type = ChallengeType.WinMatches,  target = 1, reward = 15 },
        new DailyChallenge { type = ChallengeType.WinMatches,  target = 3, reward = 30 },
        new DailyChallenge { type = ChallengeType.WinRounds,   target = 5, reward = 20 },
        new DailyChallenge { type = ChallengeType.WinRounds,   target = 10, reward = 35 },
        new DailyChallenge { type = ChallengeType.WinOnHard,   target = 1, reward = 40 },
    };

    /// <summary>
    /// Stable across runs and platforms, unlike string.GetHashCode — the saved
    /// progress is keyed by slot index, so the same date must always produce
    /// the same three challenges in the same order.
    /// </summary>
    static int StableHash(string s)
    {
        int hash = 17;
        foreach (char c in s) hash = unchecked(hash * 31 + c);
        return hash;
    }

    /// <summary>
    /// Picks three distinct challenge types for the date. Seeded by the date
    /// string so every player gets the same set on the same day.
    /// </summary>
    static List<DailyChallenge> GenerateFor(string dateKey)
    {
        var rng = new System.Random(StableHash(dateKey));
        var chosen = new List<DailyChallenge>();
        var usedTypes = new HashSet<ChallengeType>();

        var order = new List<int>();
        for (int i = 0; i < POOL.Length; i++) order.Add(i);

        // Fisher-Yates on the pool order
        for (int i = order.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            int tmp = order[i]; order[i] = order[j]; order[j] = tmp;
        }

        foreach (int idx in order)
        {
            var template = POOL[idx];
            if (usedTypes.Contains(template.type)) continue;

            usedTypes.Add(template.type);
            chosen.Add(new DailyChallenge
            {
                type = template.type,
                target = template.target,
                reward = template.reward,
                progress = 0,
                claimed = false
            });

            if (chosen.Count == 3) break;
        }

        return chosen;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Load / Save
    // ═══════════════════════════════════════════════════════════════════

    public void Load()
    {
        var db = FirebaseDBManager.Instance;
        string uid = AuthManager.Instance?.CurrentUser?.UserId;
        if (db == null || !db.IsInitialized || uid == null) return;

        db.GetRef("daily").Child(uid).GetValueAsync().ContinueWith(t =>
        {
            UnityMainThreadDispatcher.Enqueue(() =>
            {
                // Same rule as stats: a failed read must not be mistaken for a
                // fresh account, or Save() below resets the streak to day one.
                if (t.IsFaulted || t.IsCanceled)
                {
                    Debug.LogWarning("Daily load failed; not advancing or saving.");
                    return;
                }

                if (t.Result != null && t.Result.Exists)
                    ReadFrom(t.Result);

                // Opening the app IS the daily action, so count it here
                AdvanceLoginStreak();
                EnsureTodaysChallenges();

                IsLoaded = true;
                OnDailyChanged?.Invoke();
                Save();
            });
        });
    }

    void ReadFrom(DataSnapshot snap)
    {
        Streak            = ReadInt(snap, "streak", 0);
        LongestStreak     = ReadInt(snap, "longestStreak", 0);
        lastLoginDate     = snap.Child("lastLoginDate").Value?.ToString() ?? "";
        streakClaimedDate = snap.Child("streakClaimedDate").Value?.ToString() ?? "";
        challengeDate     = snap.Child("challengeDate").Value?.ToString() ?? "";

        Challenges = GenerateFor(challengeDate.Length > 0 ? challengeDate : Today);

        var chSnap = snap.Child("challenges");
        if (chSnap == null || !chSnap.Exists) return;

        for (int i = 0; i < Challenges.Count; i++)
        {
            var node = chSnap.Child(i.ToString());
            if (node == null || !node.Exists) continue;

            Challenges[i].progress = ReadInt(node, "progress", 0);

            string claimed = node.Child("claimed").Value?.ToString() ?? "";
            Challenges[i].claimed = claimed == "True" || claimed == "true";
        }
    }

    static int ReadInt(DataSnapshot snap, string key, int fallback)
    {
        var child = snap.Child(key);
        if (child == null || !child.Exists || child.Value == null) return fallback;

        int v;
        return int.TryParse(child.Value.ToString(), out v) ? v : fallback;
    }

    static string Yesterday()
    {
        DateTime today = FirebaseDBManager.Instance != null
            ? FirebaseDBManager.Instance.ServerNowUtc
            : DateTime.UtcNow;

        return today.AddDays(-1).ToString("yyyy-MM-dd");
    }

    void EnsureTodaysChallenges()
    {
        if (challengeDate == Today && Challenges.Count == 3) return;

        challengeDate = Today;
        Challenges = GenerateFor(challengeDate);
    }

    void Save()
    {
        var db = FirebaseDBManager.Instance;
        string uid = AuthManager.Instance?.CurrentUser?.UserId;
        if (db == null || !db.IsInitialized || uid == null) return;

        var challenges = new Dictionary<string, object>();
        for (int i = 0; i < Challenges.Count; i++)
        {
            challenges[i.ToString()] = new Dictionary<string, object>
            {
                ["type"]     = Challenges[i].type.ToString(),
                ["target"]   = Challenges[i].target,
                ["progress"] = Challenges[i].progress,
                ["claimed"]  = Challenges[i].claimed
            };
        }

        var data = new Dictionary<string, object>
        {
            ["streak"]            = Streak,
            ["longestStreak"]     = LongestStreak,
            ["lastLoginDate"]     = lastLoginDate,
            ["streakClaimedDate"] = streakClaimedDate,
            ["challengeDate"]     = challengeDate,
            ["challenges"]        = challenges
        };

        db.GetRef("daily").Child(uid).UpdateChildrenAsync(data);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Progress reporting
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>Called once per finished match by PlayerStatsManager.</summary>
    public void ReportMatchPlayed(bool won, GameMode mode)
    {
        if (!IsLoaded) return;

        EnsureTodaysChallenges();

        foreach (var c in Challenges)
        {
            if (c.type == ChallengeType.PlayMatches) c.progress++;
            if (c.type == ChallengeType.WinMatches && won) c.progress++;
            if (c.type == ChallengeType.WinOnHard && won && mode == GameMode.Hard) c.progress++;
        }

        Save();
        OnDailyChanged?.Invoke();
    }

    public void ReportRoundWon()
    {
        if (!IsLoaded) return;

        EnsureTodaysChallenges();

        bool changed = false;
        foreach (var c in Challenges)
        {
            if (c.type != ChallengeType.WinRounds) continue;
            c.progress++;
            changed = true;
        }

        if (!changed) return;

        Save();
        OnDailyChanged?.Invoke();
    }

    /// <summary>
    /// Called once when the app reaches the menu. Yesterday continues the run;
    /// any bigger gap starts over at day 1.
    /// </summary>
    void AdvanceLoginStreak()
    {
        if (lastLoginDate == Today) return; // already counted today

        Streak = lastLoginDate == Yesterday() ? Streak + 1 : 1;
        if (Streak > LongestStreak) LongestStreak = Streak;

        lastLoginDate = Today;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Claiming
    // ═══════════════════════════════════════════════════════════════════

    public bool ClaimStreak()
    {
        if (!CanClaimStreak) return false;

        int reward = StreakReward;
        streakClaimedDate = Today;

        PlayerStatsManager.Instance?.AddXp(reward);

        Save();
        OnDailyChanged?.Invoke();
        return true;
    }

    public bool ClaimChallenge(int index)
    {
        if (index < 0 || index >= Challenges.Count) return false;

        var c = Challenges[index];
        if (!c.Completed || c.claimed) return false;

        c.claimed = true;
        PlayerStatsManager.Instance?.AddXp(c.reward);

        Save();
        OnDailyChanged?.Invoke();
        return true;
    }

    /// <summary>Total XP sitting unclaimed right now — drives the menu badge.</summary>
    public int PendingRewards
    {
        get
        {
            if (!IsLoaded) return 0;

            int total = CanClaimStreak ? StreakReward : 0;
            foreach (var c in Challenges)
                if (c.Completed && !c.claimed) total += c.reward;

            return total;
        }
    }
}
