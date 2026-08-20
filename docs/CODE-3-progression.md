# MathStick — Progression — rating, leaderboards, daily, economy, ads

> **Generated file — do not edit.** Regenerated from the sources listed below.
> The code in `Assets/` is the only source of truth; this exists so the whole
> project can be handed to a tool that reads documents rather than a repo.

> ELO and ranks, monthly leaderboards, the login streak and challenges, coins and the ad seam.

---

## `Assets/Scripts/PlayerStatsManager.cs`

```csharp
using UnityEngine;
using Firebase.Database;
using System;
using System.Collections.Generic;

/// <summary>
/// Owns the player's competitive record: rating, win/loss, streaks and coins.
///
/// Stored at stats/{uid} in Realtime Database and mirrored into the
/// leaderboards whenever it changes. Loaded once after login and kept in
/// memory so the UI never has to wait on a round trip.
/// </summary>
public class PlayerStatsManager : MonoBehaviour
{
    public static PlayerStatsManager Instance { get; private set; }

    public const int START_ELO = 1000;

    /// <summary>
    /// Opening balance, paid when the player FINISHES their first match rather
    /// than when the account appears.
    ///
    /// Granting it on creation made a throwaway anonymous account worth 100
    /// coins on sight — more than a rewarded ad pays — so reinstalling beat
    /// watching an ad. Nobody is stranded meanwhile: the new-player shield
    /// makes the first 25 ranked matches free, and the day-one login bonus
    /// lands regardless.
    /// </summary>
    public const int START_COINS = 100;

    // K-factor: how much a single match can move the rating.
    // Bot matches move it half as far so the ladder stays meaningful.
    const int K_HUMAN = 32;
    const int K_BOT   = 16;

    /// <summary>
    /// Difficulty weighting for the rating swing. Deliberately SYMMETRIC: a
    /// Hard win is worth more and a Hard loss costs more by the same factor.
    ///
    /// A win-only bonus would be the obvious reading of "harder should pay
    /// better", but it inflates the ladder — everyone farms the mode with the
    /// best ratio and ratings drift up until they mean nothing. Scaling the
    /// swing keeps the rating honest (it still measures who beats whom) and
    /// makes difficulty a real bet rather than free points.
    ///
    /// The actual reward for difficulty is paid in coins, below, where
    /// inflation does not corrupt anything.
    /// </summary>
    static float RankWeight(GameMode mode)
    {
        switch (mode)
        {
            case GameMode.Easy:   return 0.75f;
            case GameMode.Hard:   return 1.25f;
            default:              return 1.0f;   // Medium, and Random once resolved
        }
    }

    /// <summary>Coins paid for winning a match, by difficulty.</summary>
    public static int WinCoins(GameMode mode)
    {
        switch (mode)
        {
            case GameMode.Easy:   return 10;
            case GameMode.Hard:   return 30;
            default:              return 18;
        }
    }

    /// <summary>
    /// Coins taken to enter a ranked match.
    ///
    /// The break-even win rate is exactly fee/payout. The first version used
    /// 5/8/12 against payouts of 10/18/30, which put Easy's break-even at
    /// 5/10 = 50% — precisely the win rate that ELO matchmaking and the
    /// rating-tracking bot drive every player toward. Expected drift there is
    /// exactly zero, and a zero-drift random walk against an absorbing barrier
    /// at zero reaches zero with probability 1. Over 100 Easy matches, 46% of
    /// players ended below where they started. Easy is also where weaker
    /// players live, so the old ladder punished exactly the people who most
    /// needed to keep playing.
    ///
    /// 3/5/9 puts every mode near a 30% break-even, so an even win rate drifts
    /// upward everywhere and dropping to Easy after a slump is a real recovery.
    ///
    /// TRAINING never charges. That is the guarantee that nobody can be locked
    /// out of the game itself, only out of ranked 1v1.
    /// </summary>
    public static int EntryFee(GameMode mode)
    {
        switch (mode)
        {
            case GameMode.Easy:   return 3;
            case GameMode.Hard:   return 9;
            default:              return 5;
        }
    }

    /// <summary>Ranked entry is free until the player has this many matches.</summary>
    public const int SHIELD_MATCHES = 25;

    /// <summary>Free ranked entries granted per day once the player is broke.</summary>
    public const int FREE_ENTRIES_PER_DAY = 3;

    int    freeEntriesUsed;
    string freeEntriesDate = "";

    /// <summary>A newcomer cannot be priced out before they know the game.</summary>
    public bool HasNewPlayerShield => Matches < SHIELD_MATCHES;

    public int FreeEntriesRemaining
    {
        get { SyncFreeEntryDay(); return Mathf.Max(0, FREE_ENTRIES_PER_DAY - freeEntriesUsed); }
    }

    static string TodayKey => FirebaseDBManager.Instance != null
        ? FirebaseDBManager.Instance.ServerDateKey
        : DateTime.UtcNow.ToString("yyyy-MM-dd");

    void SyncFreeEntryDay()
    {
        string today = TodayKey;
        if (freeEntriesDate == today) return;

        freeEntriesDate = today;
        freeEntriesUsed = 0;
        PlayerPrefs.SetString("FreeEntryDate", freeEntriesDate);
        PlayerPrefs.SetInt("FreeEntryUsed", 0);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Spends one of the day's free entries. This is the floor that makes the
    /// anti-lockout guarantee true for ranked play too, not just for training.
    /// </summary>
    public bool TryUseFreeEntry()
    {
        if (FreeEntriesRemaining <= 0) return false;

        freeEntriesUsed++;
        PlayerPrefs.SetInt("FreeEntryUsed", freeEntriesUsed);
        PlayerPrefs.SetString("FreeEntryDate", freeEntriesDate);
        PlayerPrefs.Save();

        OnStatsChanged?.Invoke();
        return true;
    }

    public bool CanAfford(GameMode mode) => Coins >= EntryFee(mode);

    /// <summary>
    /// Deducts the entry fee. Returns false and changes nothing when the player
    /// cannot pay, so the caller can offer the top-up flow instead.
    /// </summary>
    public bool TrySpendCoins(int amount)
    {
        if (amount <= 0) return true;
        if (Coins < amount) return false;

        Coins -= amount;
        Save();
        OnStatsChanged?.Invoke();
        return true;
    }

    public event Action OnStatsChanged;

    // ── Live values ─────────────────────────────────────────────────────
    public int    Elo           { get; private set; } = START_ELO;
    public int    Wins          { get; private set; }
    public int    Losses        { get; private set; }
    public int    Matches       { get; private set; }
    public int    CurrentStreak { get; private set; }
    public int    BestStreak    { get; private set; }
    public int    RoundsWon     { get; private set; }
    public int    RoundsLost    { get; private set; }
    public int    Coins         { get; private set; }
    public string Country       { get; private set; } = "XX";

    public bool IsLoaded { get; private set; }

    public int WinRate => Matches > 0 ? Mathf.RoundToInt(100f * Wins / Matches) : 0;

    /// <summary>Rating change applied by the most recent match, for the result screen.</summary>
    public int LastEloDelta { get; private set; }

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(this); return; }

        Country = DetectCountry();

        freeEntriesUsed = PlayerPrefs.GetInt("FreeEntryUsed", 0);
        freeEntriesDate = PlayerPrefs.GetString("FreeEntryDate", "");
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Ranks
    // ═══════════════════════════════════════════════════════════════════

    public static readonly int[] RANK_FLOORS = { 0, 1000, 1150, 1300, 1450, 1600 };
    public static readonly string[] RANK_NAMES =
        { "BRONZE", "SILVER", "GOLD", "PLATINUM", "DIAMOND", "MASTER" };
    static readonly string[] RANK_HEX =
        { "#CD7F32", "#C0C0C0", "#FFD600", "#4DD0E1", "#B388FF", "#FF1744" };

    public static int RankIndexFor(int elo)
    {
        int idx = 0;
        for (int i = 0; i < RANK_FLOORS.Length; i++)
            if (elo >= RANK_FLOORS[i]) idx = i;
        return idx;
    }

    public static string RankNameFor(int elo) => RANK_NAMES[RankIndexFor(elo)];

    public static Color RankColorFor(int elo)
    {
        Color c;
        ColorUtility.TryParseHtmlString(RANK_HEX[RankIndexFor(elo)], out c);
        return c;
    }

    /// <summary>0..1 progress toward the next rank (1 when already at the top rank).</summary>
    public static float RankProgressFor(int elo)
    {
        int idx = RankIndexFor(elo);
        if (idx >= RANK_FLOORS.Length - 1) return 1f;

        int floor = RANK_FLOORS[idx];
        int next  = RANK_FLOORS[idx + 1];
        return Mathf.Clamp01((float)(elo - floor) / (next - floor));
    }

    public string RankName    => RankNameFor(Elo);
    public Color  RankColor   => RankColorFor(Elo);
    public float  RankProgress => RankProgressFor(Elo);

    public int EloToNextRank
    {
        get
        {
            int idx = RankIndexFor(Elo);
            if (idx >= RANK_FLOORS.Length - 1) return 0;
            return RANK_FLOORS[idx + 1] - Elo;
        }
    }

    static string DetectCountry()
    {
        try
        {
            string cc = System.Globalization.RegionInfo.CurrentRegion.TwoLetterISORegionName;
            if (!string.IsNullOrEmpty(cc) && cc.Length == 2) return cc.ToUpper();
        }
        catch (Exception) { /* some Android locales have no region — fall through */ }

        return "XX";
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Load / Save
    // ═══════════════════════════════════════════════════════════════════

    public void Load()
    {
        var db = FirebaseDBManager.Instance;
        string uid = AuthManager.Instance?.CurrentUser?.UserId;
        if (db == null || !db.IsInitialized || uid == null) return;

        db.GetRef("stats").Child(uid).GetValueAsync().ContinueWith(t =>
        {
            UnityMainThreadDispatcher.Enqueue(() =>
            {
                // A failed read is NOT an empty account. Treating it as one and
                // then saving would overwrite a real record with defaults, so
                // bail out and leave the remote data untouched.
                if (t.IsFaulted || t.IsCanceled)
                {
                    Debug.LogWarning("Stats load failed; keeping local state and not saving.");
                    return;
                }

                // A new account starts empty; START_COINS is paid on the first
                // finished match instead (see ApplyResult).
                if (t.Result != null && t.Result.Exists) ReadFrom(t.Result);

                IsLoaded = true;
                OnStatsChanged?.Invoke();

                // A brand-new player needs a row before they can appear anywhere
                Save();
            });
        });
    }

    void ReadFrom(DataSnapshot snap)
    {
        Elo           = ReadInt(snap, "elo", START_ELO);
        Wins          = ReadInt(snap, "wins", 0);
        Losses        = ReadInt(snap, "losses", 0);
        Matches       = ReadInt(snap, "matches", 0);
        CurrentStreak = ReadInt(snap, "currentStreak", 0);
        BestStreak    = ReadInt(snap, "bestStreak", 0);
        RoundsWon     = ReadInt(snap, "roundsWon", 0);
        RoundsLost    = ReadInt(snap, "roundsLost", 0);
        Coins         = ReadInt(snap, "coins", 0);
    }

    static int ReadInt(DataSnapshot snap, string key, int fallback)
    {
        var child = snap.Child(key);
        if (child == null || !child.Exists || child.Value == null) return fallback;

        int v;
        return int.TryParse(child.Value.ToString(), out v) ? v : fallback;
    }

    void Save()
    {
        var db = FirebaseDBManager.Instance;
        string uid = AuthManager.Instance?.CurrentUser?.UserId;
        if (db == null || !db.IsInitialized || uid == null) return;

        string name = AuthManager.Instance.DisplayName ?? "Player";

        var data = new Dictionary<string, object>
        {
            ["displayName"]   = name,
            ["country"]       = Country,
            ["elo"]           = Elo,
            ["wins"]          = Wins,
            ["losses"]        = Losses,
            ["matches"]       = Matches,
            ["currentStreak"] = CurrentStreak,
            ["bestStreak"]    = BestStreak,
            ["roundsWon"]     = RoundsWon,
            ["roundsLost"]    = RoundsLost,
            ["coins"]         = Coins,
            ["updatedAt"]     = ServerValue.Timestamp
        };

        db.GetRef("stats").Child(uid).UpdateChildrenAsync(data);

        LeaderboardManager.Instance?.PublishMyEntry(name, Elo, Wins, Country);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Recording results
    // ═══════════════════════════════════════════════════════════════════

    public void RecordRound(bool won)
    {
        if (won) RoundsWon++; else RoundsLost++;

        if (won) DailyManager.Instance?.ReportRoundWon();

        OnStatsChanged?.Invoke();
        // Not saved here — the match result save covers it, avoiding a write per round.
    }

    /// <summary>
    /// Applies a finished match. Human opponents move the rating by their own
    /// rating difference; bots are treated as an even-rated opponent at half K.
    /// </summary>
    public void RecordMatchResult(bool won, string opponentUid, bool vsBot, GameMode mode)
    {
        if (vsBot || string.IsNullOrEmpty(opponentUid))
        {
            ApplyResult(won, Elo, K_BOT, mode);
            return;
        }

        var db = FirebaseDBManager.Instance;
        if (db == null || !db.IsInitialized)
        {
            ApplyResult(won, Elo, K_HUMAN, mode);
            return;
        }

        db.GetRef("stats").Child(opponentUid).Child("elo").GetValueAsync().ContinueWith(t =>
        {
            UnityMainThreadDispatcher.Enqueue(() =>
            {
                int oppElo = START_ELO;
                if (!t.IsFaulted && t.Result != null && t.Result.Exists && t.Result.Value != null)
                    int.TryParse(t.Result.Value.ToString(), out oppElo);

                ApplyResult(won, oppElo, K_HUMAN, mode);
            });
        });
    }

    void ApplyResult(bool won, int opponentElo, int k, GameMode mode)
    {
        float expected = 1f / (1f + Mathf.Pow(10f, (opponentElo - Elo) / 400f));
        float score = won ? 1f : 0f;

        int delta = Mathf.RoundToInt(k * RankWeight(mode) * (score - expected));

        // A win must always be worth something, a loss must always cost something
        if (won  && delta < 1)  delta = 1;
        if (!won && delta > -1) delta = -1;

        LastEloDelta = delta;
        Elo = Mathf.Max(0, Elo + delta);

        Matches++;
        if (won)
        {
            Wins++;
            CurrentStreak++;
            if (CurrentStreak > BestStreak) BestStreak = CurrentStreak;
        }
        else
        {
            Losses++;
            CurrentStreak = 0;
        }

        // Coins are where difficulty actually pays: no ladder to distort, and
        // the player can see the number before choosing the mode.
        if (won) Coins += WinCoins(mode);

        // Matches is monotonic and server-validated, so this fires exactly once
        // in the lifetime of an account.
        if (Matches == 1) Coins += START_COINS;

        Save();
        OnStatsChanged?.Invoke();

        DailyManager.Instance?.ReportMatchPlayed(won, mode);
    }

    public void AddCoins(int amount)
    {
        if (amount == 0) return;

        Coins = Mathf.Max(0, Coins + amount);
        Save();
        OnStatsChanged?.Invoke();
    }
}
```

