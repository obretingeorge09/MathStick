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

    int[] PossibleAnswers = new int[2000];

    void Awake()
    {
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

    List<int>[] LinePositionToNumber = new List<int>[(int)LinePositions.EnumLength];
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

            if (!HasUniqueSolution(hs)) continue;

            // Good — initialize the puzzle
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

    // ── Brute-force uniqueness check ──────────────────────────────────────
    // Given hidden segments for [num1d1, num1d2, num2d1, num2d2, ansd1, ansd2],
    // enumerate ALL possible digit combos × both operations.
    // Returns true only if exactly ONE (digits, operation) combo is valid.
    bool HasUniqueSolution(int[] hs)
    {
        var p0 = LinePositionToNumber[hs[0]];
        var p1 = LinePositionToNumber[hs[1]];
        var p2 = LinePositionToNumber[hs[2]];
        var p3 = LinePositionToNumber[hs[3]];
        var p4 = LinePositionToNumber[hs[4]];
        var p5 = LinePositionToNumber[hs[5]];

        int count = 0;

        for (int a = 0; a < p0.Count; a++)
        for (int b = 0; b < p1.Count; b++)
        for (int c = 0; c < p2.Count; c++)
        for (int d = 0; d < p3.Count; d++)
        for (int e = 0; e < p4.Count; e++)
        for (int f = 0; f < p5.Count; f++)
        {
            int n1  = p0[a] * 10 + p1[b];
            int n2  = p2[c] * 10 + p3[d];
            int ans = p4[e] * 10 + p5[f];

            if (n1 + n2 == ans) count++;
            if (n1 - n2 == ans && n1 - n2 >= 0) count++;

            if (count > 1) return false;
        }

        return count == 1;
    }

    // ── Generate a raw candidate solution (hidden segment positions) ──────
    public int[] GenerateCandidate()
    {
        int[] solution = new int[6];

        solution[0] = Random.Range(0, (int)LinePositions.EnumLength - 1);
        solution[1] = Random.Range(0, (int)LinePositions.EnumLength);
        solution[2] = Random.Range(0, (int)LinePositions.EnumLength - 1);
        solution[3] = Random.Range(0, (int)LinePositions.EnumLength);

        if (solution[0] == (int)LinePositions.Middle)
            solution[0] = (int)LinePositions.EnumLength - 1;
        if (solution[2] == (int)LinePositions.Middle)
            solution[2] = (int)LinePositions.EnumLength - 1;

        if (solution[0] == (int)LinePositions.TopRight && solution[2] == (int)LinePositions.TopRight)
        {
            if (Random.Range(0, 2) == 0)
                solution[0] = (int)LinePositions.EnumLength - 1;
            else
                solution[2] = (int)LinePositions.EnumLength - 1;
        }

        // Count possible sums
        for (int i = 0; i < PossibleAnswers.Length; i++)
            PossibleAnswers[i] = 0;

        for (int i = 0; i < LinePositionToNumber[solution[0]].Count; i++)
        for (int j = 0; j < LinePositionToNumber[solution[1]].Count; j++)
        for (int k = 0; k < LinePositionToNumber[solution[2]].Count; k++)
        for (int l = 0; l < LinePositionToNumber[solution[3]].Count; l++)
        {
            int d1 = LinePositionToNumber[solution[0]][i];
            int d2 = LinePositionToNumber[solution[1]][j];
            int d3 = LinePositionToNumber[solution[2]][k];
            int d4 = LinePositionToNumber[solution[3]][l];
            int sum = d1 * 10 + d2 + d3 * 10 + d4;
            if (sum < PossibleAnswers.Length)
                PossibleAnswers[sum]++;
        }

        // Collect sums that have exactly 1 way to be formed, excluding digit 8
        List<int> validSums = new List<int>();
        for (int i = 10; i < PossibleAnswers.Length; i++)
        {
            if (PossibleAnswers[i] == 1 && i / 10 != 8 && i % 10 != 8)
                validSums.Add(i);
        }

        if (validSums.Count == 0) return null;

        // Pick a random valid sum and find answer hidden segments
        int targetSum = validSums[Random.Range(0, validSums.Count)];
        int sd1 = targetSum / 10;
        int sd2 = targetSum % 10;

        // Skip if digits don't have valid positions (e.g., digit 8)
        if (sd1 < 0 || sd1 >= LineNumberToPosition.Length || LineNumberToPosition[sd1] == null || LineNumberToPosition[sd1].Count == 0)
            return null;
        if (sd2 < 0 || sd2 >= LineNumberToPosition.Length || LineNumberToPosition[sd2] == null || LineNumberToPosition[sd2].Count == 0)
            return null;

        // Find hidden segment combos for the answer digits
        List<int> answerCombos = new List<int>();
        for (int j = 0; j < LineNumberToPosition[sd1].Count; j++)
        for (int k = 0; k < LineNumberToPosition[sd2].Count; k++)
        {
            int l1 = LineNumberToPosition[sd1][j];
            int l2 = LineNumberToPosition[sd2][k];

            // Check this combo only allows one valid answer number
            int ansCount = 0;
            for (int a = 0; a < LinePositionToNumber[l1].Count; a++)
            for (int b = 0; b < LinePositionToNumber[l2].Count; b++)
            {
                int possibleAns = LinePositionToNumber[l1][a] * 10 + LinePositionToNumber[l2][b];
                if (PossibleAnswers[possibleAns] > 0)
                    ansCount++;
            }

            if (ansCount == 1)
                answerCombos.Add(l1 * 100 + l2);
        }

        if (answerCombos.Count == 0) return null;

        int pick = answerCombos[Random.Range(0, answerCombos.Count)];
        solution[4] = pick / 100;
        solution[5] = pick % 100;

        return solution;
    }

    // ── Check answer (Hard mode: A op1 B op2 C = D) ────────────────────────
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
        for (int i = 0; i < hs.Length; i++)
            nums[i] = LinePositionToNumber[hs[i]];

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
                if (checkMinus && n1 - n2 == ans && n1 - n2 >= 0) { correctSolution = n1 + " - " + n2 + " = " + ans; Debug.Log("Solution: " + correctSolution); return; }
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
        for (int i = 0; i < 9; i++) p[i] = LinePositionToNumber[s[i]];

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

    public int[] GenerateCandidate3D()
    {
        int[] solution = new int[9]; // [h1,t1,o1, h2,t2,o2, ha,ta,oa]
        int enumLen = (int)LinePositions.EnumLength;

        // Pick random hidden segments for num1 (3 digits) and num2 (3 digits)
        // Hundreds digit CAN be Middle (hidden=Middle → digit becomes 0)
        for (int i = 0; i < 6; i++)
        {
            solution[i] = Random.Range(0, enumLen);
        }

        // Count possible sums
        for (int i = 0; i < PossibleAnswers.Length; i++)
            PossibleAnswers[i] = 0;

        var p0 = LinePositionToNumber[solution[0]];
        var p1 = LinePositionToNumber[solution[1]];
        var p2 = LinePositionToNumber[solution[2]];
        var p3 = LinePositionToNumber[solution[3]];
        var p4 = LinePositionToNumber[solution[4]];
        var p5 = LinePositionToNumber[solution[5]];

        for (int a = 0; a < p0.Count; a++)
        for (int b = 0; b < p1.Count; b++)
        for (int c = 0; c < p2.Count; c++)
        for (int d = 0; d < p3.Count; d++)
        for (int e = 0; e < p4.Count; e++)
        for (int f = 0; f < p5.Count; f++)
        {
            int n1 = p0[a] * 100 + p1[b] * 10 + p2[c];
            int n2 = p3[d] * 100 + p4[e] * 10 + p5[f];
            int sum = n1 + n2;
            if (sum >= 0 && sum < PossibleAnswers.Length)
                PossibleAnswers[sum]++;
        }

        // Collect sums with exactly 1 way, excluding digit 8, max 999
        List<int> validSums = new List<int>();
        for (int i = 0; i <= 999 && i < PossibleAnswers.Length; i++)
        {
            if (PossibleAnswers[i] == 1)
            {
                int h = i / 100, t = (i / 10) % 10, o = i % 10;
                if (h != 8 && t != 8 && o != 8)
                    validSums.Add(i);
            }
        }

        if (validSums.Count == 0) return null;

        int targetSum = validSums[Random.Range(0, validSums.Count)];
        int sd0 = targetSum / 100;
        int sd1 = (targetSum / 10) % 10;
        int sd2 = targetSum % 10;

        if (LineNumberToPosition[sd0].Count == 0 ||
            LineNumberToPosition[sd1].Count == 0 ||
            LineNumberToPosition[sd2].Count == 0)
            return null;

        // Find hidden segment combos for answer
        List<int[]> answerCombos = new List<int[]>();
        for (int i = 0; i < LineNumberToPosition[sd0].Count; i++)
        for (int j = 0; j < LineNumberToPosition[sd1].Count; j++)
        for (int k = 0; k < LineNumberToPosition[sd2].Count; k++)
        {
            int l0 = LineNumberToPosition[sd0][i];
            int l1 = LineNumberToPosition[sd1][j];
            int l2 = LineNumberToPosition[sd2][k];

            // Check this combo only allows one valid answer
            int ansCount = 0;
            for (int a = 0; a < LinePositionToNumber[l0].Count; a++)
            for (int b = 0; b < LinePositionToNumber[l1].Count; b++)
            for (int c = 0; c < LinePositionToNumber[l2].Count; c++)
            {
                int possibleAns = LinePositionToNumber[l0][a] * 100 +
                                  LinePositionToNumber[l1][b] * 10 +
                                  LinePositionToNumber[l2][c];
                if (possibleAns < PossibleAnswers.Length && PossibleAnswers[possibleAns] > 0)
                    ansCount++;
            }

            if (ansCount == 1)
                answerCombos.Add(new int[] { l0, l1, l2 });
        }

        if (answerCombos.Count == 0) return null;

        int[] pick = answerCombos[Random.Range(0, answerCombos.Count)];
        solution[6] = pick[0];
        solution[7] = pick[1];
        solution[8] = pick[2];

        return solution;
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
        for (int i = 0; i < 8; i++) p[i] = LinePositionToNumber[hs[i]];

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
                if (results[o] == D && D >= 0)
                {
                    correctSolution = A + " " + op1s[o] + " " + B + " " + op2s[o] + " " + C + " = " + D;
                    Debug.Log("Solution: " + correctSolution);
                    return;
                }
            }
        }
        correctSolution = "?";
    }

    public int[] GenerateCandidateHard()
    {
        int enumLen = (int)LinePositions.EnumLength;

        // Pick hidden segments for A (2d), B (2d), C (2d)
        int[] hs = new int[6];
        for (int i = 0; i < 6; i++)
        {
            hs[i] = Random.Range(0, enumLen - 1);
            if (hs[i] == (int)LinePositions.Middle)
                hs[i] = enumLen - 1;
        }

        var pA0 = LinePositionToNumber[hs[0]];
        var pA1 = LinePositionToNumber[hs[1]];
        var pB0 = LinePositionToNumber[hs[2]];
        var pB1 = LinePositionToNumber[hs[3]];
        var pC0 = LinePositionToNumber[hs[4]];
        var pC1 = LinePositionToNumber[hs[5]];

        // Try all combos of digits and both operators (4 combos: ++, +-, -+, --)
        // For each combo, compute result and collect valid ones
        for (int i = 0; i < PossibleAnswers.Length; i++)
            PossibleAnswers[i] = 0;

        // We store results indexed by: result * 4 + opCombo
        // But simpler: just count how many (digit,op) combos produce each result
        // We need results 10..99 (2-digit)
        // Count per (result, opCombo) pair
        // opCombo: 0=++, 1=+-, 2=-+, 3=--
        int[,] resultCounts = new int[200, 4];

        for (int a0 = 0; a0 < pA0.Count; a0++)
        for (int a1 = 0; a1 < pA1.Count; a1++)
        for (int b0 = 0; b0 < pB0.Count; b0++)
        for (int b1 = 0; b1 < pB1.Count; b1++)
        for (int c0 = 0; c0 < pC0.Count; c0++)
        for (int c1 = 0; c1 < pC1.Count; c1++)
        {
            int A = pA0[a0] * 10 + pA1[a1];
            int B = pB0[b0] * 10 + pB1[b1];
            int C = pC0[c0] * 10 + pC1[c1];

            int[] results = {
                A + B + C,  // op 0: ++
                A + B - C,  // op 1: +-
                A - B + C,  // op 2: -+
                A - B - C   // op 3: --
            };

            for (int op = 0; op < 4; op++)
            {
                int r = results[op];
                if (r >= 10 && r < 200)
                    resultCounts[r, op]++;
            }
        }

        // Find (result, opCombo) pairs with exactly 1 digit combination, no digit 8
        List<int> validResults = new List<int>();
        List<int> validOps = new List<int>();
        for (int r = 10; r < 100; r++)
        {
            int rd1 = r / 10, rd2 = r % 10;
            if (rd1 == 8 || rd2 == 8) continue;

            for (int op = 0; op < 4; op++)
            {
                if (resultCounts[r, op] == 1)
                {
                    validResults.Add(r);
                    validOps.Add(op);
                }
            }
        }

        if (validResults.Count == 0) return null;

        int pick = Random.Range(0, validResults.Count);
        int targetResult = validResults[pick];
        int targetOp = validOps[pick];

        int td1 = targetResult / 10;
        int td2 = targetResult % 10;

        if (LineNumberToPosition[td1].Count == 0 || LineNumberToPosition[td2].Count == 0)
            return null;

        // Find answer hidden segments
        List<int> ansCombos = new List<int>();
        for (int j = 0; j < LineNumberToPosition[td1].Count; j++)
        for (int k = 0; k < LineNumberToPosition[td2].Count; k++)
        {
            int l1 = LineNumberToPosition[td1][j];
            int l2 = LineNumberToPosition[td2][k];

            int ansCount = 0;
            for (int a = 0; a < LinePositionToNumber[l1].Count; a++)
            for (int b = 0; b < LinePositionToNumber[l2].Count; b++)
            {
                int possAns = LinePositionToNumber[l1][a] * 10 + LinePositionToNumber[l2][b];
                if (possAns < 200 && resultCounts[possAns, targetOp] > 0)
                    ansCount++;
            }

            if (ansCount == 1)
                ansCombos.Add(l1 * 100 + l2);
        }

        if (ansCombos.Count == 0) return null;

        int ansPick = ansCombos[Random.Range(0, ansCombos.Count)];

        // Return [a1,a2, b1,b2, c1,c2, ansD1,ansD2, op]
        // op is stored but not used for initialization (player must figure it out)
        return new int[] {
            hs[0], hs[1], hs[2], hs[3], hs[4], hs[5],
            ansPick / 100, ansPick % 100
        };
    }
}
