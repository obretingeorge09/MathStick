# MathStick — Core gameplay — the puzzle itself

> **Generated file — do not edit.** Regenerated from the sources listed below.
> The code in `Assets/` is the only source of truth; this exists so the whole
> project can be handed to a tool that reads documents rather than a repo.

> Puzzle generation, answer checking, the seven-segment display, the operator and the clock.

---

## `Assets/Scripts/GameManager.cs`

```csharp
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
```

## `Assets/Scripts/Line.cs`

```csharp
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;

public class Line : MonoBehaviour, IPointerClickHandler
{
    // ── State ──────────────────────────────────────────────────────────────
    enum SegState { Selected, Active, Inactive }
    SegState state = SegState.Active;

    // ── Visuals ────────────────────────────────────────────────────────────
    Image body   = null;  // segment rectangle
    Image shadow = null;  // dark offset duplicate for depth
    Image glow   = null;  // outer halo (larger rect behind body, semi-transparent when selected)

    // ── Palette — reads from GameSettings if available ────────────────
    static Color SegSelected {
        get {
            var gs = GameSettings.Instance;
            return gs != null ? gs.SelectedSegColor : new Color(0.75f, 1.00f, 0.15f, 1f);
        }
    }
    static Color SegActive {
        get {
            var gs = GameSettings.Instance;
            return gs != null ? gs.ActiveSegColor : new Color(0.72f, 0.45f, 0.03f, 1f);
        }
    }
    static Color SegInactive {
        get {
            var gs = GameSettings.Instance;
            return gs != null ? gs.InactiveSegColor : new Color(0.10f, 0.06f, 0.01f, 1f);
        }
    }
    static Color GlowOn {
        get {
            var gs = GameSettings.Instance;
            return gs != null ? gs.GlowColor : new Color(0.70f, 1.00f, 0.10f, 0.45f);
        }
    }
    static readonly Color GlowOff     = new Color(0f, 0f, 0f, 0f);
    static readonly Color ShadowOn    = new Color(0f, 0f, 0f, 0.25f);
    static readonly Color ShadowInactive = new Color(0f, 0f, 0f, 0.10f);
    const float GLOW_PAD = 50f;

    // ── Sprite cache ───────────────────────────────────────────────────────
    static Sprite s_hSeg;   // horizontal segment sprite
    static Sprite s_vSeg;   // vertical segment sprite
    static Sprite s_hGlow;  // horizontal glow sprite (blurred)
    static Sprite s_vGlow;  // vertical glow sprite (blurred)

    // ── Setup ──────────────────────────────────────────────────────────────
    void Awake()
    {
        body = GetComponent<Image>();

        var rt = GetComponent<RectTransform>();

        // Apply beveled segment sprite (authentic 7-segment display look)
        bool horiz = rt.sizeDelta.x > rt.sizeDelta.y;
        if (horiz)
            body.sprite = s_hSeg != null ? s_hSeg : (s_hSeg = MakeBeveledSprite(
                Mathf.RoundToInt(rt.sizeDelta.x), Mathf.RoundToInt(rt.sizeDelta.y)));
        else
            body.sprite = s_vSeg != null ? s_vSeg : (s_vSeg = MakeBeveledSprite(
                Mathf.RoundToInt(rt.sizeDelta.x), Mathf.RoundToInt(rt.sizeDelta.y)));

        Messenger.AddListener(Message.GameWon,      OnGameWon);
        Messenger.AddListener(Message.StartNewGame, OnNewGame);

        // Glow — blurred larger sprite behind everything for diffuse light emission
        Vector2 glowSize = rt.sizeDelta + new Vector2(GLOW_PAD * 2, GLOW_PAD * 2);
        glow = MakeRect("Glow", Vector2.zero, glowSize, GlowOff);
        glow.sprite = horiz
            ? (s_hGlow != null ? s_hGlow : (s_hGlow = MakeGlowSprite(Mathf.RoundToInt(glowSize.x), Mathf.RoundToInt(glowSize.y), true)))
            : (s_vGlow != null ? s_vGlow : (s_vGlow = MakeGlowSprite(Mathf.RoundToInt(glowSize.x), Mathf.RoundToInt(glowSize.y), false)));
        glow.transform.SetAsFirstSibling(); // behind everything

        // Shadow — dark offset copy behind body
        shadow = MakeRect("Shadow", new Vector2(2f, -2f), rt.sizeDelta, ShadowOn);
        shadow.transform.SetSiblingIndex(1); // between glow and body

        // Apply initial state (Active by default, will be overridden by Initialize if needed)
        ApplyState();
    }

    // ── Click ──────────────────────────────────────────────────────────────
    public void OnPointerClick(PointerEventData eventData)
    {
        if (state == SegState.Selected)
        {
            SetActive();
            AudioManager.Instance?.PlaySFX(CreateClickSound());
            Messenger.Broadcast(Message.CheckForSolution);
        }
        else if (state == SegState.Active)
        {
            SetSelected();
            AudioManager.Instance?.PlaySFX(CreateClickSound());
            Messenger.Broadcast(Message.CheckForSolution);
        }
    }

    // Generate simple click sound procedurally
    AudioClip CreateClickSound()
    {
        const int sampleRate = 44100;
        const float duration = 0.1f;
        int samples = (int)(sampleRate * duration);
        AudioClip clip = AudioClip.Create("click", samples, 1, sampleRate, false);
        float[] data = new float[samples];

        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)sampleRate;
            float frequency = 800f * Mathf.Exp(-t * 10f); // frequency decay
            data[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * (1f - t / duration);
        }

        clip.SetData(data, 0);
        return clip;
    }

    // ── State API ──────────────────────────────────────────────────────────
    public void SetSelected()
    {
        state = SegState.Selected;
        StopAllCoroutines();
        Apply(SegSelected, GlowOn, ShadowOn);
        StartCoroutine(Bounce());
    }

    public void SetActive()
    {
        state = SegState.Active;
        StopAllCoroutines();
        transform.localScale = Vector3.one;  // reset scale when deselecting
        Apply(SegActive, GlowOff, ShadowOn);
    }

    public void SetInactive()
    {
        state = SegState.Inactive;
        StopAllCoroutines();
        Apply(SegInactive, GlowOff, ShadowInactive);
    }

    public bool IsSelected() => state == SegState.Selected;
    public bool IsActive()   => state == SegState.Active;
    public bool IsInactive() => state == SegState.Inactive;

    // ── Events ─────────────────────────────────────────────────────────────
    void OnGameWon()  { if (state == SegState.Selected) StartCoroutine(Phosphor()); }
    void OnNewGame()  { StopAllCoroutines(); ApplyState(); }

    // ── Apply colours ──────────────────────────────────────────────────────
    void Apply(Color seg, Color glw, Color shd)
    {
        if (body)   body.color   = seg;
        if (glow)   glow.color   = glw;
        if (shadow) shadow.color = shd;
        ResetScale();
    }

    void ApplyState()
    {
        switch (state)
        {
            case SegState.Selected: Apply(SegSelected, GlowOn, ShadowOn);        break;
            case SegState.Active:   Apply(SegActive,   GlowOff, ShadowOn);       break;
            case SegState.Inactive: Apply(SegInactive, GlowOff, ShadowInactive); break;
        }
    }

    void ResetScale()
    {
        transform.localScale = Vector3.one;
    }

    // ── Animations ─────────────────────────────────────────────────────────
    IEnumerator Bounce()
    {
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / 0.16f;
            transform.localScale = Vector3.one * (1f + 0.20f * Mathf.Sin(t * Mathf.PI));
            yield return null;
        }
        // End at 1.08 scale for selected state (stay slightly larger)
        transform.localScale = Vector3.one * 1.08f;
    }

    IEnumerator Phosphor()
    {
        float t = 0f;
        while (true)
        {
            t += Time.deltaTime;
            float pulse = 0.7f + 0.3f * Mathf.Sin(t * 6f);
            if (body)
                body.color = new Color(
                    SegSelected.r * (0.8f + 0.2f * pulse),
                    SegSelected.g * pulse,
                    SegSelected.b * (0.2f + 0.8f * pulse));
            if (glow)
                glow.color = new Color(GlowOn.r, GlowOn.g, GlowOn.b, GlowOn.a * pulse);
            yield return null;
        }
    }

    // ── Child creation helpers ─────────────────────────────────────────────
    Image MakeRect(string n, Vector2 offset, Vector2 sz, Color col)
    {
        var go = new GameObject(n);
        go.transform.SetParent(transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchoredPosition = offset;
        rt.sizeDelta = sz;
        var img = go.AddComponent<Image>();
        img.color = col;
        img.raycastTarget = false;
        return img;
    }

    // ── Glow sprite — soft gaussian blur for diffuse light emission ─────
    static Sprite MakeGlowSprite(int w, int h, bool horizontal)
    {
        // Use higher-res texture for smooth gradient
        int tw = w * 2, th = h * 2;
        var tex = new Texture2D(tw, th, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode   = TextureWrapMode.Clamp;

        float cx = tw * 0.5f, cy = th * 0.5f;

        // Small sigma relative to total size — light concentrates at center
        // and fades to zero well before the edges
        float sigX = horizontal ? tw * 0.22f : tw * 0.28f;
        float sigY = horizontal ? th * 0.28f : th * 0.22f;

        for (int y = 0; y < th; y++)
        for (int x = 0; x < tw; x++)
        {
            float dx = (x - cx + 0.5f) / sigX;
            float dy = (y - cy + 0.5f) / sigY;
            float a  = Mathf.Exp(-(dx * dx + dy * dy));
            // Smooth to zero at edges — no hard cutoff
            a *= a; // square for sharper center, softer tails
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
        }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, tw, th), new Vector2(0.5f, 0.5f), 1f);
    }

    // ── Sprite generation — authentic LCD digital-clock segment ─────────
    static Sprite MakeBeveledSprite(int w, int h)
    {
        // Higher resolution for crisp anti-aliased edges
        int tw = Mathf.Max(w, 64);
        int th = Mathf.Max(h, 64);
        if (w > h) { th = Mathf.RoundToInt(tw * (float)h / w); }
        else       { tw = Mathf.RoundToInt(th * (float)w / h); }
        tw = Mathf.Max(tw, 4); th = Mathf.Max(th, 4);

        var tex = new Texture2D(tw, th, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode   = TextureWrapMode.Clamp;

        bool horizontal = w > h;

        if (horizontal)
        {
            // Horizontal: hexagon with pointed left/right ends  < ══════ >
            float hh    = th * 0.5f;
            float bevel = hh; // 45° pointed ends

            for (int y = 0; y < th; y++)
            for (int x = 0; x < tw; x++)
            {
                float fy   = y - hh + 0.5f;
                float absY = Mathf.Abs(fy);
                float leftEdge  = bevel * (absY / hh);
                float rightEdge = tw - bevel * (absY / hh);

                if (x >= leftEdge - 0.5f && x <= rightEdge + 0.5f)
                {
                    float aL    = Mathf.Clamp01(x - leftEdge + 1f);
                    float aR    = Mathf.Clamp01(rightEdge - x + 1f);
                    float alpha = Mathf.Min(aL, aR);

                    // Inner bevel: slight brightness gradient for 3D LCD depth
                    float distFromTop = (hh - fy) / th;
                    float lum = 0.82f + 0.18f * (1f - distFromTop);
                    tex.SetPixel(x, y, new Color(lum, lum, lum, alpha));
                }
                else
                    tex.SetPixel(x, y, Color.clear);
            }
        }
        else
        {
            // Vertical: hexagon rotated 90° — pointed top/bottom
            float hw    = tw * 0.5f;
            float bevel = hw;

            for (int y = 0; y < th; y++)
            for (int x = 0; x < tw; x++)
            {
                float fx   = x - hw + 0.5f;
                float absX = Mathf.Abs(fx);
                float botEdge = bevel * (absX / hw);
                float topEdge = th - bevel * (absX / hw);

                if (y >= botEdge - 0.5f && y <= topEdge + 0.5f)
                {
                    float aB    = Mathf.Clamp01(y - botEdge + 1f);
                    float aT    = Mathf.Clamp01(topEdge - y + 1f);
                    float alpha = Mathf.Min(aB, aT);

                    // Inner bevel: slight brightness gradient for 3D LCD depth
                    float distFromLeft = (hw + fx) / tw;
                    float lum = 0.80f + 0.20f * distFromLeft;
                    tex.SetPixel(x, y, new Color(lum, lum, lum, alpha));
                }
                else
                    tex.SetPixel(x, y, Color.clear);
            }
        }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, tw, th), new Vector2(0.5f, 0.5f), 1f);
    }

}
```