## `Assets/Scripts/LeaderboardManager.cs`

```csharp
using UnityEngine;
using Firebase.Database;
using System;
using System.Collections;
using System.Collections.Generic;

[Serializable]
public class LeaderboardEntry
{
    public int    rank;
    public string uid;
    public string name;
    public int    elo;
    public int    wins;
    public string country;
    public bool   isMe;
}

public enum LeaderboardScope { Global, Country, Friends }

/// <summary>
/// Monthly leaderboards, kept deliberately small to stay inside Firebase's
/// free tier: entries are denormalised copies of the handful of fields a row
/// needs, queried with LimitToLast so a client never downloads the full table.
///
/// Boards are keyed by month (yyyy-MM) so they reset on their own — a player
/// who starts today is never staring at an all-time top ten they cannot reach.
///
/// Requires these Realtime Database rules so the sort happens server-side:
///   "leaderboard": {
///     "global":  { "$month":            { ".indexOn": "elo" } },
///     "country": { "$cc": { "$month":   { ".indexOn": "elo" } } }
///   }
/// </summary>
public class LeaderboardManager : MonoBehaviour
{
    public static LeaderboardManager Instance { get; private set; }

    public const int PAGE_SIZE = 100;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(this); return; }
    }

    string MonthKey => FirebaseDBManager.Instance != null
        ? FirebaseDBManager.Instance.ServerMonthKey
        : DateTime.UtcNow.ToString("yyyy-MM");

    // ═══════════════════════════════════════════════════════════════════
    //  Publishing
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>Mirrors the local player's row into this month's boards.</summary>
    public void PublishMyEntry(string name, int elo, int wins, string country)
    {
        var db = FirebaseDBManager.Instance;
        string uid = AuthManager.Instance?.CurrentUser?.UserId;
        if (db == null || !db.IsInitialized || uid == null) return;

        var row = new Dictionary<string, object>
        {
            ["name"]    = name,
            ["elo"]     = elo,
            ["wins"]    = wins,
            ["country"] = country
        };

        string month = MonthKey;

        db.GetRef("leaderboard").Child("global").Child(month).Child(uid).SetValueAsync(row);

        if (!string.IsNullOrEmpty(country) && country != "XX")
            db.GetRef("leaderboard").Child("country").Child(country).Child(month).Child(uid)
              .SetValueAsync(row);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Fetching
    // ═══════════════════════════════════════════════════════════════════

    public void Fetch(LeaderboardScope scope, Action<List<LeaderboardEntry>> onDone)
    {
        switch (scope)
        {
            case LeaderboardScope.Country: FetchCountry(onDone); break;
            case LeaderboardScope.Friends: FetchFriends(onDone); break;
            default:                       FetchGlobal(onDone);  break;
        }
    }

    public void FetchGlobal(Action<List<LeaderboardEntry>> onDone)
    {
        var db = FirebaseDBManager.Instance;
        if (db == null || !db.IsInitialized) { onDone?.Invoke(new List<LeaderboardEntry>()); return; }

        var q = db.GetRef("leaderboard").Child("global").Child(MonthKey)
                  .OrderByChild("elo").LimitToLast(PAGE_SIZE);

        RunQuery(q, onDone);
    }

    public void FetchCountry(Action<List<LeaderboardEntry>> onDone)
    {
        var db = FirebaseDBManager.Instance;
        string cc = PlayerStatsManager.Instance?.Country ?? "XX";

        if (db == null || !db.IsInitialized || cc == "XX")
        {
            onDone?.Invoke(new List<LeaderboardEntry>());
            return;
        }

        var q = db.GetRef("leaderboard").Child("country").Child(cc).Child(MonthKey)
                  .OrderByChild("elo").LimitToLast(PAGE_SIZE);

        RunQuery(q, onDone);
    }

    void RunQuery(Query q, Action<List<LeaderboardEntry>> onDone)
    {
        string myUid = AuthManager.Instance?.CurrentUser?.UserId;

        q.GetValueAsync().ContinueWith(t =>
        {
            UnityMainThreadDispatcher.Enqueue(() =>
            {
                var list = new List<LeaderboardEntry>();

                if (!t.IsFaulted && t.Result != null && t.Result.Exists)
                {
                    foreach (var child in t.Result.Children)
                        list.Add(ParseRow(child, myUid));
                }

                // LimitToLast returns ascending — the board reads top-down
                list.Reverse();
                for (int i = 0; i < list.Count; i++) list[i].rank = i + 1;

                onDone?.Invoke(list);
            });
        });
    }

    static LeaderboardEntry ParseRow(DataSnapshot child, string myUid)
    {
        int elo = 0, wins = 0;
        if (child.Child("elo").Value != null)  int.TryParse(child.Child("elo").Value.ToString(), out elo);
        if (child.Child("wins").Value != null) int.TryParse(child.Child("wins").Value.ToString(), out wins);

        // Board rows use "name"; stats/{uid} rows use "displayName"
        string name = child.Child("name").Value?.ToString()
                   ?? child.Child("displayName").Value?.ToString()
                   ?? "Player";

        return new LeaderboardEntry
        {
            uid     = child.Key,
            name    = name,
            elo     = elo,
            wins    = wins,
            country = child.Child("country").Value?.ToString() ?? "XX",
            isMe    = child.Key == myUid
        };
    }

    /// <summary>
    /// Friends have no board of their own — their rows come straight from
    /// stats/{uid} and are ranked locally. The list is small, so this is cheap.
    /// </summary>
    public void FetchFriends(Action<List<LeaderboardEntry>> onDone)
    {
        StartCoroutine(FetchFriendsCo(onDone));
    }

    IEnumerator FetchFriendsCo(Action<List<LeaderboardEntry>> onDone)
    {
        var db = FirebaseDBManager.Instance;
        var list = new List<LeaderboardEntry>();
        string myUid = AuthManager.Instance?.CurrentUser?.UserId;

        if (db == null || !db.IsInitialized || myUid == null)
        {
            onDone?.Invoke(list);
            yield break;
        }

        var friendsTask = db.GetRef("friends").Child(myUid).GetValueAsync();
        yield return new WaitUntil(() => friendsTask.IsCompleted);

        var uids = new List<string> { myUid };
        if (!friendsTask.IsFaulted && friendsTask.Result != null && friendsTask.Result.Exists)
        {
            foreach (var child in friendsTask.Result.Children)
                uids.Add(child.Key);
        }

        foreach (var uid in uids)
        {
            var task = db.GetRef("stats").Child(uid).GetValueAsync();
            yield return new WaitUntil(() => task.IsCompleted);

            if (task.IsFaulted || task.Result == null || !task.Result.Exists) continue;

            list.Add(ParseRow(task.Result, myUid));
        }

        list.Sort((a, b) => b.elo.CompareTo(a.elo));
        for (int i = 0; i < list.Count; i++) list[i].rank = i + 1;

        onDone?.Invoke(list);
    }
}
```

