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
    /// Opening balance for a brand-new account. Without it a first-time player
    /// hits the entry fee with zero coins and is bounced into the top-up sheet
    /// before ever seeing a match — the worst possible first thirty seconds.
    /// 100 buys roughly a dozen matches, which is well past the point where the
    /// first daily bonus lands.
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

                bool existing = t.Result != null && t.Result.Exists;

                if (existing) ReadFrom(t.Result);
                else          Coins = START_COINS;   // genuinely a first run

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