## `Assets/Scripts/Digit.cs`

```csharp
using UnityEngine;
using System.Collections;

public enum LinePositions
{
    Top = 0,
    Middle = 1,
    Bottom = 2,
    TopLeft = 3,
    TopRight = 4,
    BottomLeft = 5,
    BottomRight = 6,
    EnumLength = 7,
}

public class Digit : MonoBehaviour
{
    public Line[] Lines = null;

    public void Initialize(LinePositions lp)
    {
        for (int i = 0; i < Lines.Length; i++)
        {
            if (Lines[i] != null)
                Lines[i].SetActive();
        }
        if (Lines[(int)lp] != null)
            Lines[(int)lp].SetInactive();
    }

    public int GetDigit()
    {
        bool[] areActive = new bool[(int)LinePositions.EnumLength];
        GetActiveLines(areActive);

        int sum = 0;
        for (int i = 0; i < areActive.Length; i++)
        {
            if (areActive[i]) sum++;
        }

        if (sum == 6 && !areActive[(int)LinePositions.Middle])
            return 0;
        else if (sum == 2 && areActive[(int)LinePositions.TopRight] && areActive[(int)LinePositions.BottomRight])
            return 1;
        else if (sum == 5 && !areActive[(int)LinePositions.TopLeft] && !areActive[(int)LinePositions.BottomRight])
            return 2;
        else if (sum == 5 && !areActive[(int)LinePositions.TopLeft] && !areActive[(int)LinePositions.BottomLeft])
            return 3;
        else if (sum == 4 && !areActive[(int)LinePositions.Top] && !areActive[(int)LinePositions.BottomLeft] && !areActive[(int)LinePositions.Bottom])
            return 4;
        else if (sum == 5 && !areActive[(int)LinePositions.TopRight] && !areActive[(int)LinePositions.BottomLeft])
            return 5;
        else if (sum == 6 && !areActive[(int)LinePositions.TopRight])
            return 6;
        else if (sum == 3 && areActive[(int)LinePositions.Top] && areActive[(int)LinePositions.TopRight] && areActive[(int)LinePositions.BottomRight])
            return 7;
        else if (sum == 7)
            return 8;
        else if (sum == 6 && !areActive[(int)LinePositions.BottomLeft])
            return 9;
        else
            return -1;
    }

    void GetActiveLines(bool[] activeLines)
    {
        if (activeLines.Length >= (int)LinePositions.EnumLength && Lines.Length >= (int)LinePositions.EnumLength)
        {
            for (int i = 0; i < (int)LinePositions.EnumLength; i++)
            {
                activeLines[i] = Lines[i].IsSelected();
            }
        }
    }
}
```