## `Assets/Scripts/DailyManager.cs`

```csharp
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

        PlayerStatsManager.Instance?.AddCoins(reward);

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
        PlayerStatsManager.Instance?.AddCoins(c.reward);

        Save();
        OnDailyChanged?.Invoke();
        return true;
    }

    /// <summary>Total coins sitting unclaimed right now — drives the menu badge.</summary>
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
```

## `Assets/Scripts/AdManager.cs`

```csharp
using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;

/// <summary>
/// The single seam the game uses to ask for a rewarded ad.
///
/// No ad SDK is installed yet, so this ships a STUB that fakes the wait. The stub
/// exists so the whole coin flow can be built and played today; it is deliberately
/// impossible to ship by accident:
///
///   * without the MATHSTICK_ADS_ADMOB compile symbol it screams in the console
///     every single time it runs, and
///   * in a non-development build it reports NO ad available at all, so a shipped
///     APK without the real SDK hands out nothing.
///
/// To go live, install the Google Mobile Ads Unity plugin, define
/// MATHSTICK_ADS_ADMOB in Player Settings, and fill in the real implementation
/// where LoadReal/ShowReal are marked below.
/// </summary>
public class AdManager : MonoBehaviour
{
    public static AdManager Instance { get; private set; }

    /// <summary>Raised when readiness or the remaining daily count changes.</summary>
    public event Action OnAvailabilityChanged;

    [Header("Stub UI (used only until a real SDK is wired in)")]
    public GameObject stubPanel;
    public Text       stubCountdown;

    [Header("Limits")]
    [Tooltip("How many rewarded ads a player may watch per day.")]
    public int dailyCap = 5;

    [Tooltip("Seconds the stub pretends an ad runs for.")]
    public float stubDuration = 4f;

    bool showing;
    int  watchedToday;
    string watchedDate = "";

    public bool IsShowing => showing;

    public int RewardsRemainingToday
    {
        get { SyncDay(); return Mathf.Max(0, dailyCap - watchedToday); }
    }

    /// <summary>
    /// True only when an ad can actually be shown right now. Callers must always
    /// offer another way forward when this is false — the player can never be
    /// left with an ad as their only exit.
    /// </summary>
    public bool IsRewardedReady
    {
        get
        {
            if (showing) return false;
            if (RewardsRemainingToday <= 0) return false;

#if MATHSTICK_ADS_ADMOB
            return IsRealAdLoaded();
#else
            // A release build with no SDK must never pay out.
            return Debug.isDebugBuild || Application.isEditor;
#endif
        }
    }

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(this); return; }

        watchedToday = PlayerPrefs.GetInt("AdsWatchedToday", 0);
        watchedDate  = PlayerPrefs.GetString("AdsWatchedDate", "");
    }

    void Start()
    {
        if (stubPanel != null) stubPanel.SetActive(false);

#if MATHSTICK_ADS_ADMOB
        InitialiseReal();
#endif
        OnAvailabilityChanged?.Invoke();
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Daily cap
    // ═══════════════════════════════════════════════════════════════════

    static string Today => FirebaseDBManager.Instance != null
        ? FirebaseDBManager.Instance.ServerDateKey
        : DateTime.UtcNow.ToString("yyyy-MM-dd");

    /// <summary>Roll the counter over when the server date changes.</summary>
    void SyncDay()
    {
        string today = Today;
        if (watchedDate == today) return;

        watchedDate = today;
        watchedToday = 0;

        PlayerPrefs.SetString("AdsWatchedDate", watchedDate);
        PlayerPrefs.SetInt("AdsWatchedToday", 0);
        PlayerPrefs.Save();
    }

    void CountWatch()
    {
        SyncDay();
        watchedToday++;

        PlayerPrefs.SetInt("AdsWatchedToday", watchedToday);
        PlayerPrefs.SetString("AdsWatchedDate", watchedDate);
        PlayerPrefs.Save();

        OnAvailabilityChanged?.Invoke();
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Showing
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Shows a rewarded ad. onFinished(true) ONLY when it was watched to the end —
    /// a dismissal, a load failure or no fill all report false, and the caller
    /// must not pay out on false.
    /// </summary>
    public void ShowRewarded(Action<bool> onFinished)
    {
        if (showing) { onFinished?.Invoke(false); return; }

        if (!IsRewardedReady)
        {
            Debug.LogWarning("AdManager: no rewarded ad available.");
            onFinished?.Invoke(false);
            return;
        }

#if MATHSTICK_ADS_ADMOB
        showing = true;
        ShowReal(granted =>
        {
            showing = false;
            if (granted) CountWatch();
            onFinished?.Invoke(granted);
        });
#else
        showing = true;
        StartCoroutine(ShowStub(onFinished));
#endif
    }

    IEnumerator ShowStub(Action<bool> onFinished)
    {
        Debug.LogWarning("═══ AdManager STUB: no ad SDK installed. " +
                         "This grants the reward WITHOUT showing a real ad. " +
                         "Install Google Mobile Ads and define MATHSTICK_ADS_ADMOB before release. ═══");

        if (stubPanel != null) stubPanel.SetActive(true);

        float t = stubDuration;
        while (t > 0f)
        {
            if (stubCountdown != null)
                stubCountdown.text = "SIMULATED AD  ·  " + Mathf.CeilToInt(t) + "s";

            // Unscaled: the popover must work even if the game is paused
            t -= Time.unscaledDeltaTime;
            yield return null;
        }

        if (stubPanel != null) stubPanel.SetActive(false);

        showing = false;
        CountWatch();
        onFinished?.Invoke(true);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Real SDK — filled in once the plugin is installed
    // ═══════════════════════════════════════════════════════════════════

#if MATHSTICK_ADS_ADMOB
    // Replace these three with the Google Mobile Ads plugin calls. Keep the
    // contract: ShowReal must invoke its callback with TRUE only on a completed
    // view, and the game must never grant coins on false.
    void InitialiseReal()
    {
        Debug.LogError("AdManager: MATHSTICK_ADS_ADMOB is defined but the real " +
                       "implementation is still empty. Wire up MobileAds.Initialize " +
                       "and RewardedAd.Load here.");
    }

    bool IsRealAdLoaded() => false;

    void ShowReal(Action<bool> onGranted) => onGranted?.Invoke(false);
#endif
}
```

