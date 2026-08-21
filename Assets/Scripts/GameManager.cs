using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public Number number1 = null;
    public Number number2 = null;
    public Number answer = null;

    // 3-digit references for Medium mode
    public Number number1_3d = null;
    public Number number2_3d = null;
    public Number answer_3d = null;
    public GameObject eqPanel2d = null;
    public GameObject eqPanel3d = null;
    public GameObject eqBg2d = null;
    public GameObject eqBg3d = null;
    public PlusMinus pm_3d = null;

    // Hard mode references (3 numbers, 2 operators)
    public Number number1_hard = null;
    public Number number2_hard = null;
    public Number number3_hard = null;
    public Number answer_hard = null;
    public PlusMinus pm_hard1 = null;
    public PlusMinus pm_hard2 = null;
    public GameObject eqPanelHard = null;
    public GameObject eqBgHard = null;

    int gamesWonCounter = 0;
    int highscore = 0;

    bool plus = true;
    bool gameActive = false;

    public bool isArcadeMode = false;

    // Store correct solution for display on loss
    public string correctSolution { get; private set; } = "";

    public PlusMinus pm = null;

    public GameMode currentMode = GameMode.Easy;

    // Active references (switch based on mode)
    PlusMinus activePm {
        get {
            if (currentMode == GameMode.Medium) return pm_3d;
            if (currentMode == GameMode.Hard) return pm_hard1;
            return pm;
        }
    }

    public void SetMode(GameMode mode)
    {
        currentMode = mode;
        Debug.Log("Game mode set to: " + mode);
    }


    void Awake()
    {
        // Nothing here animates faster than the eye. On a 120 Hz panel the
        // default would render twice as many frames as the game needs and
        // halve battery life for no visible gain.
        Application.targetFrameRate = 60;

        if (Instance == null) Instance = this;
        Messenger.AddListener(Message.CheckForSolution, CheckAnswer);
        Messenger.AddListener(Message.StartNewGame, StartNewGame);
        Messenger.AddListener(Message.OnResetProgress, () => { gamesWonCounter = 0; Messenger.Broadcast<int, int>(Message.SaveGame, highscore, gamesWonCounter); });
        Messenger.AddListener(Message.OnIncrementProgress, () => { gamesWonCounter++; if (highscore < gamesWonCounter) { highscore = gamesWonCounter; } Messenger.Broadcast<int, int>(Message.SaveGame, highscore, gamesWonCounter); });
        Messenger.AddReceiverListener<int>(ReceiveMessage.ReceiveGameProgress, () => { return gamesWonCounter; });
        Messenger.AddListener<int, int>(Message.SetHighscoreAndCurrentScore, (hs, cs) => { highscore = hs; gamesWonCounter = cs; });
        Messenger.AddListener(Message.GameWon, () => { gameActive = false; });
        Messenger.AddListener(Message.GameLost, () => { gameActive = false; });
    }

    // A digit slot is constrained by ONE dead segment, or by NO_DEAD_SEGMENT,
    // which means every stick is the player's to place. Digit 8 needs all seven
    // sticks, so NO_DEAD_SEGMENT is the only way it can ever appear.
    public const int NO_DEAD_SEGMENT = 7;

    List<int>[] LinePositionToNumber = new List<int>[NO_DEAD_SEGMENT + 1];

    // Candidate digits per dead segment, cached as arrays so the generator does
    // not walk a List on every one of the millions of combinations it counts.
    int[][] candAll = new int[NO_DEAD_SEGMENT + 1][];

    // Reused so the generator does not allocate on every candidate it rejects
    readonly bool[] okHi = new bool[10];
    readonly bool[] okLo = new bool[10];
    readonly bool[] okMid = new bool[10];
    readonly List<int> vals1 = new List<int>(1024);
    readonly List<int> vals2 = new List<int>(1024);
    readonly int[]  layoutBuf = new int[6];
    List<int>[] LineNumberToPosition = new List<int>[10];

    void Start()
    {
        LinePositionToNumber[(int)LinePositions.Bottom]      = new List<int> { 1, 4, 7 };
        LinePositionToNumber[(int)LinePositions.BottomLeft]   = new List<int> { 1, 3, 4, 5, 7, 9 };
        LinePositionToNumber[(int)LinePositions.BottomRight]  = new List<int> { 2 };
        LinePositionToNumber[(int)LinePositions.Middle]       = new List<int> { 0, 1, 7 };
        LinePositionToNumber[(int)LinePositions.Top]          = new List<int> { 1, 4 };
        LinePositionToNumber[(int)LinePositions.TopLeft]      = new List<int> { 1, 2, 3, 7 };
        LinePositionToNumber[(int)LinePositions.TopRight]     = new List<int> { 5, 6 };

        LineNumberToPosition[0] = new List<int> { (int)LinePositions.Middle };
        LineNumberToPosition[1] = new List<int> { (int)LinePositions.Bottom, (int)LinePositions.BottomLeft, (int)LinePositions.Middle, (int)LinePositions.Top, (int)LinePositions.TopLeft };
        LineNumberToPosition[2] = new List<int> { (int)LinePositions.BottomRight, (int)LinePositions.TopLeft };
        LineNumberToPosition[3] = new List<int> { (int)LinePositions.BottomLeft, (int)LinePositions.TopLeft };
        LineNumberToPosition[4] = new List<int> { (int)LinePositions.Bottom, (int)LinePositions.BottomLeft, (int)LinePositions.Top };
        LineNumberToPosition[5] = new List<int> { (int)LinePositions.BottomLeft, (int)LinePositions.TopRight };
        LineNumberToPosition[6] = new List<int> { (int)LinePositions.TopRight };
        LineNumberToPosition[7] = new List<int> { (int)LinePositions.Bottom, (int)LinePositions.BottomLeft, (int)LinePositions.Middle, (int)LinePositions.TopLeft };
        LineNumberToPosition[8] = new List<int> { };
        LineNumberToPosition[9] = new List<int> { (int)LinePositions.BottomLeft };

        // With no dead segment every digit is reachable, 8 included
        LinePositionToNumber[NO_DEAD_SEGMENT] = new List<int> { 0,1,2,3,4,5,6,7,8,9 };
        LineNumberToPosition[8] = new List<int> { NO_DEAD_SEGMENT };

        BuildCandidateTables();
    }

    // ── Check answer using player's chosen operation ──────────────────────
    void CheckAnswer()
    {
        if (!gameActive) return;

        if (currentMode == GameMode.Hard)
        {
            CheckAnswerHard();
            return;
        }

        Number n1ref = currentMode == GameMode.Medium ? number1_3d : number1;
        Number n2ref = currentMode == GameMode.Medium ? number2_3d : number2;
        Number aref  = currentMode == GameMode.Medium ? answer_3d  : answer;
        PlusMinus pmRef = activePm;

        int n1 = n1ref.GetNumber();
        int n2 = n2ref.GetNumber();
        int a  = aref.GetNumber();
        if (n1 == -1 || n2 == -1 || a == -1) return;

        bool correct = false;
        if (pmRef.IsPlus())  correct = (n1 + n2 == a);
        if (pmRef.IsMinus()) correct = (n1 - n2 == a);

        if (correct)
        {
            if (isArcadeMode)
            {
                gameActive = false;
                Messenger.Broadcast(Message.ArcadeRoundWon);
            }
            else
            {
                Messenger.Broadcast(Message.OnIncrementProgress);
                Messenger.Broadcast(Message.GameWon);
            }
        }
    }

    // ── Generate a new level with a UNIQUE solution ───────────────────────
    void StartNewGame()
    {
        // Show correct equation panel
        bool isEasy = currentMode == GameMode.Easy;
        bool isMed  = currentMode == GameMode.Medium;
        bool isHard = currentMode == GameMode.Hard;
        if (eqPanel2d != null)   eqPanel2d.SetActive(isEasy);
        if (eqPanel3d != null)   eqPanel3d.SetActive(isMed);
        if (eqPanelHard != null) eqPanelHard.SetActive(isHard);
        if (eqBg2d != null)      eqBg2d.SetActive(isEasy);
        if (eqBg3d != null)      eqBg3d.SetActive(isMed);
        if (eqBgHard != null)    eqBgHard.SetActive(isHard);

        if (currentMode == GameMode.Medium)
        {
            StartNewGame3D();
            return;
        }
        if (currentMode == GameMode.Hard)
        {
            StartNewGameHard();
            return;
        }

        // Try many times to find a puzzle with exactly 1 solution
        for (int attempt = 0; attempt < 200; attempt++)
        {
            int[] solution = GenerateCandidate();
            if (solution == null) continue;

            // Randomly assign as plus or minus
            bool asMinus = Random.Range(0, 2) == 1;

            // Build the 6 hidden-segment indices as [num1d1, num1d2, num2d1, num2d2, ansd1, ansd2]
            int[] hs;
            if (!asMinus)
                hs = new int[] { solution[0], solution[1], solution[2], solution[3], solution[4], solution[5] };
            else
                hs = new int[] { solution[4], solution[5], solution[0], solution[1], solution[2], solution[3] };

            // GenerateCandidate has already proved both readings unique
            if (!asMinus)
                InitializePlus(solution);
            else
                InitializeMinus(solution);
            StoreSolutionFromHS(hs, 2, true);

            plus = !asMinus;
            activePm.ResetToggle();
            gameActive = true;
            return;
        }

        // Fallback — use first valid candidate without strict uniqueness
        int[] fb = GenerateCandidate();
        if (fb != null)
        {
            InitializePlus(fb);
            StoreSolutionFromHS(fb, 2, false);
            plus = true;
            activePm.ResetToggle();
            gameActive = true;
        }
    }

    // ── Generate a raw candidate solution (hidden segment positions) ──────
    void BuildCandidateTables()
    {
        for (int p = 0; p <= NO_DEAD_SEGMENT; p++)
        {
            var src = LinePositionToNumber[p];
            candAll[p] = src.ToArray();
        }
    }

    /// <summary>
    /// Every digit this slot can be shown as. There is deliberately no
    /// leading-digit variant: Number.GetNumber reads a slot pair as d1*10+d2
    /// with no leading-zero rule, so the game accepts 05 + 12 = 17 as a win.
    /// Excluding zero here proved uniqueness against a rule the game does not
    /// enforce — 19% of Easy and 20% of Medium boards certified unique that way
    /// still had a second, leading-zero answer a player could actually enter.
    /// </summary>
    int[] Cand(int pos) => candAll[pos];

    /// <summary>
    /// Counts complete solutions for a two-digit layout — operands AND answer —
    /// over BOTH operators, stopping once it reaches cap.
    ///
    /// The old generator only counted operand pairs per sum and assumed the
    /// answer was then forced. It is not: the answer digits have candidates of
    /// their own, so a different operand pair reaching a different sum the
    /// answer can also spell is an equally valid solution. That is why 81% of
    /// generated puzzles had more than one right answer, some as many as 99.
    /// CheckAnswer accepts either operator, so both must be counted here too.
    /// </summary>
    int CountSolutions2(int s0, int s1, int s2, int s3, int s4, int s5, int cap)
    {
        var c0 = Cand(s0);
        var c1 = Cand(s1);
        var c2 = Cand(s2);
        var c3 = Cand(s3);

        System.Array.Clear(okHi, 0, 10);
        System.Array.Clear(okLo, 0, 10);
        foreach (var d in Cand(s4))  okHi[d] = true;
        foreach (var d in Cand(s5)) okLo[d] = true;

        int n = 0;

        for (int i = 0; i < c0.Length; i++)
        for (int j = 0; j < c1.Length; j++)
        {
            int n1 = c0[i] * 10 + c1[j];

            for (int k = 0; k < c2.Length; k++)
            for (int l = 0; l < c3.Length; l++)
            {
                int n2 = c2[k] * 10 + c3[l];

                int sum = n1 + n2;
                if (sum >= 0 && sum <= 99 && okHi[sum / 10] && okLo[sum % 10])
                    if (++n >= cap) return n;

                int dif = n1 - n2;
                if (dif >= 0 && dif <= 99 && okHi[dif / 10] && okLo[dif % 10])
                    if (++n >= cap) return n;
            }
        }

        return n;
    }

    /// <summary>
    /// The plus layout shows slots 0..5 in order; InitializeMinus permutes them
    /// so the answer slot becomes the first operand. The player picks the
    /// operator, so a puzzle is only honest if BOTH readings have exactly one
    /// solution.
    /// </summary>
    bool IsUniqueEasy(int[] g)
    {
        if (CountSolutions2(g[0], g[1], g[2], g[3], g[4], g[5], 2) != 1) return false;
        if (CountSolutions2(g[4], g[5], g[0], g[1], g[2], g[3], 2) != 1) return false;
        return true;
    }

    public int[] GenerateCandidate()
    {
        for (int attempt = 0; attempt < 300; attempt++)
        {
            // Start from a real equation rather than from constraints: this way
            // a solution always exists and only uniqueness has to be proven.
            int a = Random.Range(10, 100);
            int b = Random.Range(10, 90);
            int c = a + b;
            if (c > 99) continue;

            int[] digits = { a / 10, a % 10, b / 10, b % 10, c / 10, c % 10 };

            // Tightest board first — it is the one most likely to be unique —
            // then hand the freedom back slot by slot. Same strategy as Medium.
            int[] segs = new int[6];
            for (int i = 0; i < 6; i++) segs[i] = TightestDead(digits[i]);

            if (!IsUniqueEasy(segs)) continue;

            int start = Random.Range(0, 6);
            for (int t = 0; t < 6; t++)
            {
                int i = (start + t) % 6;
                var off = LineNumberToPosition[digits[i]];
                int count = off == null ? 0 : off.Count;

                int best = segs[i];
                int bestSize = Cand(best).Length;

                for (int o = 0; o <= count; o++)
                {
                    int alt = o < count ? off[o] : NO_DEAD_SEGMENT;
                    int size = Cand(alt).Length;
                    if (size <= bestSize) continue;

                    int keep = segs[i];
                    segs[i] = alt;
                    if (IsUniqueEasy(segs)) { best = alt; bestSize = size; }
                    segs[i] = keep;
                }

                segs[i] = best;
            }

            return segs;
        }

        return null;
    }

    void CheckAnswerHard()
    {
        int a = number1_hard.GetNumber();
        int b = number2_hard.GetNumber();
        int c = number3_hard.GetNumber();
        int d = answer_hard.GetNumber();
        if (a == -1 || b == -1 || c == -1 || d == -1) return;

        bool op1Plus = pm_hard1.IsPlus();
        bool op2Plus = pm_hard2.IsPlus();
        if (!op1Plus && !pm_hard1.IsMinus()) return;
        if (!op2Plus && !pm_hard2.IsMinus()) return;

        int result = a;
        result = op1Plus ? result + b : result - b;
        result = op2Plus ? result + c : result - c;

        if (result == d)
        {
            if (isArcadeMode)
            {
                gameActive = false;
                Messenger.Broadcast(Message.ArcadeRoundWon);
            }
            else
            {
                Messenger.Broadcast(Message.OnIncrementProgress);
                Messenger.Broadcast(Message.GameWon);
            }
        }
    }

    // ── Initialize digits ─────────────────────────────────────────────────
    void InitializePlus(int[] solution)
    {
        if (solution.Length < 6) return;
        number1.Initialize((LinePositions)solution[0], (LinePositions)solution[1]);
        number2.Initialize((LinePositions)solution[2], (LinePositions)solution[3]);
        answer.Initialize((LinePositions)solution[4],  (LinePositions)solution[5]);
    }

    void InitializeMinus(int[] solution)
    {
        if (solution.Length < 6) return;
        number1.Initialize((LinePositions)solution[4], (LinePositions)solution[5]);
        number2.Initialize((LinePositions)solution[0], (LinePositions)solution[1]);
        answer.Initialize((LinePositions)solution[2],  (LinePositions)solution[3]);
    }

    // ── Compute correct solution by brute-force from hidden segment info ──
    // Called AFTER Initialize — uses the hidden segment arrays to find valid equation
    void StoreSolutionFromHS(int[] hs, int digitsPerNum, bool checkMinus)
    {
        // hs contains hidden segment positions for all digits
        // For 2-digit: [d1,d2, d3,d4, a1,a2] (6 entries)
        // For 3-digit: [d1,d2,d3, d4,d5,d6, a1,a2,a3] (9 entries)
        int d = digitsPerNum;

        var nums = new List<int>[hs.Length];
        for (int i = 0; i < hs.Length; i++) nums[i] = new List<int>(Cand(hs[i]));

        if (d == 2)
        {
            for (int a = 0; a < nums[0].Count; a++)
            for (int b = 0; b < nums[1].Count; b++)
            for (int c = 0; c < nums[2].Count; c++)
            for (int dd = 0; dd < nums[3].Count; dd++)
            for (int e = 0; e < nums[4].Count; e++)
            for (int f = 0; f < nums[5].Count; f++)
            {
                int n1 = nums[0][a]*10 + nums[1][b];
                int n2 = nums[2][c]*10 + nums[3][dd];
                int ans = nums[4][e]*10 + nums[5][f];
                if (n1 + n2 == ans) { correctSolution = n1 + " + " + n2 + " = " + ans; Debug.Log("Solution: " + correctSolution); return; }
                if (checkMinus && n1 - n2 == ans && n1 >= n2) { correctSolution = n1 + " - " + n2 + " = " + ans; Debug.Log("Solution: " + correctSolution); return; }
            }
        }
        correctSolution = "?";
    }

    // ══════════════════════════════════════════════════════════════════════
    //  Arcade: Initialize from remote equation (Firebase)
    // ══════════════════════════════════════════════════════════════════════

    public void InitializeFromRemote(int[] solution, bool isMinus, GameMode mode)
    {
        currentMode = mode;

        // Show correct equation panel
        bool isEasy = mode == GameMode.Easy;
        bool isMed  = mode == GameMode.Medium;
        bool isHard = mode == GameMode.Hard;
        if (eqPanel2d != null)   eqPanel2d.SetActive(isEasy);
        if (eqPanel3d != null)   eqPanel3d.SetActive(isMed);
        if (eqPanelHard != null) eqPanelHard.SetActive(isHard);
        if (eqBg2d != null)      eqBg2d.SetActive(isEasy);
        if (eqBg3d != null)      eqBg3d.SetActive(isMed);
        if (eqBgHard != null)    eqBgHard.SetActive(isHard);

        if (mode == GameMode.Medium)
        {
            if (!isMinus)
                InitializePlus3D(solution);
            else
                InitializeMinus3D(solution);
            StoreSolution3D(solution, isMinus);
            plus = !isMinus;
            activePm.ResetToggle();
        }
        else if (mode == GameMode.Hard)
        {
            number1_hard.Initialize((LinePositions)solution[0], (LinePositions)solution[1]);
            number2_hard.Initialize((LinePositions)solution[2], (LinePositions)solution[3]);
            number3_hard.Initialize((LinePositions)solution[4], (LinePositions)solution[5]);
            answer_hard.Initialize((LinePositions)solution[6],  (LinePositions)solution[7]);
            StoreSolutionHard(solution);
            pm_hard1.ResetToggle();
            pm_hard2.ResetToggle();
        }
        else // Easy
        {
            if (!isMinus)
                InitializePlus(solution);
            else
                InitializeMinus(solution);

            int[] hs;
            if (!isMinus)
                hs = new int[] { solution[0], solution[1], solution[2], solution[3], solution[4], solution[5] };
            else
                hs = new int[] { solution[4], solution[5], solution[0], solution[1], solution[2], solution[3] };
            StoreSolutionFromHS(hs, 2, true);

            plus = !isMinus;
            activePm.ResetToggle();
        }

        gameActive = true;
    }

    // ══════════════════════════════════════════════════════════════════════
    //  3-DIGIT (Medium mode) puzzle generation
    // ══════════════════════════════════════════════════════════════════════

    void StartNewGame3D()
    {
        for (int attempt = 0; attempt < 300; attempt++)
        {
            int[] solution = GenerateCandidate3D();
            if (solution == null) continue;

            bool asMinus = Random.Range(0, 2) == 1;

            if (!asMinus)
                InitializePlus3D(solution);
            else
                InitializeMinus3D(solution);

            StoreSolution3D(solution, asMinus);
            plus = !asMinus;
            activePm.ResetToggle();
            gameActive = true;
            return;
        }

        // Fallback
        int[] fb = GenerateCandidate3D();
        if (fb != null)
        {
            InitializePlus3D(fb);
            StoreSolution3D(fb, false);
            plus = true;
            activePm.ResetToggle();
            gameActive = true;
        }
    }

    void StoreSolution3D(int[] s, bool isMinus)
    {
        // s = [h1,t1,o1, h2,t2,o2, ha,ta,oa] — all 9 hidden segment positions
        var p = new List<int>[9];
        for (int i = 0; i < 9; i++) p[i] = new List<int>(Cand(s[i]));

        for (int a = 0; a < p[0].Count; a++)
        for (int b = 0; b < p[1].Count; b++)
        for (int c = 0; c < p[2].Count; c++)
        for (int d = 0; d < p[3].Count; d++)
        for (int e = 0; e < p[4].Count; e++)
        for (int f = 0; f < p[5].Count; f++)
        for (int g = 0; g < p[6].Count; g++)
        for (int h = 0; h < p[7].Count; h++)
        for (int i = 0; i < p[8].Count; i++)
        {
            int n1 = p[0][a]*100 + p[1][b]*10 + p[2][c];
            int n2 = p[3][d]*100 + p[4][e]*10 + p[5][f];
            int ans = p[6][g]*100 + p[7][h]*10 + p[8][i];
            if (!isMinus && n1 + n2 == ans)
            {
                correctSolution = n1.ToString("D3") + " + " + n2.ToString("D3") + " = " + ans.ToString("D3");
                Debug.Log("Solution: " + correctSolution); return;
            }
            if (isMinus && n1 - n2 == ans && n1 >= n2)
            {
                correctSolution = n1.ToString("D3") + " - " + n2.ToString("D3") + " = " + ans.ToString("D3");
                Debug.Log("Solution: " + correctSolution); return;
            }
        }
        correctSolution = "?";
    }

    /// <summary>
    /// The dead segment that leaves this digit the fewest alternatives.
    /// Generation starts from the tightest possible board because that is the
    /// one most likely to be unique, then loosens it back — searching from the
    /// loose end almost never finds a unique board at three digits (8.7% of
    /// attempts against 55% this way).
    /// </summary>
    int TightestDead(int digit)
    {
        var off = LineNumberToPosition[digit];
        if (off == null || off.Count == 0) return NO_DEAD_SEGMENT;

        int best = off[0];
        int bestSize = Cand(best).Length;

        for (int i = 1; i < off.Count; i++)
        {
            int size = Cand(off[i]).Length;
            if (size < bestSize) { best = off[i]; bestSize = size; }
        }
        return best;
    }

    void BuildValues3(int s0, int s1, int s2, List<int> into)
    {
        into.Clear();
        var c0 = Cand(s0);
        var c1 = Cand(s1);
        var c2 = Cand(s2);

        for (int i = 0; i < c0.Length; i++)
        for (int j = 0; j < c1.Length; j++)
        for (int k = 0; k < c2.Length; k++)
            into.Add(c0[i] * 100 + c1[j] * 10 + c2[k]);
    }

    /// <summary>
    /// Complete solutions for a three-digit layout, both operators.
    /// Returns -1 when the board is too loose to score cheaply; the caller
    /// treats that as "not unique" and moves on rather than stalling a round.
    /// </summary>
    int CountSolutions3(int a0, int a1, int a2, int b0, int b1, int b2,
                        int r0, int r1, int r2, int cap)
    {
        BuildValues3(a0, a1, a2, vals1);
        BuildValues3(b0, b1, b2, vals2);

        if ((long)vals1.Count * vals2.Count > 400000) return -1;

        System.Array.Clear(okHi, 0, 10);
        System.Array.Clear(okMid, 0, 10);
        System.Array.Clear(okLo, 0, 10);
        foreach (var d in Cand(r0))  okHi[d]  = true;
        foreach (var d in Cand(r1)) okMid[d] = true;
        foreach (var d in Cand(r2)) okLo[d]  = true;

        int n = 0;

        for (int i = 0; i < vals1.Count; i++)
        {
            int v1 = vals1[i];

            for (int j = 0; j < vals2.Count; j++)
            {
                int v2 = vals2[j];

                int sum = v1 + v2;
                if (sum >= 0 && sum <= 999 && okHi[sum / 100] && okMid[(sum / 10) % 10] && okLo[sum % 10])
                    if (++n >= cap) return n;

                int dif = v1 - v2;
                if (dif >= 0 && dif <= 999 && okHi[dif / 100] && okMid[(dif / 10) % 10] && okLo[dif % 10])
                    if (++n >= cap) return n;
            }
        }

        return n;
    }

    /// <summary>Both readings of the board, exactly as InitializeMinus3D permutes them.</summary>
    bool IsUniqueMedium(int[] g)
    {
        if (CountSolutions3(g[0], g[1], g[2], g[3], g[4], g[5], g[6], g[7], g[8], 2) != 1) return false;
        if (CountSolutions3(g[6], g[7], g[8], g[0], g[1], g[2], g[3], g[4], g[5], 2) != 1) return false;
        return true;
    }


    public int[] GenerateCandidate3D()
    {
        for (int attempt = 0; attempt < 60; attempt++)
        {
            int a = Random.Range(100, 900);
            int b = Random.Range(100, 900);
            int c = a + b;
            if (c > 999) continue;

            int[] digits =
            {
                a / 100, (a / 10) % 10, a % 10,
                b / 100, (b / 10) % 10, b % 10,
                c / 100, (c / 10) % 10, c % 10,
            };

            int[] segs = new int[9];
            for (int i = 0; i < 9; i++) segs[i] = TightestDead(digits[i]);

            if (!IsUniqueMedium(segs)) continue;

            // Give every slot back as much freedom as uniqueness will bear.
            // Without this pass each digit is pinned to a single candidate and
            // the board reads itself out rather than being solved.
            int start = Random.Range(0, 9);
            for (int t = 0; t < 9; t++)
            {
                int i = (start + t) % 9;
                var off = LineNumberToPosition[digits[i]];
                int count = off == null ? 0 : off.Count;

                int best = segs[i];
                int bestSize = Cand(best).Length;

                // every dead segment this digit allows, and finally none at all
                for (int o = 0; o <= count; o++)
                {
                    int alt = o < count ? off[o] : NO_DEAD_SEGMENT;
                    int size = Cand(alt).Length;
                    if (size <= bestSize) continue;

                    int keep = segs[i];
                    segs[i] = alt;
                    if (IsUniqueMedium(segs)) { best = alt; bestSize = size; }
                    segs[i] = keep;
                }

                segs[i] = best;
            }

            return segs;
        }

        return null;
    }

    void InitializePlus3D(int[] s)
    {
        if (s.Length < 9) return;
        number1_3d.Initialize((LinePositions)s[0], (LinePositions)s[1], (LinePositions)s[2]);
        number2_3d.Initialize((LinePositions)s[3], (LinePositions)s[4], (LinePositions)s[5]);
        answer_3d.Initialize((LinePositions)s[6],  (LinePositions)s[7], (LinePositions)s[8]);
    }

    void InitializeMinus3D(int[] s)
    {
        if (s.Length < 9) return;
        number1_3d.Initialize((LinePositions)s[6], (LinePositions)s[7], (LinePositions)s[8]);
        number2_3d.Initialize((LinePositions)s[0], (LinePositions)s[1], (LinePositions)s[2]);
        answer_3d.Initialize((LinePositions)s[3],  (LinePositions)s[4], (LinePositions)s[5]);
    }

    // ══════════════════════════════════════════════════════════════════════
    //  HARD MODE: A ± B ± C = D  (3 numbers, 2 operators, 2 digits each)
    // ══════════════════════════════════════════════════════════════════════

    void StartNewGameHard()
    {
        for (int attempt = 0; attempt < 300; attempt++)
        {
            int[] solution = GenerateCandidateHard();
            if (solution == null) continue;

            // solution = [a1,a2, b1,b2, c1,c2, d1,d2, op1(0=+,1=-), op2(0=+,1=-)]
            number1_hard.Initialize((LinePositions)solution[0], (LinePositions)solution[1]);
            number2_hard.Initialize((LinePositions)solution[2], (LinePositions)solution[3]);
            number3_hard.Initialize((LinePositions)solution[4], (LinePositions)solution[5]);
            answer_hard.Initialize((LinePositions)solution[6],  (LinePositions)solution[7]);

            StoreSolutionHard(solution);
            pm_hard1.ResetToggle();
            pm_hard2.ResetToggle();
            gameActive = true;
            return;
        }

        // Fallback
        int[] fb = GenerateCandidateHard();
        if (fb != null)
        {
            number1_hard.Initialize((LinePositions)fb[0], (LinePositions)fb[1]);
            number2_hard.Initialize((LinePositions)fb[2], (LinePositions)fb[3]);
            number3_hard.Initialize((LinePositions)fb[4], (LinePositions)fb[5]);
            answer_hard.Initialize((LinePositions)fb[6],  (LinePositions)fb[7]);
            StoreSolutionHard(fb);
            pm_hard1.ResetToggle();
            pm_hard2.ResetToggle();
            gameActive = true;
        }
    }

    void StoreSolutionHard(int[] hs)
    {
        var p = new List<int>[8];
        for (int i = 0; i < 8; i++) p[i] = new List<int>(Cand(hs[i]));

        string[] op1s = { "+", "+", "-", "-" };
        string[] op2s = { "+", "-", "+", "-" };

        for (int a0 = 0; a0 < p[0].Count; a0++)
        for (int a1 = 0; a1 < p[1].Count; a1++)
        for (int b0 = 0; b0 < p[2].Count; b0++)
        for (int b1 = 0; b1 < p[3].Count; b1++)
        for (int c0 = 0; c0 < p[4].Count; c0++)
        for (int c1 = 0; c1 < p[5].Count; c1++)
        for (int d0 = 0; d0 < p[6].Count; d0++)
        for (int d1 = 0; d1 < p[7].Count; d1++)
        {
            int A = p[0][a0]*10 + p[1][a1];
            int B = p[2][b0]*10 + p[3][b1];
            int C = p[4][c0]*10 + p[5][c1];
            int D = p[6][d0]*10 + p[7][d1];

            int[] results = { A+B+C, A+B-C, A-B+C, A-B-C };
            for (int o = 0; o < 4; o++)
            {
                if (results[o] != D) continue;

                correctSolution = A.ToString("D2") + " " + op1s[o] + " " + B.ToString("D2") +
                                  " " + op2s[o] + " " + C.ToString("D2") + " = " + D.ToString("D2");
                Debug.Log("Solution: " + correctSolution);
                return;
            }
        }
        correctSolution = "?";
    }

    void BuildValues2(int s0, int s1, List<int> into)
    {
        into.Clear();
        var c0 = Cand(s0);
        var c1 = Cand(s1);

        for (int i = 0; i < c0.Length; i++)
        for (int j = 0; j < c1.Length; j++)
            into.Add(c0[i] * 10 + c1[j]);
    }

    // A ± B lands anywhere in -99..198, so the tally is offset by 99.
    const int SAB_OFFSET = 99;
    readonly int[] sab = new int[SAB_OFFSET + 198 + 1];

    /// <summary>
    /// Complete solutions for A ± B ± C = D, over all four operator pairings,
    /// stopping once it reaches cap.
    ///
    /// Enumerating all four numbers directly is a hundred million combinations.
    /// Instead this tallies how many (A, B, op1) triples reach each value of
    /// A ± B, then walks D and C: the equation holds exactly when A ± B equals
    /// D - C or D + C, so each (D, C, op2) pair contributes the whole tally
    /// sitting at that value. Forty thousand steps instead.
    /// </summary>
    int CountSolutionsHard(int[] g, int cap)
    {
        BuildValues2(g[0], g[1], vals1);   // A
        BuildValues2(g[2], g[3], vals2);   // B

        System.Array.Clear(sab, 0, sab.Length);
        for (int i = 0; i < vals1.Count; i++)
        {
            int a = vals1[i];
            for (int j = 0; j < vals2.Count; j++)
            {
                sab[a + vals2[j] + SAB_OFFSET]++;
                sab[a - vals2[j] + SAB_OFFSET]++;
            }
        }

        BuildValues2(g[4], g[5], vals1);   // C — A is no longer needed
        BuildValues2(g[6], g[7], vals2);   // D

        int n = 0;

        for (int i = 0; i < vals2.Count; i++)
        {
            int d = vals2[i];

            for (int j = 0; j < vals1.Count; j++)
            {
                int c = vals1[j];
                n += sab[d - c + SAB_OFFSET];   // op2 was plus
                n += sab[d + c + SAB_OFFSET];   // op2 was minus
            }

            if (n >= cap) return n;
        }

        return n;
    }

    /// <summary>Hard shows its slots in one fixed order, so there is one reading to check.</summary>
    bool IsUniqueHard(int[] g) => CountSolutionsHard(g, 2) == 1;

    /// <summary>
    /// Hard is far harder to make unique than Easy or Medium: three operands and
    /// four operator pairings give roughly a thousand readings of a board against
    /// only a handful of spellable answers, so even the tightest board is unique
    /// for barely 0.3% of equations — and it is the equation that decides it, not
    /// the segment assignment (trying thirty other assignments per equation moved
    /// that to 0.4%). So this simply tries a lot of equations. Each rejection
    /// costs one cheap count, and four thousand attempts land a board every time.
    /// </summary>
    public int[] GenerateCandidateHard()
    {
        int[] segs = new int[8];

        for (int attempt = 0; attempt < 4000; attempt++)
        {
            int A = Random.Range(10, 100);
            int B = Random.Range(10, 100);
            int C = Random.Range(10, 100);

            bool op1Plus = Random.Range(0, 2) == 0;
            bool op2Plus = Random.Range(0, 2) == 0;

            int D = op1Plus ? A + B : A - B;
            D = op2Plus ? D + C : D - C;
            if (D < 0 || D > 99) continue;

            int[] digits = { A / 10, A % 10, B / 10, B % 10, C / 10, C % 10, D / 10, D % 10 };

            for (int i = 0; i < 8; i++) segs[i] = TightestDead(digits[i]);
            if (!IsUniqueHard(segs)) continue;

            // Same loosening pass as the other two modes — give each slot back
            // as much freedom as uniqueness will bear.
            int start = Random.Range(0, 8);
            for (int t = 0; t < 8; t++)
            {
                int i = (start + t) % 8;
                var off = LineNumberToPosition[digits[i]];
                int count = off == null ? 0 : off.Count;

                int best = segs[i];
                int bestSize = Cand(best).Length;

                for (int o = 0; o <= count; o++)
                {
                    int alt = o < count ? off[o] : NO_DEAD_SEGMENT;
                    int size = Cand(alt).Length;
                    if (size <= bestSize) continue;

                    int keep = segs[i];
                    segs[i] = alt;
                    if (IsUniqueHard(segs)) { best = alt; bestSize = size; }
                    segs[i] = keep;
                }

                segs[i] = best;
            }

            return (int[])segs.Clone();
        }

        return null;
    }
}
