using UnityEngine;
using System;
using System.Collections;

/// <summary>
/// Runs a fully local 1v1 match against a simulated opponent.
///
/// This exists so matchmaking never dead-ends: when no human is found within
/// LobbyManager's search window, the player still gets a match instead of an
/// empty queue. The bot mirrors ArcadeMatchManager's event surface so
/// ArcadeGUIManager can drive the same panels for both kinds of match.
///
/// Nothing here touches Firebase — the whole match lives in memory.
/// </summary>
public class BotMatchManager : MonoBehaviour
{
    public static BotMatchManager Instance { get; private set; }

    // ── Events (mirror ArcadeMatchManager) ──────────────────────────────
    public event Action<int, int> OnScoreUpdated;   // myScore, botScore
    public event Action<int, bool> OnRoundResult;   // round#, iWon
    public event Action<bool> OnMatchResult;        // iWon
    public event Action OnEquationReady;
    public event Action<string> OnOpponentJoined;   // bot name
    public event Action<string> OnError;

    // ── Match state ─────────────────────────────────────────────────────
    bool active;
    string botName;
    int myScore, botScore;
    int currentRound;
    int firstTo;
    GameMode matchMode;
    bool roundActive;
    float botSkill;              // 0 = hopeless, 1 = near-instant
    Coroutine botSolveCo;
    Coroutine nextRoundCo;

    public bool IsInMatch => active;
    public string OpponentName => botName;
    public int MyScore => myScore;
    public int OpponentScore => botScore;
    public int CurrentRound => currentRound;
    public int FirstTo => firstTo;
    public GameMode MatchMode => matchMode;

    // ── Bot identity pool ───────────────────────────────────────────────
    // Deliberately in the same shape as real guest names ("Player_48213")
    // plus ordinary handles, so a bot opponent reads like any other player.
    static readonly string[] HANDLES =
    {
        "Alex", "Maya", "Kai", "Nova", "Rex", "Luna", "Zed", "Iris",
        "Milo", "Vera", "Otto", "Nyx", "Finn", "Sage", "Jax", "Wren",
        "Dario", "Elis", "Cato", "Runa", "Pip", "Tova", "Bram", "Ivy"
    };