## `Assets/Scripts/Number.cs`

```csharp
﻿using UnityEngine;
using System.Collections;

public class Number : MonoBehaviour
{
    public Digit FirstDigit = null;   // tens (2-digit) or tens (3-digit)
    public Digit SecondDigit = null;  // ones
    public Digit ThirdDigit = null;   // hundreds (optional, for 3-digit mode)

    public bool Is3Digit => ThirdDigit != null;

    public void Initialize(LinePositions lp1, LinePositions lp2)
    {
        FirstDigit.Initialize(lp1);
        SecondDigit.Initialize(lp2);
    }

    public void Initialize(LinePositions lpH, LinePositions lpT, LinePositions lpO)
    {
        if (ThirdDigit != null) ThirdDigit.Initialize(lpH);
        FirstDigit.Initialize(lpT);
        SecondDigit.Initialize(lpO);
    }

    public int GetNumber()
    {
        int d1 = FirstDigit.GetDigit();
        int d2 = SecondDigit.GetDigit();
        if (d1 == -1 || d2 == -1) return -1;

        if (ThirdDigit != null)
        {
            int d0 = ThirdDigit.GetDigit();
            if (d0 == -1) return -1;
            return d0 * 100 + d1 * 10 + d2;
        }

        return d1 * 10 + d2;
    }
}
```

## `Assets/Scripts/PlusMinus.cs`

```csharp
using UnityEngine;
using UnityEngine.UI;

// The +/− operator: one key, one tap. Minus becomes plus, plus becomes minus.
//
// It used to be two independently tappable matchsticks, which turned two
// meanings into four states — "neither lit" and "only the vertical lit" were
// dead ends that could never be correct. CheckAnswer fell through silently on
// both, so a player could place every digit right and have the game say
// nothing. In Hard, with two operators, 12 of the 16 combinations were dead.
//
// As a single toggle there is no invalid state left to get stuck in.
public class PlusMinus : MonoBehaviour
{
    public Line line1 = null;   // horizontal bar — always shown
    public Line line2 = null;   // vertical bar — shown only for plus

    bool isPlus = false;

    void Awake()
    {
        EnsureClickTarget();
    }

    /// <summary>
    /// A scene built before this became a single key still has two separately
    /// tappable bars and no Button. The state now lives in a bool, so tapping
    /// a bar there changes its colour but never the operator — it sticks on
    /// minus forever. Rather than fail that way, build the missing key here.
    /// </summary>
    void EnsureClickTarget()
    {
        if (GetComponentInChildren<Button>(true) != null) return;   // scene is current

        Debug.LogWarning("PlusMinus: this scene predates the single-key operator. " +
                         "Built a fallback key at runtime — re-run PlusMinus > Build Scene.");

        // The bars must stop eating the tap before the key can receive it
        foreach (var line in new[] { line1, line2 })
        {
            if (line == null) continue;
            var img = line.GetComponent<Image>();
            if (img != null) img.raycastTarget = false;
        }

        var go = new GameObject("btn_face");
        go.transform.SetParent(transform, false);

        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        var face = go.AddComponent<Image>();
        face.color = new Color(0f, 0f, 0f, 0.01f);

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = face;
        btn.onClick.AddListener(Toggle);

        // Behind the bars visually; raycasts still reach it since they are off
        go.transform.SetAsFirstSibling();
    }

    public bool IsPlus()  => isPlus;
    public bool IsMinus() => !isPlus;

    /// <summary>Flip the operator. Wired to the key's Button.</summary>
    public void Toggle()
    {
        isPlus = !isPlus;
        Apply();

        AudioManager.Instance?.PlaySFX(ClickSound());
        Messenger.Broadcast(Message.CheckForSolution);
    }

    /// <summary>Start of a round. Minus is the resting state, and it is valid.</summary>
    public void ResetToggle()
    {
        isPlus = false;
        Apply();
    }

    public void SetPlus()  { isPlus = true;  Apply(); }
    public void SetMinus() { isPlus = false; Apply(); }

    void Apply()
    {
        if (line1 != null)
        {
            line1.gameObject.SetActive(true);
            line1.SetSelected();
        }

        if (line2 != null)
        {
            // Hidden rather than dimmed when minus: a faint second stick is
            // exactly what made this read as two separate controls.
            line2.gameObject.SetActive(isPlus);
            if (isPlus) line2.SetSelected();
        }
    }

    // Same procedural click the segments use, so the operator does not sound
    // like a different kind of control.
    static AudioClip s_click;

    static AudioClip ClickSound()
    {
        if (s_click != null) return s_click;

        const int rate = 44100;
        const float dur = 0.1f;
        int n = (int)(rate * dur);

        s_click = AudioClip.Create("pmClick", n, 1, rate, false);
        var data = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            float freq = 800f * Mathf.Exp(-t * 10f);
            data[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * (1f - t / dur);
        }
        s_click.SetData(data, 0);
        return s_click;
    }
}
```