## `Assets/Scripts/ProgressionGUIManager.cs`

```csharp
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// Drives the three retention screens: PROFILE (rating + record),
/// LEADERBOARD (monthly, by country, and among friends) and DAILY
/// (login streak + rotating challenges).
///
/// Kept out of GUIManager so the menu flow stays readable; GUIManager only
/// opens these panels and hides them again.
/// </summary>
public class ProgressionGUIManager : MonoBehaviour
{
    public static ProgressionGUIManager Instance { get; private set; }

    // ── Panels ──────────────────────────────────────────────────────────
    public GameObject pnl_profile = null;
    public GameObject pnl_leaderboard = null;
    public GameObject pnl_daily = null;

    // ── Profile ─────────────────────────────────────────────────────────
    public Text  lbl_prof_name    = null;
    public Text  lbl_prof_rank    = null;
    public Text  lbl_prof_elo     = null;
    public Text  lbl_prof_next    = null;   // "112 TO GOLD"
    public Image img_prof_rankbar = null;   // filled image
    public Text  lbl_prof_record  = null;   // "24W - 11L"
    public Text  lbl_prof_winrate = null;
    public Text  lbl_prof_streak  = null;
    public Text  lbl_prof_best    = null;
    public Text  lbl_prof_rounds  = null;
    public Text  lbl_prof_coins   = null;

    // ── Leaderboard ─────────────────────────────────────────────────────
    public Transform  lbContent    = null;
    public GameObject lbRowPrefab  = null;
    public Text       lbl_lb_status = null;
    public Text       lbl_lb_month  = null;
    public Text       lbl_tab_global  = null;
    public Text       lbl_tab_country = null;
    public Text       lbl_tab_friends = null;

    // ── Daily ───────────────────────────────────────────────────────────
    public Text       lbl_daily_streak    = null;   // big number
    public Text       lbl_daily_sub       = null;   // "DAY STREAK"
    public Text       lbl_daily_best      = null;
    public Text       lbl_daily_coins     = null;
    public GameObject btn_claim_streak    = null;
    public Text       lbl_claim_streak    = null;
    public Transform  challengeRow0       = null;
    public Transform  challengeRow1       = null;
    public Transform  challengeRow2       = null;

    // ── Start-screen badges ─────────────────────────────────────────────
    public Text       lbl_menu_rank  = null;
    public Text       lbl_menu_coins = null;
    public Text       lbl_arcade_coins = null;  // same balance, on the screen that charges
    public GameObject badge_daily    = null;   // dot shown when rewards are waiting

    LeaderboardScope currentScope = LeaderboardScope.Global;

    static readonly Color TAB_ON  = new Color(0.46f, 1f, 0.01f);
    static readonly Color TAB_OFF = new Color(0.22f, 0.41f, 0.12f);
    static readonly Color DIM     = new Color(0.4f, 0.4f, 0.4f);

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(this); return; }
    }

    void Start()
    {
        if (PlayerStatsManager.Instance != null)
            PlayerStatsManager.Instance.OnStatsChanged += OnProgressionChanged;

        if (DailyManager.Instance != null)
            DailyManager.Instance.OnDailyChanged += OnProgressionChanged;

        HideAll();
    }

    void OnProgressionChanged()
    {
        RefreshMenuBadges();

        if (pnl_profile != null && pnl_profile.activeSelf) RefreshProfile();
        if (pnl_daily != null && pnl_daily.activeSelf)     RefreshDaily();
    }

    public void HideAll()
    {
        Show(pnl_profile, false);
        Show(pnl_leaderboard, false);
        Show(pnl_daily, false);
    }

    void BackToMenu()
    {
        HideAll();
        var gui = FindObjectOfType<GUIManager>();
        if (gui != null) gui.ShowStartScreen();
    }

    public void OnBackPressed() => BackToMenu();

    // ═══════════════════════════════════════════════════════════════════
    //  Menu badges
    // ═══════════════════════════════════════════════════════════════════

    public void RefreshMenuBadges()
    {
        var stats = PlayerStatsManager.Instance;
        if (stats != null)
        {
            if (lbl_menu_rank != null)
            {
                lbl_menu_rank.text = stats.RankName + "  " + stats.Elo;
                lbl_menu_rank.color = stats.RankColor;
            }
            if (lbl_menu_coins != null)
                lbl_menu_coins.text = stats.Coins.ToString();
            if (lbl_arcade_coins != null)
                lbl_arcade_coins.text = stats.Coins.ToString();
        }

        var daily = DailyManager.Instance;
        if (badge_daily != null)
            badge_daily.SetActive(daily != null && daily.PendingRewards > 0);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Profile
    // ═══════════════════════════════════════════════════════════════════

    public void ShowProfile()
    {
        var gui = FindObjectOfType<GUIManager>();
        gui?.HideAllPanelsPublic();

        HideAll();
        Show(pnl_profile, true);
        RefreshProfile();
    }

    void RefreshProfile()
    {
        var stats = PlayerStatsManager.Instance;
        if (stats == null) return;

        if (lbl_prof_name != null)
            lbl_prof_name.text = (AuthManager.Instance?.DisplayName ?? "PLAYER").ToUpper();

        if (lbl_prof_rank != null)
        {
            lbl_prof_rank.text  = stats.RankName;
            lbl_prof_rank.color = stats.RankColor;
        }

        if (lbl_prof_elo != null) lbl_prof_elo.text = stats.Elo.ToString();

        if (lbl_prof_next != null)
        {
            int toNext = stats.EloToNextRank;
            int idx = PlayerStatsManager.RankIndexFor(stats.Elo);

            lbl_prof_next.text = toNext > 0 && idx + 1 < PlayerStatsManager.RANK_NAMES.Length
                ? toNext + " TO " + PlayerStatsManager.RANK_NAMES[idx + 1]
                : "TOP RANK";
        }

        if (img_prof_rankbar != null)
        {
            img_prof_rankbar.fillAmount = stats.RankProgress;
            img_prof_rankbar.color = stats.RankColor;
        }

        if (lbl_prof_record != null)  lbl_prof_record.text  = stats.Wins + "W - " + stats.Losses + "L";
        if (lbl_prof_winrate != null) lbl_prof_winrate.text = stats.WinRate + "%";
        if (lbl_prof_streak != null)  lbl_prof_streak.text  = stats.CurrentStreak.ToString();
        if (lbl_prof_best != null)    lbl_prof_best.text    = stats.BestStreak.ToString();
        if (lbl_prof_rounds != null)  lbl_prof_rounds.text  = stats.RoundsWon + " / " + (stats.RoundsWon + stats.RoundsLost);
        if (lbl_prof_coins != null)   lbl_prof_coins.text   = stats.Coins.ToString();
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Leaderboard
    // ═══════════════════════════════════════════════════════════════════

    public void ShowLeaderboard()
    {
        var gui = FindObjectOfType<GUIManager>();
        gui?.HideAllPanelsPublic();

        HideAll();
        Show(pnl_leaderboard, true);

        currentScope = LeaderboardScope.Global;
        LoadBoard();
    }

    public void OnTabGlobal()  { currentScope = LeaderboardScope.Global;  LoadBoard(); }
    public void OnTabCountry() { currentScope = LeaderboardScope.Country; LoadBoard(); }
    public void OnTabFriends() { currentScope = LeaderboardScope.Friends; LoadBoard(); }

    void LoadBoard()
    {
        UpdateTabHighlight();
        ClearRows();

        if (lbl_lb_month != null)
        {
            string month = FirebaseDBManager.Instance != null
                ? FirebaseDBManager.Instance.ServerMonthKey
                : System.DateTime.UtcNow.ToString("yyyy-MM");
            lbl_lb_month.text = "SEASON " + month;
        }

        if (lbl_lb_status != null) lbl_lb_status.text = "LOADING...";

        var mgr = LeaderboardManager.Instance;
        if (mgr == null)
        {
            if (lbl_lb_status != null) lbl_lb_status.text = "UNAVAILABLE";
            return;
        }

        mgr.Fetch(currentScope, PopulateBoard);
    }

    void UpdateTabHighlight()
    {
        if (lbl_tab_global != null)
            lbl_tab_global.color = currentScope == LeaderboardScope.Global ? TAB_ON : TAB_OFF;
        if (lbl_tab_country != null)
            lbl_tab_country.color = currentScope == LeaderboardScope.Country ? TAB_ON : TAB_OFF;
        if (lbl_tab_friends != null)
            lbl_tab_friends.color = currentScope == LeaderboardScope.Friends ? TAB_ON : TAB_OFF;

        if (lbl_tab_country != null)
        {
            string cc = PlayerStatsManager.Instance?.Country ?? "XX";
            lbl_tab_country.text = cc == "XX" ? "COUNTRY" : cc;
        }
    }

    void ClearRows()
    {
        if (lbContent == null) return;

        for (int i = lbContent.childCount - 1; i >= 0; i--)
            Destroy(lbContent.GetChild(i).gameObject);
    }

    void PopulateBoard(List<LeaderboardEntry> entries)
    {
        ClearRows();

        if (lbl_lb_status != null)
        {
            if (entries.Count == 0)
                lbl_lb_status.text = currentScope == LeaderboardScope.Friends
                    ? "ADD FRIENDS TO COMPARE"
                    : "NO PLAYERS YET THIS MONTH";
            else
                lbl_lb_status.text = entries.Count + (entries.Count == 1 ? " PLAYER" : " PLAYERS");
        }

        if (lbContent == null || lbRowPrefab == null) return;

        foreach (var e in entries)
        {
            var row = Instantiate(lbRowPrefab, lbContent);
            row.SetActive(true);

            var lblRank = row.transform.Find("lbl_rank")?.GetComponent<Text>();
            if (lblRank != null)
            {
                lblRank.text = "#" + e.rank;
                // Podium colours make the top three read at a glance
                if (e.rank == 1)      lblRank.color = new Color(1f, 0.84f, 0f);
                else if (e.rank == 2) lblRank.color = new Color(0.75f, 0.75f, 0.75f);
                else if (e.rank == 3) lblRank.color = new Color(0.80f, 0.50f, 0.20f);
            }

            var lblName = row.transform.Find("lbl_name")?.GetComponent<Text>();
            if (lblName != null)
            {
                lblName.text = e.isMe ? e.name + "  (YOU)" : e.name;
                if (e.isMe) lblName.color = new Color(0.46f, 1f, 0.01f);
            }

            var lblElo = row.transform.Find("lbl_elo")?.GetComponent<Text>();
            if (lblElo != null)
            {
                lblElo.text = e.elo.ToString();
                lblElo.color = PlayerStatsManager.RankColorFor(e.elo);
            }

            var lblSub = row.transform.Find("lbl_sub")?.GetComponent<Text>();
            if (lblSub != null)
                lblSub.text = e.wins + "W" + (currentScope == LeaderboardScope.Global && e.country != "XX"
                    ? "  ·  " + e.country : "");

            // Tint the player's own row so it stands out while scrolling
            if (e.isMe)
            {
                var bg = row.GetComponent<Image>();
                if (bg != null) bg.color = new Color(0.10f, 0.20f, 0.06f);
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Daily
    // ═══════════════════════════════════════════════════════════════════

    public void ShowDaily()
    {
        var gui = FindObjectOfType<GUIManager>();
        gui?.HideAllPanelsPublic();

        HideAll();
        Show(pnl_daily, true);
        RefreshDaily();
    }

    void RefreshDaily()
    {
        var daily = DailyManager.Instance;
        var stats = PlayerStatsManager.Instance;

        if (lbl_daily_coins != null && stats != null)
            lbl_daily_coins.text = stats.Coins.ToString();

        if (daily == null) return;

        if (lbl_daily_streak != null) lbl_daily_streak.text = daily.Streak.ToString();
        if (lbl_daily_sub != null)
            lbl_daily_sub.text = daily.Streak == 1 ? "DAY STREAK" : "DAY STREAK";
        if (lbl_daily_best != null)
            lbl_daily_best.text = "BEST: " + daily.LongestStreak;

        // Streak claim button
        if (btn_claim_streak != null)
        {
            bool canClaim = daily.CanClaimStreak;
            btn_claim_streak.SetActive(true);

            var claimBtn = btn_claim_streak.transform.Find("btn_face")?.GetComponent<Button>();
            if (claimBtn != null) claimBtn.interactable = canClaim;

            if (lbl_claim_streak != null)
            {
                // Nothing has to be earned first any more — opening the app is
                // the whole requirement, so there is no "go play something" state.
                if (canClaim)
                {
                    lbl_claim_streak.text = "CLAIM +" + daily.StreakReward;
                    lbl_claim_streak.color = new Color(0.46f, 1f, 0.01f);
                }
                else
                {
                    lbl_claim_streak.text = "COME BACK TOMORROW";
                    lbl_claim_streak.color = DIM;
                }
            }
        }

        RefreshChallengeRow(challengeRow0, 0);
        RefreshChallengeRow(challengeRow1, 1);
        RefreshChallengeRow(challengeRow2, 2);
    }

    void RefreshChallengeRow(Transform row, int index)
    {
        if (row == null) return;

        var daily = DailyManager.Instance;
        if (daily == null || index >= daily.Challenges.Count)
        {
            row.gameObject.SetActive(false);
            return;
        }

        row.gameObject.SetActive(true);
        var c = daily.Challenges[index];

        var lblDesc = row.Find("lbl_desc")?.GetComponent<Text>();
        if (lblDesc != null) lblDesc.text = c.Description;

        var lblProg = row.Find("lbl_prog")?.GetComponent<Text>();
        if (lblProg != null)
            lblProg.text = Mathf.Min(c.progress, c.target) + " / " + c.target;

        var bar = row.Find("bar/bar_fill")?.GetComponent<Image>();
        if (bar != null)
            bar.fillAmount = c.target > 0 ? Mathf.Clamp01((float)c.progress / c.target) : 0f;

        var lblReward = row.Find("btn_claim/btn_face/lbl_btn")?.GetComponent<Text>();
        var btnClaim  = row.Find("btn_claim/btn_face")?.GetComponent<Button>();

        if (lblReward != null)
        {
            if (c.claimed)
            {
                lblReward.text = "DONE";
                lblReward.color = DIM;
            }
            else if (c.Completed)
            {
                lblReward.text = "CLAIM";
                lblReward.color = new Color(0.46f, 1f, 0.01f);
            }
            else
            {
                lblReward.text = "+" + c.reward;
                lblReward.color = new Color(0.4f, 0.73f, 0.42f);
            }
        }

        if (btnClaim != null)
            btnClaim.interactable = c.Completed && !c.claimed;
    }

    public void OnClaimStreakPressed()
    {
        if (DailyManager.Instance != null && DailyManager.Instance.ClaimStreak())
            RefreshDaily();
    }

    public void OnClaimChallenge0() => ClaimChallenge(0);
    public void OnClaimChallenge1() => ClaimChallenge(1);
    public void OnClaimChallenge2() => ClaimChallenge(2);

    void ClaimChallenge(int index)
    {
        if (DailyManager.Instance != null && DailyManager.Instance.ClaimChallenge(index))
            RefreshDaily();
    }

    void OnDestroy()
    {
        if (PlayerStatsManager.Instance != null)
            PlayerStatsManager.Instance.OnStatsChanged -= OnProgressionChanged;

        if (DailyManager.Instance != null)
            DailyManager.Instance.OnDailyChanged -= OnProgressionChanged;
    }

    /// <summary>
    /// SetActive that survives an unassigned or destroyed reference.
    ///
    /// The null-conditional operator does a RAW reference check and bypasses
    /// Unity's overloaded ==, so `panel?.SetActive(x)` throws
    /// UnassignedReferenceException on a field the scene never wired up. That
    /// is exactly what happens whenever a script gains a new field and the
    /// scene has not been rebuilt yet — and one throw inside a hide-everything
    /// sweep takes the whole screen down with it.
    /// </summary>
    static void Show(GameObject go, bool on)
    {
        if (go != null && go.activeSelf != on) go.SetActive(on);
    }

}
```