    static readonly string[] SUFFIXES =
    {
        "", "", "", "_x", "99", "07", "_pro", "21", "_hd", "42", "_", "88"
    };

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(this); return; }

        Messenger.AddListener(Message.ArcadeRoundWon, OnLocalPlayerSolved);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Start
    // ═══════════════════════════════════════════════════════════════════

    public void StartBotMatch(GameMode mode, int firstToRounds)
    {
        if (active) return;

        // Random mode resolves to a concrete difficulty, same as a human match would
        matchMode = mode == GameMode.Random
            ? (GameMode)UnityEngine.Random.Range(0, 3)
            : mode;

        firstTo = firstToRounds;
        myScore = 0;
        botScore = 0;
        currentRound = 1;
        active = true;
        roundActive = false;
        botName = GenerateBotName();
        botSkill = RollBotSkill();

        GameManager.Instance.SetMode(matchMode);
        GameManager.Instance.isArcadeMode = true;

        OnOpponentJoined?.Invoke(botName);

        // Short pause so the "found an opponent" beat is visible
        nextRoundCo = StartCoroutine(BeginRoundAfter(1.2f));
    }

    static string GenerateBotName()
    {
        // Half the bots use the guest-style name real anonymous players get
        if (UnityEngine.Random.Range(0, 2) == 0)
            return "Player_" + UnityEngine.Random.Range(10000, 99999);

        return HANDLES[UnityEngine.Random.Range(0, HANDLES.Length)]
             + SUFFIXES[UnityEngine.Random.Range(0, SUFFIXES.Length)];
    }

    /// <summary>
    /// Bot strength tracks the player's rating so matches stay close.
    /// A per-match wobble keeps consecutive bots from feeling identical.
    /// </summary>
    float RollBotSkill()
    {
        int elo = PlayerStatsManager.Instance != null
            ? PlayerStatsManager.Instance.Elo
            : PlayerStatsManager.START_ELO;

        float baseSkill = Mathf.InverseLerp(800f, 1800f, elo);
        float wobble = UnityEngine.Random.Range(-0.18f, 0.18f);
        return Mathf.Clamp(baseSkill + wobble, 0.05f, 0.95f);
    }

    /// <summary>Roughly how long this difficulty takes a competent player.</summary>
    public static float NominalFor(GameMode mode)
    {
        switch (mode)
        {
            case GameMode.Easy:   return 45f;
            case GameMode.Medium: return 65f;
            case GameMode.Hard:   return 85f;
            default:              return 45f;
        }
    }

    /// <summary>
    /// However fast the bot is, it never answers before this. A round decided
    /// while the player is still reading the board is not a round they lost.
    /// </summary>
    const float MIN_SOLVE_SECONDS = 11f;

    /// <summary>How much of a lead the bot gives back per round it is ahead.</summary>
    const float CATCH_UP_PER_ROUND = 0.18f;

    /// <summary>
    /// How long the bot "thinks" this round. Rounds have no time limit any
    /// more, so the bot always answers eventually — otherwise a round the
    /// player cannot solve would never end. Beating it means being faster.
    /// </summary>
    float RollBotSolveTime()
    {
        float nominal = NominalFor(matchMode);

        // Re-rolled every round. The skill was drawn once per match, so a fast draw
        // stayed fast for every round of it — the player did not lose some
        // rounds, they lost all of that match.
        float skill = botSkill + UnityEngine.Random.Range(-0.14f, 0.14f);

        // A bot that is ahead eases off, one that is behind presses. Without
        // this a match settled in the first round stays settled, which is the
        // shape that makes a fallback opponent feel like a wall.
        skill -= (botScore - myScore) * CATCH_UP_PER_ROUND;

        skill = Mathf.Clamp(skill, 0.02f, 0.95f);

        float fastest = nominal * 0.45f;
        float slowest = nominal * 1.60f;

        float t = Mathf.Lerp(slowest, fastest, skill) * UnityEngine.Random.Range(0.9f, 1.15f);
        return Mathf.Max(t, MIN_SOLVE_SECONDS);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Round flow
    // ═══════════════════════════════════════════════════════════════════

    IEnumerator BeginRoundAfter(float delay)
    {
        yield return new WaitForSeconds(delay);
        BeginRound();
    }

    void BeginRound()
    {
        if (!active) return;

        var gm = GameManager.Instance;
        int[] solution = null;

        for (int attempt = 0; attempt < 200; attempt++)
        {
            if (matchMode == GameMode.Medium)      solution = gm.GenerateCandidate3D();
            else if (matchMode == GameMode.Hard)   solution = gm.GenerateCandidateHard();
            else                                   solution = gm.GenerateCandidate();

            if (solution != null) break;
        }

        if (solution == null)
        {
            OnError?.Invoke("Failed to generate equation");
            return;
        }

        bool isMinus = matchMode != GameMode.Hard && UnityEngine.Random.Range(0, 2) == 1;

        gm.InitializeFromRemote(solution, isMinus, matchMode);

        roundActive = true;
        OnEquationReady?.Invoke();

        botSolveCo = StartCoroutine(BotSolvesAfter(RollBotSolveTime()));
    }

    IEnumerator BotSolvesAfter(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (!active || !roundActive) yield break;

        EndRound(false);
    }

    /// <summary>Player answered correctly (Message.ArcadeRoundWon).</summary>
    void OnLocalPlayerSolved()
    {
        if (!active || !roundActive) return;
        EndRound(true);
    }

    /// <summary>
    /// Legacy hook from the countdown timer. 1v1 rounds no longer expire, so
    /// this is a no-op kept only so a stale call cannot hand the bot a point.
    /// </summary>
    public void OnLocalPlayerTimeout() { }

    void EndRound(bool iWon)
    {
        roundActive = false;

        if (botSolveCo != null) { StopCoroutine(botSolveCo); botSolveCo = null; }

        var timer = FindObjectOfType<TimerManager>();
        if (timer != null) timer.StopArcadeTimer();

        if (iWon) myScore++;
        else      botScore++;

        OnScoreUpdated?.Invoke(myScore, botScore);
        OnRoundResult?.Invoke(currentRound, iWon);

        if (myScore >= firstTo || botScore >= firstTo)
        {
            nextRoundCo = StartCoroutine(FinishMatchAfter(1.6f, myScore >= firstTo));
            return;
        }

        currentRound++;
        nextRoundCo = StartCoroutine(BeginRoundAfter(3.2f));
    }

    IEnumerator FinishMatchAfter(float delay, bool iWon)
    {
        yield return new WaitForSeconds(delay);

        active = false;
        GameManager.Instance.isArcadeMode = false;

        OnMatchResult?.Invoke(iWon);
        Messenger.Broadcast(Message.ArcadeMatchEnded);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Rematch / cleanup
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>Bots always accept — a rematch starts immediately.</summary>
    public void Rematch()
    {
        GameMode mode = matchMode;
        int ft = firstTo;
        Cleanup();
        StartBotMatch(mode, ft);
    }

    public void Cleanup()
    {
        if (botSolveCo != null)  { StopCoroutine(botSolveCo);  botSolveCo = null; }
        if (nextRoundCo != null) { StopCoroutine(nextRoundCo); nextRoundCo = null; }

        var timer = FindObjectOfType<TimerManager>();
        if (timer != null) timer.StopArcadeTimer();

        if (active && GameManager.Instance != null)
            GameManager.Instance.isArcadeMode = false;

        active = false;
        roundActive = false;
        myScore = 0;
        botScore = 0;
        currentRound = 1;
    }

    void OnDestroy()
    {
        Cleanup();
    }
}