## `Assets/Scripts/Equals.cs`

```csharp
﻿using UnityEngine;
using System.Collections;

public class Equals : MonoBehaviour 
{
	public Line line1 = null;
	public Line line2 = null;

	void Start()
	{
		line1.SetSelected ();
		line2.SetSelected ();
	}

}
```

## `Assets/Scripts/TimerManager.cs`

```csharp
using UnityEngine;
using System.Collections;

public class TimerManager : MonoBehaviour
{
    public float StartTime = 60f;
    public float MinTime = 25f;

    float currentTimer = 0f;
    float currentMaxTime = 0f;
    bool gameAlreadyWon = false;

    float GetStartTimeForMode()
    {
        if (GameManager.Instance == null) return StartTime;

        // Arcade mode: fixed timer per mode (no progressive reduction)
        if (GameManager.Instance.isArcadeMode)
        {
            switch (GameManager.Instance.currentMode)
            {
                case GameMode.Easy:   return 30f;
                case GameMode.Medium: return 45f;
                case GameMode.Hard:   return 60f;
                default:              return 30f;
            }
        }

        // Training is where the player LEARNS the puzzle, so it gets real room.
        // The original 30/60/90 predated Medium growing to three digits and Hard
        // to three numbers with two operators. Doubled again after play-testing:
        // reading a seven-segment puzzle is slow work, and a clock that runs out
        // mid-thought teaches nothing.
        switch (GameManager.Instance.currentMode)
        {
            case GameMode.Easy:   return 100f;
            case GameMode.Medium: return 200f;
            case GameMode.Hard:   return 320f;
            default:              return StartTime;
        }
    }

    void Awake()
    {
        currentMaxTime = StartTime;

        Messenger.AddListener<string>(Message.OnEndFadeToTransparent, OnFadeToTransparentEnd);
        Messenger.AddListener<string>(Message.OnEndFadeToOpaque, OnFadeToOpaqueEnd);
        Messenger.AddListener(Message.StartNewGame, () =>
        {
            // Reset timer to mode's start time each new game session
            float modeStart = GetStartTimeForMode();
            currentMaxTime = modeStart;
        });
        Messenger.AddListener(Message.ArcadeRoundWon, () =>
        {
            StopCoroutine("TimerCo");
            StopCoroutine("StopwatchCo");
            gameAlreadyWon = true;
            // No timer reduction in arcade mode
        });
        Messenger.AddListener(Message.GameWon, () =>
        {
            StopCoroutine("TimerCo");
            gameAlreadyWon = true;
            if (GameManager.Instance != null && GameManager.Instance.isArcadeMode) return;

            // The streak tightens the clock, but gently: losing 5s per win meant
            // a good run punished itself into the floor within ten rounds.
            // 3s per win against a 65% floor keeps a long streak playable.
            float modeMin = GetStartTimeForMode() * 0.65f;
            if (modeMin < MinTime) modeMin = MinTime;
            float reduction = currentMaxTime > modeMin + 3f ? 3f : 1f;
            currentMaxTime = Mathf.Max(currentMaxTime - reduction, modeMin);
        });
        Messenger.AddListener(Message.GameLost, () =>
        {
            gameAlreadyWon = true;
            if (GameManager.Instance == null || !GameManager.Instance.isArcadeMode)
                currentMaxTime = GetStartTimeForMode();
        });
    }

    void OnFadeToTransparentEnd(string fadeToTransparentName)
    {
        if (fadeToTransparentName == "fadeToTransparentBeforeGameStarts")
        {
            StartCoroutine("TimerCo");
        }
    }

    void OnFadeToOpaqueEnd(string fadeToOpaqueName)
    {
        if (fadeToOpaqueName == "fadeToOpaqueBeforeGameStarts")
        {
            currentTimer = currentMaxTime;
            Messenger.Broadcast<float>(Message.OnSetTimer, currentTimer);
            Messenger.Broadcast<float>(Message.OnSetTimerMax, currentMaxTime);
        }
    }

    /// <summary>
    /// Arcade rounds run a stopwatch, not a countdown. A max of 0 tells the
    /// HUD there is no limit, so it drops the progress bar and stops tinting
    /// the readout red as if the player were running out.
    /// </summary>
    public void StartArcadeTimer()
    {
        StopCoroutine("TimerCo");
        StopCoroutine("StopwatchCo");

        currentTimer = 0f;
        currentMaxTime = 0f;
        Messenger.Broadcast<float>(Message.OnSetTimerMax, 0f);
        Messenger.Broadcast<float>(Message.OnSetTimer, 0f);
        StartCoroutine("StopwatchCo");
    }

    public void StopArcadeTimer()
    {
        StopCoroutine("TimerCo");
        StopCoroutine("StopwatchCo");
    }

    IEnumerator StopwatchCo()
    {
        currentTimer = 0f;
        gameAlreadyWon = false;

        while (true)
        {
            Messenger.Broadcast<float>(Message.OnSetTimer, currentTimer);
            currentTimer += Time.deltaTime;
            yield return null;
        }
    }

    IEnumerator TimerCo()
    {
        currentTimer = currentMaxTime;
        gameAlreadyWon = false;
        Messenger.Broadcast<float>(Message.OnSetTimerMax, currentMaxTime);

        while (currentTimer > 0)
        {
            Messenger.Broadcast<float>(Message.OnSetTimer, currentTimer);
            currentTimer -= Time.deltaTime;
            yield return null;
        }

        if (!gameAlreadyWon)
        {
            currentTimer = 0f;
            Messenger.Broadcast<float>(Message.OnSetTimer, currentTimer);

            if (GameManager.Instance != null && GameManager.Instance.isArcadeMode)
            {
                // A bot match owns the round when one is running; otherwise it's a real 1v1
                if (BotMatchManager.Instance != null && BotMatchManager.Instance.IsInMatch)
                    BotMatchManager.Instance.OnLocalPlayerTimeout();
                else
                    ArcadeMatchManager.Instance?.OnLocalPlayerTimeout();
            }
            else
                Messenger.Broadcast(Message.GameLost);
        }
    }
}
```

## `Assets/Scripts/GameMode.cs`

```csharp
public enum GameMode
{
    Easy,
    Medium,
    Hard,
    Random
}
```

## `Assets/Scripts/GameSaver.cs`

