using UnityEngine;
using Firebase.Database;
using System;
using System.Collections.Generic;

/// <summary>
/// Owns the player's record: rating, win/loss, streaks and experience.
///
/// Stored at stats/{uid} in Realtime Database and mirrored into the
/// leaderboards whenever it changes. Loaded once after login and kept in
/// memory so the UI never has to wait on a round trip.
/// </summary>
public class PlayerStatsManager : MonoBehaviour
{
    public static PlayerStatsManager Instance { get; private set; }

    public const int START_ELO = 1000;

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
    /// The visible reward for difficulty is paid in XP, below, where inflation
    /// does not corrupt anything.
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

    // ═══════════════════════════════════════════════════════════════════
    //  Experience
    // ═══════════════════════════════════════════════════════════════════
    //
    // This replaced a coin balance, and the important part is not the rename.
    //
    // Coins were spent to enter a ranked match, which meant the balance could
    // fall. The break-even win rate of a fee/payout economy is exactly
    // fee/payout, and ELO matchmaking drives every player toward 50% — so the
    // balance was a near-zero-drift random walk against an absorbing barrier
    // at zero. Such a walk reaches zero with probability 1. It needed a
    // new-player shield, daily free entries, and an out-of-coins sheet with a
    // rewarded ad, and all three existed to hold back a floor the design had
    // built in on purpose.
    //
    // XP only goes up. There is no floor to defend, so all of that machinery
    // is gone, and every match is free.

    /// <summary>XP for finishing a match. Losing still pays — see above.</summary>
    public static int MatchXp(GameMode mode, bool won)
    {
        switch (mode)
        {
            case GameMode.Easy:   return won ? 20 : 6;
            case GameMode.Hard:   return won ? 55 : 16;
            default:              return won ? 35 : 10;
        }
    }

    /// <summary>
    /// Total XP needed to REACH this level: 50·(n−1)·n.
    ///
    /// The gap between levels grows by a flat 100 each time — 100, 200, 300 —
    /// so early levels arrive quickly enough to explain what the bar is for,
    /// and later ones stay worth reaching without the curve running away.
    /// </summary>
    public static int XpForLevel(int level) => 50 * (level - 1) * Mathf.Max(level, 0);

    /// <summary>Level from total XP — the inverse of XpForLevel.</summary>
    public static int LevelForXp(int xp)
    {
        if (xp <= 0) return 1;

        // 50n² − 50n − xp ≤ 0  ⇒  n ≤ (50 + √(2500 + 200·xp)) / 100
        return Mathf.Max(1, Mathf.FloorToInt((50f + Mathf.Sqrt(2500f + 200f * xp)) / 100f));
    }

    public int Level => LevelForXp(Xp);

    /// <summary>XP earned since this level began.</summary>
    public int XpIntoLevel => Xp - XpForLevel(Level);

    /// <summary>XP this level costs in total — the denominator of the bar.</summary>
    public int XpForThisLevel => XpForLevel(Level + 1) - XpForLevel(Level);

    /// <summary>0..1 through the current level.</summary>
    public float LevelProgress =>
        XpForThisLevel > 0 ? Mathf.Clamp01((float)XpIntoLevel / XpForThisLevel) : 0f;

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
    public int    Xp            { get; private set; }
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

                // A new account starts empty and earns from the first match on
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
        // Accounts written before XP still carry a coin balance. Reading it
        // once as the opening XP means a tester keeps their number instead of
        // being reset to zero by a rename.
        Xp            = ReadInt(snap, "xp", ReadInt(snap, "coins", 0));
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
            ["xp"]            = Xp,
            ["updatedAt"]     = ServerValue.Timestamp
        };

        // The result was being discarded, so a rules rejection — the whole
        // point of having rules — produced no sign anywhere that the write had
        // not landed.
        db.GetRef("stats").Child(uid).UpdateChildrenAsync(data).ContinueWith(t =>
        {
            if (t.IsFaulted || t.IsCanceled)
                Debug.LogError("Stats save rejected: " +
                               (t.Exception != null ? t.Exception.Message : "cancelled"));
        });

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
        // A failed load leaves every counter at its constructor default, and
        // Save() writes the whole record in one atomic UpdateChildrenAsync.
        // Recording a match on top of that would send xp = 0 + one match,
        // elo = 1000 and matches = 1 over the real values — and the rules
        // reject the update as a whole (xp is monotonic, matches may not
        // decrease), so the player would lose the match result AND everything
        // else in the same write, silently.
        //
        // Load() already refuses to save on a failed read for exactly this
        // reason; it just never stopped the NEXT save.
        if (!IsLoaded)
        {
            Debug.LogWarning("Stats not loaded; not recording this match rather than " +
                             "overwriting the server record with defaults.");
            return;
        }

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

        // XP is where difficulty actually pays: no ladder to distort, and the
        // player can see the number before choosing the mode.
        //
        // Routed through AddXp rather than added here, so a level crossed by
        // winning a match raises OnLevelUp like every other way of earning.
        // AddXp saves and fires OnStatsChanged itself.
        LastXpGain = MatchXp(mode, won);
        AddXp(LastXpGain);

        DailyManager.Instance?.ReportMatchPlayed(won, mode);
    }

    /// <summary>XP from the most recent match, for the result screen.</summary>
    public int LastXpGain { get; private set; }

    public void AddXp(int amount)
    {
        if (amount <= 0) return;

        int before = Level;

        Xp += amount;
        Save();
        OnStatsChanged?.Invoke();

        if (Level > before) OnLevelUp?.Invoke(Level);
    }

    /// <summary>Raised after AddXp crosses a threshold. Carries the new level.</summary>
    public event Action<int> OnLevelUp;
}