```csharp
using UnityEngine;
using System.Collections;

public class GameSaver : MonoBehaviour
{
    public int Highscore = 0;
    public int CurrentScore = 0;

    void Awake()
    {
        Messenger.AddListener<int, int>(Message.SaveGame, (hs, cs) => { SaveValues(hs, cs); });
    }

    void Start()
    {
        bool save = false;

        if (PlayerPrefs.HasKey("Highscore"))
        {
            Highscore = PlayerPrefs.GetInt("Highscore");
        }
        else
        {
            Highscore = 0;
            PlayerPrefs.SetInt("Highscore", 0);
            save = true;
        }

        if (PlayerPrefs.HasKey("CurrentScore"))
        {
            CurrentScore = PlayerPrefs.GetInt("CurrentScore");
        }
        else
        {
            CurrentScore = 0;
            PlayerPrefs.SetInt("CurrentScore", 0);
            save = true;
        }

        if (save)
        {
            PlayerPrefs.Save();
        }

        Messenger.Broadcast<int, int>(Message.SetHighscoreAndCurrentScore, Highscore, CurrentScore);
    }

    void SaveValues(int hS, int curS)
    {
        Highscore = hS;
        CurrentScore = curS;
        PlayerPrefs.SetInt("Highscore", hS);
        PlayerPrefs.SetInt("CurrentScore", curS);
        PlayerPrefs.Save();
    }
}
```

## `Assets/Scripts/EquationLayout.cs`

```csharp
using UnityEngine;

public class EquationLayout : MonoBehaviour
{
    [System.Serializable]
    public struct ElementPos
    {
        public RectTransform rt;
        public Vector2 portraitPos;
        public Vector2 landscapePos;
    }

    public ElementPos[] elements;
    public RectTransform divider;
    public RectTransform dividerGlow;
    public RectTransform equalsSign; // shown only in landscape
    public RectTransform eqBackground; // hidden in landscape
    public RectTransform container;

    public Vector2 containerPortraitSize = new Vector2(700, 700);
    public Vector2 containerLandscapeSize = new Vector2(1200, 300);
    public float portraitScale = 1f;
    public float landscapeScale = 1f;

    bool currentLandscape = false;

    Canvas rootCanvas;
    RectTransform canvasRect;

    void OnEnable()
    {
        // Find the root canvas to get actual rendered dimensions
        rootCanvas = GetComponentInParent<Canvas>();
        if (rootCanvas != null)
        {
            var root = rootCanvas.rootCanvas;
            canvasRect = root.GetComponent<RectTransform>();
        }

        currentLandscape = CheckLandscape();
        Apply(currentLandscape);
    }

    void Update()
    {
        bool landscape = CheckLandscape();
        if (landscape != currentLandscape)
        {
            currentLandscape = landscape;
            Debug.Log($"EquationLayout: switched to {(landscape ? "LANDSCAPE" : "PORTRAIT")}");
            Apply(landscape);
        }
    }

    bool CheckLandscape()
    {
        // Use canvas rect dimensions — these reflect actual rendered area
        // accounting for CanvasScaler and device simulator
        if (canvasRect != null)
        {
            var r = canvasRect.rect;
            return r.width > r.height;
        }
        // Fallback to screen dimensions
        return Screen.width > Screen.height;
    }

    public void ForceApply(bool landscape)
    {
        currentLandscape = landscape;
        Apply(landscape);
    }

    void Apply(bool landscape)
    {
        if (elements == null) return;

        for (int i = 0; i < elements.Length; i++)
        {
            if (elements[i].rt != null)
                elements[i].rt.anchoredPosition = landscape ? elements[i].landscapePos : elements[i].portraitPos;
        }

        if (divider != null) divider.gameObject.SetActive(!landscape);
        if (dividerGlow != null) dividerGlow.gameObject.SetActive(!landscape);
        if (equalsSign != null) equalsSign.gameObject.SetActive(landscape);
        if (eqBackground != null) eqBackground.gameObject.SetActive(!landscape);

        if (container != null)
        {
            container.sizeDelta = landscape ? containerLandscapeSize : containerPortraitSize;
            container.localScale = Vector3.one * (landscape ? landscapeScale : portraitScale);
        }
    }
}
```

## `Assets/Scripts/EventManager/Messages.cs`

```csharp
﻿/*
 * Based on the advanced C# messenger by Ilya Suzdalnitski. V1.0 that is based on Rod Hyde's "CSharpMessenger" and Magnus Wolffelt's "CSharpMessenger Extended".
 */

//#define LOG_ALL_MESSAGES
//#define LOG_ADD_LISTENER
//#define LOG_BROADCAST_MESSAGE
//#define REQUIRE_LISTENER
using System;
using System.Collections.Generic;
using UnityEngine;

public enum Message
{
	/// <summary>
	/// This is the default message
	/// </summary>
	none = 0,
    CheckForSolution,
    OnStartFadeToTransparent,
    OnStartFadeToOpaque,
    OnEndFadeToTransparent,
    OnEndFadeToOpaque,
    StartNewGame,
    GameWon,
    GameLost,
    OnIncrementProgress,
    OnResetProgress,
    OnSetTimer,
    OnSetTimerMax,
    SaveGame,
	SetHighscoreAndCurrentScore,
	SaveGameCounter,

    // Arcade 1v1 messages
    ArcadeRoundWon,              // local player solved correctly in arcade mode
    ArcadeMatchStarted,          // match begins
    ArcadeRoundEnded,            // round result determined
    ArcadeMatchEnded,            // match finished
    ArcadeOpponentDisconnected,  // opponent lost connection
}

public enum ReceiveMessage
{	
	/// <summary>
	/// This is the default receive message
	/// </summary>
	none = 0,
    ReceiveActiveNumberColor,
    ReceivePossibleNumberColor,
    ReceiveImpossibleNumberColor,
    ReceiveActiveInnerColor,
    ReceivePossibleImpossibleInnerColor,
	ReceiveGameProgress,
	ReceiveWinGUIColor,
	ReceiveLoseGUIColor,

}

static internal class Messenger
{
	#region Internal variables
	
	//Disable the unused variable warning
	#pragma warning disable 0414
	//Ensures that the MessengerHelper will be created automatically upon start of the game.
	//static private MessengerHelper messengerHelper = (new GameObject ("MessengerHelper")).AddComponent< MessengerHelper > ();
	#pragma warning restore 0414
	
	static public Dictionary<Message, Delegate> eventTable = new Dictionary<Message, Delegate> ();
	static public Dictionary<ReceiveMessage, Delegate> eventReceiverTable = new Dictionary<ReceiveMessage, Delegate> ();
	
	//Message handlers that should never be removed, regardless of calling Cleanup
	static public List< Message > permanentMessages = new List< Message > ();

	#endregion

	#region Helper methods
	//Marks a certain message as permanent.
	static public void MarkAsPermanent (Message eventType)
	{
		#if LOG_ALL_MESSAGES
		Debug.Log("Messenger MarkAsPermanent \t\"" + eventType + "\"");
		#endif
		
		permanentMessages.Add (eventType);
	}
	
	static public void Cleanup ()
	{
		#if LOG_ALL_MESSAGES
		Debug.Log("MESSENGER Cleanup. Make sure that none of necessary listeners are removed.");
		#endif
		
		List< Message > messagesToRemove = new List<Message> ();
		
		foreach (KeyValuePair<Message, Delegate> pair in eventTable) {
			bool wasFound = false;
			
			foreach (Message message in permanentMessages) {
				if (pair.Key == message) {
					wasFound = true;
					break;
				}
			}
			
			if (!wasFound)
				messagesToRemove.Add (pair.Key);
		}
		
		foreach (Message message in messagesToRemove) {
			eventTable.Remove (message);
		}

		eventReceiverTable.Clear();
	}
	
	static public void PrintEventTable ()
	{
		Debug.Log ("\t\t\t=== MESSENGER PrintEventTable ===");
		
		foreach (KeyValuePair<Message, Delegate> pair in eventTable) {
			Debug.Log ("\t\t\t" + pair.Key + "\t\t" + pair.Value);
		}
		
		Debug.Log ("\n");
	}

	static public void PrintEventReceiverTable()
	{
		Debug.Log ("\t\t\t=== RECEIVER PrintEventReceiverTable ===");
		
		foreach (KeyValuePair<ReceiveMessage, Delegate> pair in eventReceiverTable) {
			Debug.Log ("\t\t\t" + pair.Key + "\t\t" + pair.Value);
		}
		
		Debug.Log ("\n");
	}
	#endregion
	
	#region Message logging and exception throwing
	static public void OnListenerAdding (Message eventType, Delegate listenerBeingAdded)
	{
		#if LOG_ALL_MESSAGES || LOG_ADD_LISTENER
		Debug.Log("MESSENGER OnListenerAdding \t\"" + eventType + "\"\t{" + listenerBeingAdded.Target + " -> " + listenerBeingAdded.Method + "}");
		#endif
		
		if (!eventTable.ContainsKey (eventType)) {
			eventTable.Add (eventType, null);
		}
		
		Delegate d = eventTable [eventType];
		if (d != null && d.GetType () != listenerBeingAdded.GetType ()) {
			throw new ListenerException (string.Format ("Attempting to add listener with inconsistent signature for event type {0}. Current listeners have type {1} and listener being added has type {2}", 
			                                            eventType, 
			                                            d.GetType ().Name, 
			                                            listenerBeingAdded.GetType ().Name));
		}
	}
	
	static public void OnListenerRemoving (Message eventType, Delegate listenerBeingRemoved)
	{
		#if LOG_ALL_MESSAGES
		Debug.Log("MESSENGER OnListenerRemoving \t\"" + eventType + "\"\t{" + listenerBeingRemoved.Target + " -> " + listenerBeingRemoved.Method + "}");
		#endif
		
		if (eventTable.ContainsKey (eventType)) {
			Delegate d = eventTable [eventType];
			
			if (d == null) {
				throw new ListenerException (string.Format ("Attempting to remove listener with for event type \"{0}\" but current listener is null.", eventType));
			}  else if (d.GetType () != listenerBeingRemoved.GetType ()) {
				throw new ListenerException (string.Format ("Attempting to remove listener with inconsistent signature for event type {0}. Current listeners have type {1} and listener being removed has type {2}", 
				                                            eventType, d.GetType ().Name, 
				                                            listenerBeingRemoved.GetType ().Name));
			}
		}  else {
			throw new ListenerException (string.Format ("Attempting to remove listener for type \"{0}\" but Messenger doesn't know about this event type.", eventType));
		}
	}
	
	static public void OnListenerRemoved (Message eventType)
	{
		if (eventTable [eventType] == null) {
			eventTable.Remove (eventType);
		}
	}
	
	static public void OnBroadcasting (Message eventType)
	{
		#if REQUIRE_LISTENER
		if (!eventTable.ContainsKey (eventType)) {
			throw new BroadcastException (string.Format ("Broadcasting message \"{0}\" but no listener found. Try marking the message with Messenger.MarkAsPermanent.", eventType));
		}
		#endif
	}
	
	static public BroadcastException CreateBroadcastSignatureException (Message eventType)
	{
		return new BroadcastException (string.Format ("Broadcasting message \"{0}\" but listeners have a different signature than the broadcaster.", eventType));
	}
	
	public class BroadcastException : Exception
	{
		public BroadcastException (string msg)
			: base(msg)
		{
		}
	}
	
	public class ListenerException : Exception
	{
		public ListenerException (string msg)
			: base(msg)
		{
		}
	}
	#endregion
	
	#region AddListener
	//No parameters
	static public void AddListener (Message eventType, Callback handler)
	{
		OnListenerAdding (eventType, handler);
		eventTable [eventType] = (Callback)eventTable [eventType] + handler;
	}
	
	//Single parameter
	static public void AddListener<T> (Message eventType, Callback<T> handler)
	{
		OnListenerAdding (eventType, handler);
		eventTable [eventType] = (Callback<T>)eventTable [eventType] + handler;
	}
	
	//Two parameters
	static public void AddListener<T, U> (Message eventType, Callback<T, U> handler)
	{
		OnListenerAdding (eventType, handler);
		eventTable [eventType] = (Callback<T, U>)eventTable [eventType] + handler;
	}
	
	//Three parameters
	static public void AddListener<T, U, V> (Message eventType, Callback<T, U, V> handler)
	{
		OnListenerAdding (eventType, handler);
		eventTable [eventType] = (Callback<T, U, V>)eventTable [eventType] + handler;
	}
	#endregion
	
	#region RemoveListener
	//No parameters
	static public void RemoveListener (Message eventType, Callback handler)
	{
		OnListenerRemoving (eventType, handler);   
		eventTable [eventType] = (Callback)eventTable [eventType] - handler;
		OnListenerRemoved (eventType);
	}
	
	//Single parameter
	static public void RemoveListener<T> (Message eventType, Callback<T> handler)
	{
		OnListenerRemoving (eventType, handler);
		eventTable [eventType] = (Callback<T>)eventTable [eventType] - handler;
		OnListenerRemoved (eventType);
	}
	
	//Two parameters
	static public void RemoveListener<T, U> (Message eventType, Callback<T, U> handler)
	{
		OnListenerRemoving (eventType, handler);
		eventTable [eventType] = (Callback<T, U>)eventTable [eventType] - handler;
		OnListenerRemoved (eventType);
	}
	
	//Three parameters
	static public void RemoveListener<T, U, V> (Message eventType, Callback<T, U, V> handler)
	{
		OnListenerRemoving (eventType, handler);
		eventTable [eventType] = (Callback<T, U, V>)eventTable [eventType] - handler;
		OnListenerRemoved (eventType);
	}
	#endregion
	
	#region Broadcast
	//No parameters
	static public void Broadcast (Message eventType)
	{
		#if LOG_ALL_MESSAGES || LOG_BROADCAST_MESSAGE
		Debug.Log("MESSENGER\t" + System.DateTime.Now.ToString("hh:mm:ss.fff") + "\t\t\tInvoking \t\"" + eventType + "\"");
		#endif
		OnBroadcasting (eventType);
		
		Delegate d;
		if (eventTable.TryGetValue (eventType, out d)) {
			Callback callback = d as Callback;
			
			if (callback != null) {
				callback ();
			}  else {
				throw CreateBroadcastSignatureException (eventType);
			}
		}
	}
	
	//Single parameter
	static public void Broadcast<T> (Message eventType, T arg1)
	{
		#if LOG_ALL_MESSAGES || LOG_BROADCAST_MESSAGE
		Debug.Log("MESSENGER\t" + System.DateTime.Now.ToString("hh:mm:ss.fff") + "\t\t\tInvoking \t\"" + eventType + "\"");
		#endif
		OnBroadcasting (eventType);
		
		Delegate d;
		if (eventTable.TryGetValue (eventType, out d)) {
			Callback<T> callback = d as Callback<T>;
			
			if (callback != null) {
				callback (arg1);
			}  else {
				throw CreateBroadcastSignatureException (eventType);
			}
		}
	}
	
	//Two parameters
	static public void Broadcast<T, U> (Message eventType, T arg1, U arg2)
	{
		#if LOG_ALL_MESSAGES || LOG_BROADCAST_MESSAGE
		Debug.Log("MESSENGER\t" + System.DateTime.Now.ToString("hh:mm:ss.fff") + "\t\t\tInvoking \t\"" + eventType + "\"");
		#endif
		OnBroadcasting (eventType);
		
		Delegate d;
		if (eventTable.TryGetValue (eventType, out d)) {
			Callback<T, U> callback = d as Callback<T, U>;
			
			if (callback != null) {
				callback (arg1, arg2);
			}  else {
				throw CreateBroadcastSignatureException (eventType);
			}
		}
	}
	
	//Three parameters
	static public void Broadcast<T, U, V> (Message eventType, T arg1, U arg2, V arg3)
	{
		#if LOG_ALL_MESSAGES || LOG_BROADCAST_MESSAGE
		Debug.Log("MESSENGER\t" + System.DateTime.Now.ToString("hh:mm:ss.fff") + "\t\t\tInvoking \t\"" + eventType + "\"");
		#endif
		OnBroadcasting (eventType);
		
		Delegate d;
		if (eventTable.TryGetValue (eventType, out d)) {
			Callback<T, U, V> callback = d as Callback<T, U, V>;
			
			if (callback != null) {
				callback (arg1, arg2, arg3);
			}  else {
				throw CreateBroadcastSignatureException (eventType);
			}
		}
	}
	#endregion

	#region Receive message logging and exception throwing
	static public void OnReceiverListenerAdding (ReceiveMessage eventType, Delegate listenerBeingAdded)
	{
		#if LOG_ALL_MESSAGES || LOG_ADD_LISTENER
		Debug.Log("MESSENGER OnListenerAdding \t\"" + eventType + "\"\t{" + listenerBeingAdded.Target + " -> " + listenerBeingAdded.Method + "}");
		#endif
		
		if (!eventReceiverTable.ContainsKey (eventType)) {
			eventReceiverTable.Add (eventType, null);
		}
		
		Delegate d = eventReceiverTable [eventType];
		if (d != null && d.GetType () != listenerBeingAdded.GetType ()) {
			throw new ListenerException (string.Format ("Attempting to add listener with inconsistent signature for event type {0}. Current listeners have type {1} and listener being added has type {2}", 
			                                            eventType, 
			                                            d.GetType ().Name, 
			                                            listenerBeingAdded.GetType ().Name));
		}
	}
	
	static public void OnReceiverListenerRemoving (ReceiveMessage eventType, Delegate listenerBeingRemoved)
	{
		#if LOG_ALL_MESSAGES
		Debug.Log("MESSENGER OnListenerRemoving \t\"" + eventType + "\"\t{" + listenerBeingRemoved.Target + " -> " + listenerBeingRemoved.Method + "}");
		#endif
		
		if (eventReceiverTable.ContainsKey (eventType)) {
			Delegate d = eventReceiverTable [eventType];
			
			if (d == null) {
				throw new ListenerException (string.Format ("Attempting to remove listener with for event type \"{0}\" but current listener is null.", eventType));
			}  else if (d.GetType () != listenerBeingRemoved.GetType ()) {
				throw new ListenerException (string.Format ("Attempting to remove listener with inconsistent signature for event type {0}. Current listeners have type {1} and listener being removed has type {2}", 
				                                            eventType, d.GetType ().Name, 
				                                            listenerBeingRemoved.GetType ().Name));
			}
		}  else {
			throw new ListenerException (string.Format ("Attempting to remove listener for type \"{0}\" but Messenger doesn't know about this event type.", eventType));
		}
	}
	
	static public void OnReceiverListenerRemoved (ReceiveMessage eventType)
	{
		if (eventReceiverTable [eventType] == null) {
			eventReceiverTable.Remove (eventType);
		}
	}
	
	static public void OnBroadcastingReceiver (ReceiveMessage eventType)
	{
		#if REQUIRE_LISTENER
		if (!eventReceiverTable.ContainsKey (eventType)) {
			throw new BroadcastException (string.Format ("Broadcasting message \"{0}\" but no listener found. Try marking the message with Messenger.MarkAsPermanent.", eventType));
		}
		#endif
	}
	
	static public BroadcastException CreateReceiverBroadcastSignatureException (ReceiveMessage eventType)
	{
		return new BroadcastException (string.Format ("Broadcasting message \"{0}\" but listeners have a different signature than the broadcaster.", eventType));
	}

	#endregion

	
	#region AddReceiverListener
	//No parameters
	static public void AddReceiverListener<R> (ReceiveMessage eventType, CallbackReceiver<R> handler)
	{
		OnReceiverListenerAdding (eventType, handler);
		eventReceiverTable [eventType] = (CallbackReceiver<R>)eventReceiverTable [eventType] + handler;
	}
	
	//Single parameter
	static public void AddReceiverListener<R, T> (ReceiveMessage eventType, CallbackReceiver<R, T> handler)
	{
		OnReceiverListenerAdding (eventType, handler);
		eventReceiverTable [eventType] = (CallbackReceiver<R, T>)eventReceiverTable [eventType] + handler;
	}
	
	//Two parameters
	static public void AddReceiverListener<R, T, U> (ReceiveMessage eventType, CallbackReceiver<R, T, U> handler)
	{
		OnReceiverListenerAdding (eventType, handler);
		eventReceiverTable [eventType] = (CallbackReceiver<R, T, U>)eventReceiverTable [eventType] + handler;
	}
	
	//Three parameters
	static public void AddReceiverListener<R, T, U, V> (ReceiveMessage eventType, CallbackReceiver<R, T, U, V> handler)
	{
		OnReceiverListenerAdding (eventType, handler);
		eventReceiverTable [eventType] = (CallbackReceiver<R, T, U, V>)eventReceiverTable [eventType] + handler;
	}
	#endregion
	
	#region RemoveReceiverListener
	//No parameters
	static public void RemoveReceiverListener<R> (ReceiveMessage eventType, CallbackReceiver<R> handler)
	{
		OnReceiverListenerRemoving (eventType, handler);   
		eventReceiverTable [eventType] = (CallbackReceiver<R>)eventReceiverTable [eventType] - handler;
		OnReceiverListenerRemoved (eventType);
	}
	
	//Single parameter
	static public void RemoveReceiverListener<R, T> (ReceiveMessage eventType, CallbackReceiver<R, T> handler)
	{
		OnReceiverListenerRemoving (eventType, handler);
		eventReceiverTable [eventType] = (CallbackReceiver<R, T>)eventReceiverTable [eventType] - handler;
		OnReceiverListenerRemoved (eventType);
	}
	
	//Two parameters
	static public void RemoveReceiverListener<R, T, U> (ReceiveMessage eventType, CallbackReceiver<R, T, U> handler)
	{
		OnReceiverListenerRemoving (eventType, handler);
		eventReceiverTable [eventType] = (CallbackReceiver<R, T, U>)eventReceiverTable [eventType] - handler;
		OnReceiverListenerRemoved (eventType);
	}
	
	//Three parameters
	static public void RemoveReceiverListener<R, T, U, V> (ReceiveMessage eventType, CallbackReceiver<R, T, U, V> handler)
	{
		OnReceiverListenerRemoving (eventType, handler);
		eventReceiverTable [eventType] = (CallbackReceiver<R, T, U, V>)eventReceiverTable [eventType] - handler;
		OnReceiverListenerRemoved (eventType);
	}
	#endregion
	
	#region Broadcast
	//No parameters
	static public R BroadcastReceiver<R> (ReceiveMessage eventType)
	{
		#if LOG_ALL_MESSAGES || LOG_BROADCAST_MESSAGE
		Debug.Log("MESSENGER\t" + System.DateTime.Now.ToString("hh:mm:ss.fff") + "\t\t\tInvoking \t\"" + eventType + "\"");
		#endif
		OnBroadcastingReceiver (eventType);
		
		Delegate d;
		if (eventReceiverTable.TryGetValue (eventType, out d)) {
			CallbackReceiver<R> callback = d as CallbackReceiver<R>;
			
			if (callback != null) {
				return callback ();
			}  else {
				throw CreateReceiverBroadcastSignatureException (eventType);
			}
		}

		Debug.LogWarning(eventType.ToString() + " listener not created");

		return default(R);
	}
	
	//Single parameter
	static public R BroadcastReceiver<R, T> (ReceiveMessage eventType, T arg1)
	{
		#if LOG_ALL_MESSAGES || LOG_BROADCAST_MESSAGE
		Debug.Log("MESSENGER\t" + System.DateTime.Now.ToString("hh:mm:ss.fff") + "\t\t\tInvoking \t\"" + eventType + "\"");
		#endif
		OnBroadcastingReceiver (eventType);
		
		Delegate d;
		if (eventReceiverTable.TryGetValue (eventType, out d)) {
			CallbackReceiver<R, T> callback = d as CallbackReceiver<R, T>;
			
			if (callback != null) {
				return callback (arg1);
			}  else {
				throw CreateReceiverBroadcastSignatureException (eventType);
			}
		}
		
		Debug.LogWarning(eventType.ToString() + " listener not created");
		
		return default(R);
	}
	
	//Two parameters
	static public R BroadcastReceiver<R, T, U> (ReceiveMessage eventType, T arg1, U arg2)
	{
		#if LOG_ALL_MESSAGES || LOG_BROADCAST_MESSAGE
		Debug.Log("MESSENGER\t" + System.DateTime.Now.ToString("hh:mm:ss.fff") + "\t\t\tInvoking \t\"" + eventType + "\"");
		#endif
		OnBroadcastingReceiver (eventType);
		
		Delegate d;
		if (eventReceiverTable.TryGetValue (eventType, out d)) {
			CallbackReceiver<R, T, U> callback = d as CallbackReceiver<R, T, U>;
			
			if (callback != null) {
				return callback (arg1, arg2);
			}  else {
				throw CreateReceiverBroadcastSignatureException (eventType);
			}
		}
		
		Debug.LogWarning(eventType.ToString() + " listener not created");
		
		return default(R);
	}
	
	//Three parameters
	static public R BroadcastReceiver<R, T, U, V> (ReceiveMessage eventType, T arg1, U arg2, V arg3)
	{
		#if LOG_ALL_MESSAGES || LOG_BROADCAST_MESSAGE
		Debug.Log("MESSENGER\t" + System.DateTime.Now.ToString("hh:mm:ss.fff") + "\t\t\tInvoking \t\"" + eventType + "\"");
		#endif
		OnBroadcastingReceiver (eventType);
		
		Delegate d;
		if (eventReceiverTable.TryGetValue (eventType, out d)) {
			CallbackReceiver<R, T, U, V> callback = d as CallbackReceiver<R, T, U, V>;
			
			if (callback != null) {
				return callback (arg1, arg2, arg3);
			}  else {
				throw CreateReceiverBroadcastSignatureException (eventType);
			}
		}
		
		Debug.LogWarning(eventType.ToString() + " listener not created");
		
		return default(R);
	}
	#endregion
}
```

## `Assets/Scripts/EventManager/Callbacks.cs`

```csharp
﻿


public delegate void Callback();
public delegate void Callback<T>(T arg1);
public delegate void Callback<T, U>(T arg1, U arg2);
public delegate void Callback<T, U, V>(T arg1, U arg2, V arg3);
public delegate R CallbackReceiver<R>();
public delegate R CallbackReceiver<R, T>(T arg1);
public delegate R CallbackReceiver<R, T, U>(T arg1, U arg2);
public delegate R CallbackReceiver<R, T, U, V>(T arg1, U arg2, V arg3);
```

## `Assets/Scripts/EventManager/MessengerCleaner.cs`

```csharp
﻿using UnityEngine;
using System.Collections;

//This manager will ensure that the messenger's eventTable will be cleaned up upon loading of a new level.
public sealed class MessengerCleaner : MonoBehaviour
{
	//Clean up eventTable every time a new level loads.
	public void OnDisable ()
	{
		Messenger.Cleanup ();
	}
}
```
