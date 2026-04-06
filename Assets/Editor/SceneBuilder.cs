#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class SceneBuilder
{
    // ── Neon Green LCD Palette ───────────────────────────────────────────
    static readonly Color BG           = Hex("#0A0F0A");   // near-black with green tint
    static readonly Color BG_GRAD_TOP  = Hex("#0D120D");
    static readonly Color BG_GRAD_BOT  = Hex("#060A06");
    static readonly Color PANEL_BG     = Hex("#0E140E");   // card backgrounds
    static readonly Color PANEL_BORDER = Hex("#1A2E1A");

    static readonly Color SEG_OFF      = Hex("#1A2E10");   // dim green (unselected segment)
    static readonly Color SEG_FIXED    = Hex("#0D1A08");   // very dim (fixed/impossible)

    static readonly Color ACCENT       = Hex("#76FF03");   // neon lime green
    static readonly Color ACCENT_LIGHT = Hex("#B2FF59");   // light lime
    static readonly Color ACCENT_DARK  = Hex("#33691E");   // dark green

    static readonly Color WIN_COLOR    = Hex("#76FF03");   // same neon green
    static readonly Color LOSE_COLOR   = Hex("#FF1744");   // bright red

    static readonly Color CARD_COLOR   = Hex("#0E160EF0"); // dark green card
    static readonly Color CARD_BORDER  = Hex("#1B2E1B");

    static readonly Color BTN_FACE     = Hex("#33691E");   // dark green face
    static readonly Color BTN_TOP      = Hex("#76FF03");   // neon green
    static readonly Color BTN_SHADOW   = Hex("#1B3A0A");   // deep green shadow
    static readonly Color BTN_BORDER   = Hex("#2E7D0E");

    static readonly Color BTN_GREEN_FACE   = Hex("#2E7D32");
    static readonly Color BTN_GREEN_TOP    = Hex("#76FF03");
    static readonly Color BTN_GREEN_SHADOW = Hex("#1B5E20");

    static readonly Color TEXT_PRIMARY = Hex("#E8F5E9");   // light green-white
    static readonly Color TEXT_DIM     = Hex("#66BB6A");   // medium green
    static readonly Color TEXT_MUTED   = Hex("#388E3C");   // muted green
    static readonly Color TIMER_BG     = Hex("#1A2E1A");
    static readonly Color DIVIDER_C    = Hex("#76FF03");   // neon green divider

    // ── Segment dimensions ───────────────────────────────────────────────
    const float DW  = 160f;
    const float DH  = 240f;
    const float SHW = 94f;
    const float SHH = 20f;
    const float SVW = 20f;
    const float SVH = 78f;
    const float SX  = 70f;
    const float SYT = 110f;
    const float SYV = 60f;

    const float TIMER_MAX = 90f;

    // ── Rounded rect sprite cache ────────────────────────────────────────
    static Sprite s_roundRect;
    static Sprite RoundRect => s_roundRect != null ? s_roundRect : (s_roundRect = MakeRoundRect(128, 128, 24));
    static Sprite s_roundRectLarge;
    static Sprite RoundRectLarge => s_roundRectLarge != null ? s_roundRectLarge : (s_roundRectLarge = MakeRoundRect(128, 128, 32));
    static Sprite s_pill;
    static Sprite Pill => s_pill != null ? s_pill : (s_pill = MakeRoundRect(128, 64, 32));
    static Sprite s_circle;
    static Sprite Circle => s_circle != null ? s_circle : (s_circle = MakeCircle(64));

    // ── Digital display font (Orbitron Bold) ────────────────────────────
    static Font s_digitalFont;
    static Font DigitalFont {
        get {
            if (s_digitalFont == null)
                s_digitalFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/Orbitron-Bold.ttf");
            return s_digitalFont;
        }
    }

    // ── Beveled hexagonal segment sprites ─────────────────────────────────
    static Sprite s_hSeg;   // horizontal segment (pointed left/right ends)
    static Sprite HSeg => s_hSeg != null ? s_hSeg : (s_hSeg = MakeBeveledSeg(128, 32, true));
    static Sprite s_vSeg;   // vertical segment (pointed top/bottom ends)
    static Sprite VSeg => s_vSeg != null ? s_vSeg : (s_vSeg = MakeBeveledSeg(32, 128, false));

    [MenuItem("PlusMinus/Build Scene")]
    public static void Build()
    {
        if (EditorApplication.isPlaying)
        { Debug.LogError("Stop Play mode first!"); return; }

        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // EventSystem
        var es = new GameObject("EventSystem");
        es.AddComponent<UnityEngine.EventSystems.EventSystem>();
        es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();

        // Camera
        var camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera";
        var cam = camGO.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = BG;
        cam.orthographic = true;
        cam.orthographicSize = 5f;
        camGO.AddComponent<AudioListener>();
        camGO.AddComponent<HeartbeatManager>();
        camGO.AddComponent<CameraResizer>();

        // Canvas
        var canvasGO = new GameObject("GameCanvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();
        var ct = canvasGO.transform;

        // ── Background with subtle gradient ──────────────────────────────
        Img(ct, "BG", V2(0,0), V2(1,1), V2(0,0), V2(0,0), BG).raycastTarget = false;

        // Placeholder for login/register panels (created after gui is initialized)

        // ══════════════════════════════════════════════════════════════════
        //  pnl_start — Main Menu (segment-glow minimalist)
        // ══════════════════════════════════════════════════════════════════
        var pnlStart = Panel(ct, "pnl_start", V2(0,0), V2(1,1), V2(0,0), V2(0,0));
        var st = pnlStart.transform;

        // Dark background
        Img(st, "StartBG", V2(0,0), V2(1,1), V2(0,0), V2(0,0), Hex("#080C16")).raycastTarget = false;

        // ── Title — digital segment letters ─────────────────────────────
        DigTxt(st, "lbl_title_glow", "MATHSTICK", V2(.5f,1), V2(.5f,1), V2(2,-648), V2(900,210), 140, Hex("#F59E0B10")).raycastTarget = false;
        DigTxt(st, "lbl_title_main", "MATHSTICK", V2(.5f,1), V2(.5f,1), V2(0,-646), V2(900,210), 140, ACCENT).raycastTarget = false;
        DigTxt(st, "lbl_title2_glow", "PUZZLE", V2(.5f,1), V2(.5f,1), V2(2,-808), V2(900,165), 110, Hex("#F59E0B10")).raycastTarget = false;
        DigTxt(st, "lbl_title2_main", "PUZZLE", V2(.5f,1), V2(.5f,1), V2(0,-806), V2(900,165), 110, ACCENT).raycastTarget = false;

        // Decorative segment-bar under title
        Img(st, "TitleSeg", V2(.5f,1), V2(.5f,1), V2(0,-880), V2(160, 4), Hex("#F59E0B30")).raycastTarget = false;

        // ── TRAINING button ───────────────────────────────────────────
        SegButton(st, "btn_play", "TRAINING", V2(.5f,.5f), V2(.5f,.5f), V2(0, 60),
            V2(420, 90), 34, ACCENT);

        // ── ARCADE button ────────────────────────────────────────────
        SegButton(st, "btn_arcade", "ARCADE", V2(.5f,.5f), V2(.5f,.5f), V2(0, -60),
            V2(420, 90), 34, ACCENT);
        DigTxt(st, "lbl_arcade_tag", "1V1 ONLINE", V2(.5f,.5f), V2(.5f,.5f), V2(0,-118), V2(900,24), 16, TEXT_MUTED).raycastTarget = false;

        // ── HOW TO PLAY button ───────────────────────────────────────
        SegButton(st, "btn_tutorial", "HOW TO PLAY", V2(.5f,.5f), V2(.5f,.5f), V2(0, -180),
            V2(420, 80), 26, TEXT_DIM);

        // ── Best score — BOTTOM of screen, big ──────────────────────
        var bestGO = Panel(st, "BestFrame", V2(.5f,0), V2(.5f,0), V2(0, 180), V2(400, 140));
        float bt = 3f, bg = 2f;
        Img(bestGO.transform, "bt", V2(.5f,1), V2(.5f,1), V2(0,-bg), V2(360,bt), Hex("#F59E0B25")).raycastTarget = false;
        Img(bestGO.transform, "bb", V2(.5f,0), V2(.5f,0), V2(0,bg), V2(360,bt), Hex("#F59E0B25")).raycastTarget = false;
        DigTxt(bestGO.transform, "lbl_best_tag", "PERSONAL BEST", V2(.5f,1), V2(.5f,1), V2(0,-20), V2(900,36), 24, TEXT_MUTED).raycastTarget = false;
        var lblStartHS = Txt(bestGO.transform, "lbl_start_highscore", "0",
            V2(.5f,0), V2(.5f,0), V2(0,35), V2(360,80), 66, ACCENT_LIGHT,
            TextAnchor.MiddleCenter, FontStyle.Bold);

        // ══════════════════════════════════════════════════════════════════
        //  pnl_tutorial — Real Game Demo with Modern Onboarding
        // ══════════════════════════════════════════════════════════════════
        var pnlTutorial = Panel(ct, "pnl_tutorial", V2(0,0), V2(1,1), V2(0,0), V2(0,0));
        var tt = pnlTutorial.transform;

        // Dark bg
        RoundImg(tt, "TutBG", V2(0,0), V2(1,1), V2(0,0), V2(0,0), Hex("#0A0E1AFA")).raycastTarget = false;

        // Title
        DigTxt(tt, "lbl_tut_title", "HOW TO PLAY", V2(.5f,1), V2(.5f,1), V2(0,-60), V2(900,63), 42, TEXT_PRIMARY).raycastTarget = false;

        // ── Real equation using SAME Seg() as the game ───────────────
        // Layout: Number1 (2 digits), operator, Number2 (2 digits), divider, Answer (2 digits)
        // Exactly like pnl_main but in tutorial panel
        float tnumX = 50f;

        var tutEqGO = Panel(tt, "TutEquation", V2(.5f,.5f), V2(.5f,.5f), V2(0, 0), V2(700, 700));
        tutEqGO.transform.localScale = new Vector3(0.70f, 0.70f, 1f);
        var teq = tutEqGO.transform;

        // Number1 at top
        var tutNum1 = NumberGroup(teq, "TutNum1", V2(tnumX, 220));
        // Operator
        var tutPM = PlusMinusToggle(teq, V2(-160, 0));
        // Number2
        var tutNum2 = NumberGroup(teq, "TutNum2", V2(tnumX, 0));
        // Divider
        RoundImg(teq, "TutDiv", V2(.5f,.5f), V2(.5f,.5f), V2(tnumX, -120),
            V2(DW*2+80, 3), DIVIDER_C).raycastTarget = false;
        // Answer
        var tutAns = NumberGroup(teq, "TutAns", V2(tnumX, -240));

        // Collect all Line segments for reset, and define tap order
        // Get all Lines from the tutorial equation
        var allLines = tutEqGO.GetComponentsInChildren<Line>();
        // Get Lines per digit:
        // tutNum1.FirstDigit.Lines[0..6], tutNum1.SecondDigit.Lines[0..6]
        // tutPM lines
        // tutNum2 digits, tutAns digits
        var tapList = new System.Collections.Generic.List<Line>();

        // Demo equation: 15 + 23 = 38
        // Digit "1" = Lines: TopRight(4), BottomRight(6)
        tapList.Add(tutNum1.FirstDigit.Lines[4]);
        tapList.Add(tutNum1.FirstDigit.Lines[6]);
        // Digit "5" = Top(0), TopLeft(3), Middle(1), BottomRight(6), Bottom(2)
        tapList.Add(tutNum1.SecondDigit.Lines[0]);
        tapList.Add(tutNum1.SecondDigit.Lines[3]);
        tapList.Add(tutNum1.SecondDigit.Lines[1]);
        tapList.Add(tutNum1.SecondDigit.Lines[6]);
        tapList.Add(tutNum1.SecondDigit.Lines[2]);
        // Operator "+" = horizontal and vertical
        tapList.Add(tutPM.line1);
        tapList.Add(tutPM.line2);
        // Digit "2" = Top(0), TopRight(4), Middle(1), BottomLeft(5), Bottom(2)
        tapList.Add(tutNum2.FirstDigit.Lines[0]);
        tapList.Add(tutNum2.FirstDigit.Lines[4]);
        tapList.Add(tutNum2.FirstDigit.Lines[1]);
        tapList.Add(tutNum2.FirstDigit.Lines[5]);
        tapList.Add(tutNum2.FirstDigit.Lines[2]);
        // Digit "3" = Top(0), TopRight(4), Middle(1), BottomRight(6), Bottom(2)
        tapList.Add(tutNum2.SecondDigit.Lines[0]);
        tapList.Add(tutNum2.SecondDigit.Lines[4]);
        tapList.Add(tutNum2.SecondDigit.Lines[1]);
        tapList.Add(tutNum2.SecondDigit.Lines[6]);
        tapList.Add(tutNum2.SecondDigit.Lines[2]);
        // Answer digit "3"
        tapList.Add(tutAns.FirstDigit.Lines[0]);
        tapList.Add(tutAns.FirstDigit.Lines[4]);
        tapList.Add(tutAns.FirstDigit.Lines[1]);
        tapList.Add(tutAns.FirstDigit.Lines[6]);
        tapList.Add(tutAns.FirstDigit.Lines[2]);
        // Answer digit "8" = all 7
        tapList.Add(tutAns.SecondDigit.Lines[0]);
        tapList.Add(tutAns.SecondDigit.Lines[3]);
        tapList.Add(tutAns.SecondDigit.Lines[4]);
        tapList.Add(tutAns.SecondDigit.Lines[1]);
        tapList.Add(tutAns.SecondDigit.Lines[5]);
        tapList.Add(tutAns.SecondDigit.Lines[6]);
        tapList.Add(tutAns.SecondDigit.Lines[2]);

        // ── Overlay elements ─────────────────────────────────────────
        // Pulse ring (small, just highlights the segment)
        var pulseGO = new GameObject("pulse_ring");
        pulseGO.transform.SetParent(tt, false);
        var pulseRt = pulseGO.AddComponent<RectTransform>();
        pulseRt.anchorMin = pulseRt.anchorMax = V2(.5f,.5f);
        pulseRt.sizeDelta = V2(40, 40);
        var pulseImg = pulseGO.AddComponent<Image>();
        pulseImg.sprite = Circle;
        pulseImg.color = new Color(0.96f, 0.62f, 0.04f, 0.5f);
        pulseImg.raycastTarget = false;
        pulseGO.SetActive(false);

        // Hint popup card
        var hintCardGO = RoundImg(tt, "hint_card", V2(.5f,0), V2(.5f,0), V2(0, 130), V2(340, 80), Hex("#1E293BF0"));
        hintCardGO.raycastTarget = false;
        var lblHint = Txt(hintCardGO.transform, "lbl_hint", "",
            V2(0,0), V2(1,1), V2(10,0), V2(-10,0), 22, ACCENT_LIGHT,
            TextAnchor.MiddleCenter, FontStyle.Normal);
        lblHint.raycastTarget = false;

        // Congrats text
        var lblCongrats = Txt(tt, "lbl_congrats", "",
            V2(.5f,.5f), V2(.5f,.5f), V2(0, 0), V2(400, 80), 52, WIN_COLOR,
            TextAnchor.MiddleCenter, FontStyle.Bold);
        lblCongrats.raycastTarget = false;
        lblCongrats.gameObject.SetActive(false);

        // Finger pointer
        var fingerGO = new GameObject("finger");
        fingerGO.transform.SetParent(tt, false);
        var fingerRt = fingerGO.AddComponent<RectTransform>();
        fingerRt.anchorMin = fingerRt.anchorMax = V2(.5f,.5f);
        fingerRt.sizeDelta = V2(50, 60);
        var fingerImg = fingerGO.AddComponent<Image>();
        fingerImg.sprite = MakeFingerSprite();
        fingerImg.color = Color.white;
        fingerImg.raycastTarget = false;
        fingerGO.SetActive(false);

        // ── Wire up TutorialAnimator ─────────────────────────────────
        var tutAnim = pnlTutorial.AddComponent<TutorialAnimator>();
        tutAnim.segsToTap = tapList.ToArray();
        tutAnim.allSegs = allLines;
        tutAnim.finger = fingerImg;
        tutAnim.pulseRing = pulseImg;
        tutAnim.lblHint = lblHint;
        tutAnim.hintBg = hintCardGO;
        tutAnim.lblCongrats = lblCongrats;

        // ── Back arrow (bottom-left) ─────────────────────────────────
        BackArrowButton(tt, "btn_tut_back", V2(0,0), V2(0,0), V2(70, 60), 80, ACCENT_DARK);

        pnlTutorial.SetActive(false);

        // ══════════════════════════════════════════════════════════════════
        //  pnl_modeSelect — Game Mode Selection
        // ══════════════════════════════════════════════════════════════════
        var pnlMode = Panel(ct, "pnl_modeSelect", V2(0,0), V2(1,1), V2(0,0), V2(0,0));
        var ms = pnlMode.transform;

        // Dark bg
        RoundImg(ms, "ModeBG", V2(0,0), V2(1,1), V2(0,0), V2(0,0), Hex("#0A0E1AFA")).raycastTarget = false;

        // Title
        DigTxt(ms, "lbl_mode_title", "SELECT MODE", V2(.5f,1), V2(.5f,1), V2(0,-80), V2(900,72), 48, TEXT_PRIMARY).raycastTarget = false;

        RoundImg(ms, "ModeAccent", V2(.5f,1), V2(.5f,1), V2(0,-130), V2(120,3), ACCENT).raycastTarget = false;

        // ── EASY (green) ─────────────────────────────────────────────
        Color modeGreen  = Hex("#76FF03");
        Color modeYellow = Hex("#FFD600");
        Color modeRed    = Hex("#FF1744");

        var btnEasyGO = SegButton(ms, "card_easy", "EASY", V2(.5f,.5f), V2(.5f,.5f), V2(0,160),
            V2(600, 140), 46, modeGreen);
        DigTxt(ms, "lbl_easy_desc", "2 DIGITS", V2(.5f,.5f), V2(.5f,.5f), V2(0,75), V2(900,24), 16, modeGreen).raycastTarget = false;
        var btnEasy = btnEasyGO.transform.Find("btn_face").GetComponent<Button>();

        // ── MEDIUM (yellow) ──────────────────────────────────────────
        var btnMedGO = SegButton(ms, "card_medium", "MEDIUM", V2(.5f,.5f), V2(.5f,.5f), V2(0,-30),
            V2(600, 140), 46, modeYellow);
        DigTxt(ms, "lbl_med_desc", "3 DIGITS", V2(.5f,.5f), V2(.5f,.5f), V2(0,-115), V2(900,24), 16, modeYellow).raycastTarget = false;
        var btnMed = btnMedGO.transform.Find("btn_face").GetComponent<Button>();

        // ── HARD (red) ───────────────────────────────────────────────
        var btnHardGO = SegButton(ms, "card_hard", "HARD", V2(.5f,.5f), V2(.5f,.5f), V2(0,-220),
            V2(600, 140), 46, modeRed);
        DigTxt(ms, "lbl_hard_desc", "3 NUMBERS - 2 OPERATORS", V2(.5f,.5f), V2(.5f,.5f), V2(0,-305), V2(900,21), 14, modeRed).raycastTarget = false;
        var btnHard = btnHardGO.transform.Find("btn_face").GetComponent<Button>();

        // ── BACK button (bottom-right) ───────────────────────────────
        BackArrowButton(ms, "btn_mode_back", V2(0,0), V2(0,0), V2(70, 60), 80, ACCENT_DARK);

        pnlMode.SetActive(false);

        // ══════════════════════════════════════════════════════════════════
        //  pnl_main — Game Screen (redesigned)
        // ══════════════════════════════════════════════════════════════════
        var pnlMain = Panel(ct, "pnl_main", V2(0,0), V2(1,1), V2(0,0), V2(0,0));
        var mt = pnlMain.transform;

        // Warm dark-gold background for game levels
        Img(mt, "GameBG", V2(0,0), V2(1,1), V2(0,0), V2(0,0), Hex("#141208")).raycastTarget = false;

        // ── Header bar (minimalist) ──────────────────────────────────────
        Img(mt, "HeaderLine", V2(0,1), V2(1,1), V2(0,-110), V2(0,3), Hex("#F59E0B15")).raycastTarget = false;

        // Best score — top left, aligned with speaker icon row
        DigTxt(mt, "lbl_best_label", "BEST", V2(0,1), V2(0,1), V2(60,-160), V2(900,33), 22, TEXT_MUTED).raycastTarget = false;
        var lblHS = Txt(mt, "lbl_highscore", "0",
            V2(0,1), V2(0,1), V2(130,-155), V2(160,60), 52, ACCENT_LIGHT,
            TextAnchor.MiddleLeft, FontStyle.Bold);

        // ── Timer section ────────────────────────────────────────────────
        DigTxt(mt, "lbl_time_label", "TIME REMAINING", V2(.5f,1), V2(.5f,1), V2(0,-152), V2(900,24), 16, TEXT_MUTED).raycastTarget = false;

        var lblTimer = Txt(mt, "lbl_timer", "90:00",
            V2(.5f,1), V2(.5f,1), V2(0,-210), V2(460,70), 58, TEXT_PRIMARY,
            TextAnchor.MiddleCenter, FontStyle.Bold);

        // Timer bar with rounded ends
        var timerBarBg = RoundImg(mt, "timer_bar_bg", V2(.5f,1), V2(.5f,1), V2(0,-258), V2(860,10), TIMER_BG);
        timerBarBg.raycastTarget = false;
        var barFill = RoundImg(mt, "timer_bar_fill", V2(.5f,1), V2(.5f,1), V2(0,-258), V2(860,10), ACCENT);
        barFill.type = Image.Type.Filled;
        barFill.fillMethod = Image.FillMethod.Horizontal;
        barFill.fillOrigin = 0; barFill.fillAmount = 1f;
        barFill.raycastTarget = false;

        // ── Equation container ───────────────────────────────────────────
        var eqBg = RoundImg(mt, "EqBG", V2(.5f,.5f), V2(.5f,.5f), V2(0, 0), V2(920,880), Hex("#0F172A80"));
        eqBg.raycastTarget = false;

        var eqGO = Panel(mt, "Equation", V2(.5f,.5f), V2(.5f,.5f), V2(0,0), V2(860,840));
        var eq = eqGO.transform;

        float numX = 50f;

        var num1 = NumberGroup(eq, "Number1", V2(numX, 270));
        var pm   = PlusMinusToggle(eq, V2(-200, 0));
        var num2 = NumberGroup(eq, "Number2", V2(numX, 0));

        // Gold divider line with glow
        var divGlow = RoundImg(eq, "DividerGlow", V2(.5f,.5f), V2(.5f,.5f), V2(numX, -150),
            V2(DW * 2 + 120, 8), Hex("#F59E0B20"));
        divGlow.raycastTarget = false;
        RoundImg(eq, "Divider", V2(.5f,.5f), V2(.5f,.5f), V2(numX, -150),
            V2(DW * 2 + 100, 3), DIVIDER_C).raycastTarget = false;

        var ans = NumberGroup(eq, "Answer", V2(numX, -290));

        // Equals sign for landscape (hidden in portrait)
        var eqSign = Txt(eq, "lbl_equals", "=", V2(.5f,.5f), V2(.5f,.5f), V2(0,0), V2(60,80), 60, ACCENT,
            TextAnchor.MiddleCenter, FontStyle.Bold);
        eqSign.gameObject.SetActive(false);

        // EquationLayout for Easy mode
        var elEasy = eqGO.AddComponent<EquationLayout>();
        elEasy.container = eqGO.GetComponent<RectTransform>();
        elEasy.containerPortraitSize = new Vector2(860, 840);
        elEasy.containerLandscapeSize = new Vector2(1600, 300);
        elEasy.portraitScale = 1f;
        elEasy.landscapeScale = 0.65f;
        elEasy.divider = eq.Find("Divider").GetComponent<RectTransform>();
        elEasy.dividerGlow = eq.Find("DividerGlow").GetComponent<RectTransform>();
        elEasy.equalsSign = eqSign.rectTransform;
        elEasy.eqBackground = eqBg.rectTransform;
        float lx = -500f; // landscape positions
        elEasy.elements = new EquationLayout.ElementPos[] {
            new EquationLayout.ElementPos { rt = num1.GetComponent<RectTransform>(), portraitPos = V2(numX, 270), landscapePos = V2(lx, 0) },
            new EquationLayout.ElementPos { rt = pm.GetComponent<RectTransform>(), portraitPos = V2(-200, 0), landscapePos = V2(lx + 260, 0) },
            new EquationLayout.ElementPos { rt = num2.GetComponent<RectTransform>(), portraitPos = V2(numX, 0), landscapePos = V2(lx + 500, 0) },
            new EquationLayout.ElementPos { rt = eqSign.rectTransform, portraitPos = V2(0, 0), landscapePos = V2(lx + 700, 0) },
            new EquationLayout.ElementPos { rt = ans.GetComponent<RectTransform>(), portraitPos = V2(numX, -290), landscapePos = V2(lx + 900, 0) },
        };

        // ── 3-digit equation (Medium mode) ───────────────────────────────
        var eqBg3 = RoundImg(mt, "EqBG3", V2(.5f,.5f), V2(.5f,.5f), V2(0, 0), V2(920,880), Hex("#0F172A80"));
        eqBg3.raycastTarget = false;

        var eqGO3 = Panel(mt, "Equation3D", V2(.5f,.5f), V2(.5f,.5f), V2(0,0), V2(900,840));
        var eq3 = eqGO3.transform;
        // Scale down to fit 3-digit numbers (digits are bigger now)
        eqGO3.transform.localScale = new Vector3(0.75f, 0.75f, 1f);

        float numX3 = 60f;

        var num1_3d = NumberGroup3(eq3, "Number1_3D", V2(numX3, 270));
        var pm3d    = PlusMinusToggle(eq3, V2(-240, 0));
        var num2_3d = NumberGroup3(eq3, "Number2_3D", V2(numX3, 0));

        // Gold divider
        RoundImg(eq3, "DivGlow3", V2(.5f,.5f), V2(.5f,.5f), V2(numX3, -150),
            V2(DW * 3 + 120, 8), Hex("#F59E0B20")).raycastTarget = false;
        RoundImg(eq3, "Divider3", V2(.5f,.5f), V2(.5f,.5f), V2(numX3, -150),
            V2(DW * 3 + 100, 3), DIVIDER_C).raycastTarget = false;

        var ans_3d = NumberGroup3(eq3, "Answer_3D", V2(numX3, -290));

        // Equals sign for landscape (hidden in portrait)
        var eqSign3 = Txt(eq3, "lbl_equals3", "=", V2(.5f,.5f), V2(.5f,.5f), V2(0,0), V2(60,80), 60, ACCENT,
            TextAnchor.MiddleCenter, FontStyle.Bold);
        eqSign3.gameObject.SetActive(false);

        // EquationLayout for Medium mode
        var elMed = eqGO3.AddComponent<EquationLayout>();
        elMed.container = eqGO3.GetComponent<RectTransform>();
        elMed.containerPortraitSize = new Vector2(900, 840);
        elMed.containerLandscapeSize = new Vector2(1800, 300);
        elMed.portraitScale = 0.75f;
        elMed.landscapeScale = 0.50f;
        elMed.divider = eq3.Find("Divider3").GetComponent<RectTransform>();
        elMed.dividerGlow = eq3.Find("DivGlow3").GetComponent<RectTransform>();
        elMed.equalsSign = eqSign3.rectTransform;
        elMed.eqBackground = eqBg3.rectTransform;
        float lx3 = -600f;
        elMed.elements = new EquationLayout.ElementPos[] {
            new EquationLayout.ElementPos { rt = num1_3d.GetComponent<RectTransform>(), portraitPos = V2(numX3, 270), landscapePos = V2(lx3, 0) },
            new EquationLayout.ElementPos { rt = pm3d.GetComponent<RectTransform>(), portraitPos = V2(-240, 0), landscapePos = V2(lx3 + 320, 0) },
            new EquationLayout.ElementPos { rt = num2_3d.GetComponent<RectTransform>(), portraitPos = V2(numX3, 0), landscapePos = V2(lx3 + 640, 0) },
            new EquationLayout.ElementPos { rt = eqSign3.rectTransform, portraitPos = V2(0, 0), landscapePos = V2(lx3 + 920, 0) },
            new EquationLayout.ElementPos { rt = ans_3d.GetComponent<RectTransform>(), portraitPos = V2(numX3, -290), landscapePos = V2(lx3 + 1180, 0) },
        };

        // Hide 3-digit panel by default
        eqBg3.gameObject.SetActive(false);
        eqGO3.SetActive(false);

        // ── Hard mode equation (A ± B ± C = D, 2 digits each) ─────────
        var eqBgH = RoundImg(mt, "EqBGHard", V2(.5f,.5f), V2(.5f,.5f), V2(0, 0), V2(920,1200), Hex("#0F172A80"));
        eqBgH.raycastTarget = false;

        var eqGOH = Panel(mt, "EquationHard", V2(.5f,.5f), V2(.5f,.5f), V2(0, 0), V2(880,1150));
        var eqH = eqGOH.transform;
        eqGOH.transform.localScale = new Vector3(0.72f, 0.72f, 1f);

        float hx = 50f;
        float rowH = 260f; // generous vertical spacing

        var num1H = NumberGroup(eqH, "Number1_H", V2(hx, rowH * 1.5f));
        var pmH1  = PlusMinusToggle(eqH, V2(-210, rowH * 0.75f));
        var num2H = NumberGroup(eqH, "Number2_H", V2(hx, rowH * 0.5f));
        var pmH2  = PlusMinusToggle(eqH, V2(-210, -rowH * 0.25f));
        var num3H = NumberGroup(eqH, "Number3_H", V2(hx, -rowH * 0.5f));

        // Gold divider
        float divY = -rowH * 1.05f;
        RoundImg(eqH, "DivGlowH", V2(.5f,.5f), V2(.5f,.5f), V2(hx, divY),
            V2(DW * 2 + 120, 8), Hex("#F59E0B20")).raycastTarget = false;
        RoundImg(eqH, "DividerH", V2(.5f,.5f), V2(.5f,.5f), V2(hx, divY),
            V2(DW * 2 + 100, 3), DIVIDER_C).raycastTarget = false;

        var ansH = NumberGroup(eqH, "Answer_H", V2(hx, -rowH * 1.5f));

        // Equals sign for landscape (hidden in portrait)
        var eqSignH = Txt(eqH, "lbl_equalsH", "=", V2(.5f,.5f), V2(.5f,.5f), V2(0,0), V2(60,80), 60, ACCENT,
            TextAnchor.MiddleCenter, FontStyle.Bold);
        eqSignH.gameObject.SetActive(false);

        // EquationLayout for Hard mode
        var elHard = eqGOH.AddComponent<EquationLayout>();
        elHard.container = eqGOH.GetComponent<RectTransform>();
        elHard.containerPortraitSize = new Vector2(880, 1150);
        elHard.containerLandscapeSize = new Vector2(2000, 300);
        elHard.portraitScale = 0.72f;
        elHard.landscapeScale = 0.42f;
        elHard.divider = eqH.Find("DividerH").GetComponent<RectTransform>();
        elHard.dividerGlow = eqH.Find("DivGlowH").GetComponent<RectTransform>();
        elHard.equalsSign = eqSignH.rectTransform;
        elHard.eqBackground = eqBgH.rectTransform;
        float lxH = -700f;
        float spcH = 270f;
        elHard.elements = new EquationLayout.ElementPos[] {
            new EquationLayout.ElementPos { rt = num1H.GetComponent<RectTransform>(), portraitPos = V2(hx, rowH*1.5f), landscapePos = V2(lxH, 0) },
            new EquationLayout.ElementPos { rt = pmH1.GetComponent<RectTransform>(), portraitPos = V2(-210, rowH*0.75f), landscapePos = V2(lxH + spcH*0.8f, 0) },
            new EquationLayout.ElementPos { rt = num2H.GetComponent<RectTransform>(), portraitPos = V2(hx, rowH*0.5f), landscapePos = V2(lxH + spcH*1.6f, 0) },
            new EquationLayout.ElementPos { rt = pmH2.GetComponent<RectTransform>(), portraitPos = V2(-210, -rowH*0.25f), landscapePos = V2(lxH + spcH*2.4f, 0) },
            new EquationLayout.ElementPos { rt = num3H.GetComponent<RectTransform>(), portraitPos = V2(hx, -rowH*0.5f), landscapePos = V2(lxH + spcH*3.2f, 0) },
            new EquationLayout.ElementPos { rt = eqSignH.rectTransform, portraitPos = V2(0, 0), landscapePos = V2(lxH + spcH*4.0f, 0) },
            new EquationLayout.ElementPos { rt = ansH.GetComponent<RectTransform>(), portraitPos = V2(hx, -rowH*1.5f), landscapePos = V2(lxH + spcH*4.8f, 0) },
        };

        eqBgH.gameObject.SetActive(false);
        eqGOH.SetActive(false);

        // Hint with icon-like styling
        var hintBg = RoundImg(mt, "HintBG", V2(.5f,0), V2(.5f,0), V2(0,110), V2(780,60), Hex("#1E293B60"));
        hintBg.raycastTarget = false;
        DigTxt(mt, "lbl_hint", "TAP STICKS - BUILD DIGITS", V2(.5f,0), V2(.5f,0), V2(0,112), V2(900,21), 14, TEXT_MUTED).raycastTarget = false;

        pnlMain.SetActive(false);

        // ══════════════════════════════════════════════════════════════════
        //  pnl_continue — Result Screen (redesigned)
        // ══════════════════════════════════════════════════════════════════
        var pnlCont = ContinuePanel(ct);
        pnlCont.SetActive(false);

        // ── Volume icon (top right corner) ───────────────────────────────
        var btnVolGO = new GameObject("btn_volume");
        btnVolGO.transform.SetParent(ct, false);
        var btnVolRt = btnVolGO.AddComponent<RectTransform>();
        btnVolRt.anchorMin = btnVolRt.anchorMax = new Vector2(1, 1);
        btnVolRt.pivot = new Vector2(1, 1);
        btnVolRt.anchoredPosition = new Vector2(-15, -160);
        btnVolRt.sizeDelta = new Vector2(70, 70);
        var btnVolImg = btnVolGO.AddComponent<Image>();
        btnVolImg.sprite = MakeSpeakerSprite(false);
        btnVolImg.color = Color.white;
        btnVolImg.raycastTarget = true;
        var btnVolBtn = btnVolGO.AddComponent<Button>();
        btnVolBtn.targetGraphic = btnVolImg;
        var colorBlock = ColorBlock.defaultColorBlock;
        colorBlock.normalColor = Color.white;
        colorBlock.highlightedColor = new Color(0.85f, 0.85f, 0.85f);
        colorBlock.pressedColor = new Color(0.7f, 0.7f, 0.7f);
        btnVolBtn.colors = colorBlock;

        // Muted icon (hidden by default)
        var mutedGO = new GameObject("img_muted");
        mutedGO.transform.SetParent(btnVolGO.transform, false);
        var mutedRt = mutedGO.AddComponent<RectTransform>();
        mutedRt.anchorMin = Vector2.zero;
        mutedRt.anchorMax = Vector2.one;
        mutedRt.offsetMin = mutedRt.offsetMax = Vector2.zero;
        var mutedImg = mutedGO.AddComponent<Image>();
        mutedImg.sprite = MakeSpeakerSprite(true);
        mutedImg.color = Color.white;
        mutedImg.raycastTarget = false;
        mutedGO.SetActive(false);

        // NOTE: SetAsLastSibling moved to after all panels are created

        // ── pnl_fader ────────────────────────────────────────────────────
        var pnlFader = new GameObject("pnl_fader");
        pnlFader.transform.SetParent(ct, false);
        Stretch(pnlFader);
        var faderImg = pnlFader.AddComponent<Image>();
        faderImg.color = new Color(0,0,0,0);
        faderImg.raycastTarget = false;
        pnlFader.SetActive(false);

        // ══════════════════════════════════════════════════════════════════
        //  Arcade Panels
        // ══════════════════════════════════════════════════════════════════

        Color arcCol = ACCENT;
        Color arcDim = ACCENT_DARK;

        // ── pnl_arcadeModeSelect ─────────────────────────────────────────
        var pnlArcMode = Panel(ct, "pnl_arcadeModeSelect", V2(0,0), V2(1,1), V2(0,0), V2(0,0));
        var amt = pnlArcMode.transform;
        RoundImg(amt, "ArcModeBG", V2(0,0), V2(1,1), V2(0,0), V2(0,0), Hex("#0A0E1AFA")).raycastTarget = false;

        DigTxt(amt, "lbl_arcmode_title", "ARCADE 1V1", V2(.5f,1), V2(.5f,1), V2(0,-70), V2(900,63), 42, ACCENT).raycastTarget = false;
        RoundImg(amt, "ArcModeAccent", V2(.5f,1), V2(.5f,1), V2(0,-115), V2(120,3), ACCENT).raycastTarget = false;

        // Mode title (updates dynamically)
        var lblArcModeTitle = Txt(amt, "lbl_arcmode_info", "EASY - FIRST TO 3",
            V2(.5f,1), V2(.5f,1), V2(0,-150), V2(500,30), 16, TEXT_MUTED,
            TextAnchor.MiddleCenter, FontStyle.Normal);

        // Mode buttons
        Color mGreen2  = Hex("#76FF03");
        Color mYellow2 = Hex("#FFD600");
        Color mRed2    = Hex("#FF1744");
        Color mCyan    = Hex("#00E5FF");

        var abEasy = SegButton(amt, "btn_arc_easy", "EASY", V2(.5f,1), V2(.5f,1), V2(0,-220),
            V2(380, 80), 28, mGreen2);
        var abMed = SegButton(amt, "btn_arc_medium", "MEDIUM", V2(.5f,1), V2(.5f,1), V2(0,-320),
            V2(380, 80), 28, mYellow2);
        var abHard = SegButton(amt, "btn_arc_hard", "HARD", V2(.5f,1), V2(.5f,1), V2(0,-420),
            V2(380, 80), 28, mRed2);
        var abRand = SegButton(amt, "btn_arc_random", "RANDOM", V2(.5f,1), V2(.5f,1), V2(0,-520),
            V2(380, 80), 28, mCyan);

        // First-to buttons
        DigTxt(amt, "lbl_firstto", "FIRST TO", V2(.5f,1), V2(.5f,1), V2(0,-630), V2(900,24), 16, TEXT_MUTED).raycastTarget = false;

        var abFt3 = SegButton(amt, "btn_ft3", "3", V2(.5f,1), V2(.5f,1), V2(-140,-690),
            V2(110, 70), 28, arcCol);
        var abFt5 = SegButton(amt, "btn_ft5", "5", V2(.5f,1), V2(.5f,1), V2(0,-690),
            V2(110, 70), 28, arcDim);
        var abFt7 = SegButton(amt, "btn_ft7", "7", V2(.5f,1), V2(.5f,1), V2(140,-690),
            V2(110, 70), 28, arcDim);

        // Action buttons
        var abRandom = SegButton(amt, "btn_random_battle", "RANDOM BATTLE", V2(.5f,1), V2(.5f,1), V2(0,-810),
            V2(420, 90), 30, ACCENT);
        var abInvite = SegButton(amt, "btn_show_lobby", "INVITE PLAYER", V2(.5f,1), V2(.5f,1), V2(0,-920),
            V2(420, 80), 26, arcDim);

        // Back
        var abBack = BackArrowButton(amt, "btn_arc_back", V2(0,0), V2(0,0), V2(70, 60), 80, arcDim);

        pnlArcMode.SetActive(false);

        // ── pnl_lobby ────────────────────────────────────────────────────
        var pnlLobby = Panel(ct, "pnl_lobby", V2(0,0), V2(1,1), V2(0,0), V2(0,0));
        var lbt = pnlLobby.transform;
        RoundImg(lbt, "LobbyBG", V2(0,0), V2(1,1), V2(0,0), V2(0,0), Hex("#0A0E1AFA")).raycastTarget = false;

        DigTxt(lbt, "lbl_lobby_title", "ONLINE PLAYERS", V2(.5f,1), V2(.5f,1), V2(0,-70), V2(900,54), 36, ACCENT).raycastTarget = false;

        var lblLobbyStatus = Txt(lbt, "lbl_lobby_status", "0 ONLINE",
            V2(.5f,1), V2(.5f,1), V2(0,-120), V2(400,30), 16, TEXT_MUTED,
            TextAnchor.MiddleCenter, FontStyle.Normal);

        // Search field
        var searchGO = new GameObject("inp_search");
        searchGO.transform.SetParent(lbt, false);
        var searchRt = searchGO.AddComponent<RectTransform>();
        searchRt.anchorMin = V2(.5f,1); searchRt.anchorMax = V2(.5f,1);
        searchRt.anchoredPosition = V2(0,-170); searchRt.sizeDelta = V2(500,50);
        var searchBg = searchGO.AddComponent<Image>();
        searchBg.color = Hex("#1A1F2E");
        var searchInp = searchGO.AddComponent<InputField>();

        var searchPlaceholder = Txt(searchGO.transform, "Placeholder", "SEARCH...",
            V2(0,0), V2(1,1), V2(10,0), V2(-20,0), 18, TEXT_MUTED,
            TextAnchor.MiddleLeft, FontStyle.Italic);
        var searchPlaceholderRt = searchPlaceholder.GetComponent<RectTransform>();
        searchPlaceholderRt.anchorMin = V2(0,0); searchPlaceholderRt.anchorMax = V2(1,1);
        searchPlaceholderRt.offsetMin = V2(15,0); searchPlaceholderRt.offsetMax = V2(-15,0);

        var searchText = Txt(searchGO.transform, "Text", "",
            V2(0,0), V2(1,1), V2(10,0), V2(-20,0), 18, TEXT_PRIMARY,
            TextAnchor.MiddleLeft, FontStyle.Normal);
        var searchTextRt = searchText.GetComponent<RectTransform>();
        searchTextRt.anchorMin = V2(0,0); searchTextRt.anchorMax = V2(1,1);
        searchTextRt.offsetMin = V2(15,0); searchTextRt.offsetMax = V2(-15,0);

        searchInp.textComponent = searchText;
        searchInp.placeholder = searchPlaceholder;

        // Scroll view for user list
        var scrollGO = new GameObject("ScrollView");
        scrollGO.transform.SetParent(lbt, false);
        var scrollRt = scrollGO.AddComponent<RectTransform>();
        scrollRt.anchorMin = V2(0,0); scrollRt.anchorMax = V2(1,1);
        scrollRt.offsetMin = V2(40,120); scrollRt.offsetMax = V2(-40,-210);
        scrollGO.AddComponent<Image>().color = new Color(0,0,0,0.01f);
        var scrollRect = scrollGO.AddComponent<UnityEngine.UI.ScrollRect>();
        scrollRect.horizontal = false;
        scrollGO.AddComponent<Mask>().showMaskGraphic = false;

        var contentGO = new GameObject("Content");
        contentGO.transform.SetParent(scrollGO.transform, false);
        var contentRt = contentGO.AddComponent<RectTransform>();
        contentRt.anchorMin = V2(0,1); contentRt.anchorMax = V2(1,1);
        contentRt.pivot = V2(0.5f,1); contentRt.sizeDelta = V2(0,0);
        var vlg = contentGO.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 8;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        var csf = contentGO.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scrollRect.content = contentRt;

        // User row prefab (template)
        var userRowGO = new GameObject("UserRowTemplate");
        userRowGO.transform.SetParent(lbt, false);
        var urRt = userRowGO.AddComponent<RectTransform>();
        urRt.sizeDelta = V2(0,70);
        var urImg = userRowGO.AddComponent<Image>();
        urImg.color = Hex("#151A28");
        var urLE = userRowGO.AddComponent<LayoutElement>();
        urLE.preferredHeight = 70;

        Txt(userRowGO.transform, "lbl_name", "Player",
            V2(0,.5f), V2(0,.5f), V2(30,0), V2(300,40), 22, TEXT_PRIMARY,
            TextAnchor.MiddleLeft, FontStyle.Normal);

        var invBtnGO = SegButton(userRowGO.transform, "btn_invite", "INVITE",
            V2(1,.5f), V2(1,.5f), V2(-80,0), V2(120,50), 18, arcCol);

        userRowGO.SetActive(false); // template, not visible

        var lbBack = BackArrowButton(lbt, "btn_lobby_back", V2(0,0), V2(0,0), V2(70, 60), 80, arcDim);

        pnlLobby.SetActive(false);

        // ── pnl_arcadeWaiting ────────────────────────────────────────────
        var pnlArcWait = Panel(ct, "pnl_arcadeWaiting", V2(0,0), V2(1,1), V2(0,0), V2(0,0));
        var awt = pnlArcWait.transform;
        RoundImg(awt, "WaitBG", V2(0,0), V2(1,1), V2(0,0), V2(0,0), Hex("#0A0E1AFA")).raycastTarget = false;

        var lblWaiting = Txt(awt, "lbl_waiting_status", "SEARCHING...",
            V2(.5f,.5f), V2(.5f,.5f), V2(0,40), V2(600,60), 36, ACCENT,
            TextAnchor.MiddleCenter, FontStyle.Bold);

        // Dots animation hint
        DigTxt(awt, "lbl_wait_hint", "PLEASE WAIT", V2(.5f,.5f), V2(.5f,.5f), V2(0,-20), V2(900,24), 16, TEXT_MUTED).raycastTarget = false;

        var awCancel = SegButton(awt, "btn_cancel_search", "CANCEL", V2(.5f,.5f), V2(.5f,.5f), V2(0,-120),
            V2(300, 70), 24, arcDim);

        pnlArcWait.SetActive(false);

        // ── pnl_arcadeHUD (overlay, not fullscreen) ──────────────────────
        var pnlArcHUD = Panel(ct, "pnl_arcadeHUD", V2(0,1), V2(1,1), V2(0,0), V2(0,130));
        var ahrt = pnlArcHUD.GetComponent<RectTransform>();
        ahrt.anchorMin = V2(0,1); ahrt.anchorMax = V2(1,1);
        ahrt.pivot = V2(0.5f,1); ahrt.anchoredPosition = V2(0,0); ahrt.sizeDelta = V2(0,130);
        var aht = pnlArcHUD.transform;
        Img(aht, "HudBG", V2(0,0), V2(1,1), V2(0,0), V2(0,0), Hex("#0D111DDD")).raycastTarget = false;

        var lblOppName = Txt(aht, "lbl_opp_name", "OPPONENT",
            V2(.5f,1), V2(.5f,1), V2(0,-15), V2(400,30), 16, TEXT_MUTED,
            TextAnchor.MiddleCenter, FontStyle.Normal);

        var lblRoundInfo = Txt(aht, "lbl_round_info", "ROUND 1",
            V2(.5f,1), V2(.5f,1), V2(0,-45), V2(300,30), 14, TEXT_DIM,
            TextAnchor.MiddleCenter, FontStyle.Normal);

        // Scores
        var lblMyScore = Txt(aht, "lbl_my_score", "0",
            V2(.25f,.5f), V2(.25f,.5f), V2(0,-10), V2(100,50), 40, ACCENT_LIGHT,
            TextAnchor.MiddleCenter, FontStyle.Bold);
        DigTxt(aht, "lbl_vs", "VS", V2(.5f,.5f), V2(.5f,.5f), V2(0,-10), V2(900,27), 18, TEXT_DIM).raycastTarget = false;
        var lblOppScore = Txt(aht, "lbl_opp_score", "0",
            V2(.75f,.5f), V2(.75f,.5f), V2(0,-10), V2(100,50), 40, Hex("#FF6B6B"),
            TextAnchor.MiddleCenter, FontStyle.Bold);

        DigTxt(aht, "lbl_you", "YOU", V2(.25f,0), V2(.25f,0), V2(0,15), V2(900,18), 12, TEXT_MUTED).raycastTarget = false;
        DigTxt(aht, "lbl_them", "THEM", V2(.75f,0), V2(.75f,0), V2(0,15), V2(900,18), 12, TEXT_MUTED).raycastTarget = false;

        pnlArcHUD.SetActive(false);

        // ── pnl_arcadeResult ─────────────────────────────────────────────
        var pnlArcResult = Panel(ct, "pnl_arcadeResult", V2(0,0), V2(1,1), V2(0,0), V2(0,0));
        var art = pnlArcResult.transform;
        RoundImg(art, "ResultBG", V2(0,0), V2(1,1), V2(0,0), V2(0,0), Hex("#0A0E1AFA")).raycastTarget = false;

        var lblResTitle = Txt(art, "lbl_result_title", "YOU WIN!",
            V2(.5f,.5f), V2(.5f,.5f), V2(0,120), V2(600,80), 56, ACCENT,
            TextAnchor.MiddleCenter, FontStyle.Bold);

        var lblResScore = Txt(art, "lbl_result_score", "3 - 1",
            V2(.5f,.5f), V2(.5f,.5f), V2(0,30), V2(400,60), 48, TEXT_PRIMARY,
            TextAnchor.MiddleCenter, FontStyle.Bold);

        var lblResDetail = Txt(art, "lbl_result_detail", "VS OPPONENT",
            V2(.5f,.5f), V2(.5f,.5f), V2(0,-30), V2(400,30), 18, TEXT_MUTED,
            TextAnchor.MiddleCenter, FontStyle.Normal);

        var arRematch = SegButton(art, "btn_rematch", "REMATCH", V2(.5f,.5f), V2(.5f,.5f), V2(0,-120),
            V2(380, 80), 28, arcCol);
        var arLobby = SegButton(art, "btn_return_lobby", "RETURN TO LOBBY", V2(.5f,.5f), V2(.5f,.5f), V2(0,-220),
            V2(380, 70), 22, arcDim);
        var arMenu = SegButton(art, "btn_return_menu", "MAIN MENU", V2(.5f,.5f), V2(.5f,.5f), V2(0,-310),
            V2(380, 70), 22, arcDim);

        pnlArcResult.SetActive(false);

        // ── pnl_invitePopup ──────────────────────────────────────────────
        var pnlInvite = Panel(ct, "pnl_invitePopup", V2(0,0), V2(1,1), V2(0,0), V2(0,0));
        var ipt = pnlInvite.transform;
        // Semi-transparent overlay
        Img(ipt, "InvOverlay", V2(0,0), V2(1,1), V2(0,0), V2(0,0), new Color(0,0,0,0.7f)).raycastTarget = true;

        var invCard = Panel(ipt, "InvCard", V2(.5f,.5f), V2(.5f,.5f), V2(0,0), V2(500, 350));
        var ict = invCard.transform;
        RoundImg(ict, "InvCardBG", V2(0,0), V2(1,1), V2(0,0), V2(0,0), Hex("#151A28")).raycastTarget = false;

        DigTxt(ict, "lbl_inv_title", "MATCH INVITE", V2(.5f,1), V2(.5f,1), V2(0,-30), V2(900,42), 28, ACCENT).raycastTarget = false;

        var lblInvFrom = Txt(ict, "lbl_invite_from", "PLAYER",
            V2(.5f,1), V2(.5f,1), V2(0,-80), V2(400,40), 30, TEXT_PRIMARY,
            TextAnchor.MiddleCenter, FontStyle.Bold);

        var lblInvMode = Txt(ict, "lbl_invite_mode", "EASY - FIRST TO 3",
            V2(.5f,1), V2(.5f,1), V2(0,-130), V2(400,30), 18, TEXT_MUTED,
            TextAnchor.MiddleCenter, FontStyle.Normal);

        var invAccept = SegButton(ict, "btn_accept", "ACCEPT", V2(.5f,.5f), V2(.5f,.5f), V2(0,-10),
            V2(350, 70), 26, Hex("#76FF03"));
        var invDecline = SegButton(ict, "btn_decline", "DECLINE", V2(.5f,0), V2(.5f,0), V2(0,40),
            V2(350, 60), 22, Hex("#FF1744"));

        pnlInvite.SetActive(false);

        // ── pnl_roundOverlay (brief "ROUND WON/LOST" flash) ─────────────
        var pnlRoundOvr = Panel(ct, "pnl_roundOverlay", V2(0,0), V2(1,1), V2(0,0), V2(0,0));
        var rot = pnlRoundOvr.transform;
        Img(rot, "RoundOvrBG", V2(0,0), V2(1,1), V2(0,0), V2(0,0), new Color(0,0,0,0.8f)).raycastTarget = true;
        var lblRoundRes = Txt(rot, "lbl_round_result", "ROUND WON!",
            V2(.5f,.5f), V2(.5f,.5f), V2(0,0), V2(600,80), 52, ACCENT,
            TextAnchor.MiddleCenter, FontStyle.Bold);
        pnlRoundOvr.SetActive(false);

        // ══════════════════════════════════════════════════════════════════
        //  Audio Sources (on camera for spatial audio)
        // ══════════════════════════════════════════════════════════════════
        var musicSrc = camGO.AddComponent<AudioSource>();
        musicSrc.loop = true;
        musicSrc.volume = 0.5f;

        var sfxSrc = camGO.AddComponent<AudioSource>();
        sfxSrc.volume = 0.7f;

        // ══════════════════════════════════════════════════════════════════
        //  Managers
        // ══════════════════════════════════════════════════════════════════
        var mgrs = new GameObject("Managers");

        mgrs.AddComponent<AuthManager>();
        mgrs.AddComponent<UnityMainThreadDispatcher>();
        mgrs.AddComponent<FirebaseDBManager>();
        mgrs.AddComponent<LobbyManager>();
        mgrs.AddComponent<ArcadeMatchManager>();

        var arcGui = mgrs.AddComponent<ArcadeGUIManager>();
        arcGui.pnl_arcadeModeSelect = pnlArcMode;
        arcGui.pnl_lobby = pnlLobby;
        arcGui.pnl_arcadeWaiting = pnlArcWait;
        arcGui.pnl_arcadeHUD = pnlArcHUD;
        arcGui.pnl_arcadeResult = pnlArcResult;
        arcGui.pnl_invitePopup = pnlInvite;
        arcGui.lbl_modeSelectTitle = lblArcModeTitle;
        arcGui.inp_search = searchInp;
        arcGui.userListContent = contentRt;
        arcGui.userRowPrefab = userRowGO;
        arcGui.lbl_lobbyStatus = lblLobbyStatus;
        arcGui.lbl_waitingStatus = lblWaiting;
        arcGui.lbl_myScore = lblMyScore;
        arcGui.lbl_oppScore = lblOppScore;
        arcGui.lbl_roundInfo = lblRoundInfo;
        arcGui.lbl_oppName = lblOppName;
        arcGui.lbl_resultTitle = lblResTitle;
        arcGui.lbl_resultScore = lblResScore;
        arcGui.lbl_resultDetail = lblResDetail;
        arcGui.lbl_inviteFrom = lblInvFrom;
        arcGui.lbl_inviteMode = lblInvMode;
        arcGui.pnl_roundOverlay = pnlRoundOvr;
        arcGui.lbl_roundResult = lblRoundRes;

        var audioMgr = mgrs.AddComponent<AudioManager>();
        audioMgr.musicSource = musicSrc;
        audioMgr.sfxSource = sfxSrc;

        var cm = mgrs.AddComponent<ColorManager>();
        cm.ActiveNumberColor   = ACCENT;
        cm.PossibleNumberColor = SEG_OFF;
        cm.ImpossibleNumberColor = SEG_FIXED;
        cm.ActiveInnerColor = new Color(0,0,0,0);
        cm.PossibleImpossibleInnerColor = new Color(0,0,0,0);
        cm.BackgroundColor = BG;
        cm.WinGUIColor = WIN_COLOR;
        cm.LoseGUIColor = LOSE_COLOR;

        mgrs.AddComponent<GameSaver>();

        var tm = mgrs.AddComponent<TimerManager>();
        tm.StartTime = TIMER_MAX; tm.MinTime = 25f;

        var fader = mgrs.AddComponent<Fader>();
        fader.image = faderImg; fader.panel = pnlFader;

        var gm = mgrs.AddComponent<GameManager>();
        gm.number1 = num1; gm.number2 = num2; gm.answer = ans; gm.pm = pm;
        gm.number1_3d = num1_3d; gm.number2_3d = num2_3d; gm.answer_3d = ans_3d;
        gm.pm_3d = pm3d;
        gm.eqPanel2d = eqGO;
        gm.eqPanel3d = eqGO3;
        gm.eqBg2d = eqBg.gameObject;
        gm.eqBg3d = eqBg3.gameObject;
        gm.number1_hard = num1H; gm.number2_hard = num2H; gm.number3_hard = num3H;
        gm.answer_hard = ansH; gm.pm_hard1 = pmH1; gm.pm_hard2 = pmH2;
        gm.eqPanelHard = eqGOH;
        gm.eqBgHard = eqBgH.gameObject;

        var gui = mgrs.AddComponent<GUIManager>();
        gui.pnl_start = pnlStart;
        gui.pnl_modeSelect = pnlMode;
        gui.pnl_tutorial = pnlTutorial;
        gui.pnl_main = pnlMain;
        gui.pnl_continue = pnlCont;
        gui.pnl_fader = pnlFader;
        gui.lbl_timer = lblTimer;
        gui.lbl_highscore = lblHS;
        gui.lbl_startHighscore = lblStartHS;
        gui.timerBarFill = barFill;
        gui.timerMaxTime = TIMER_MAX;

        // ── Login and Register panels (created after gui exists) ──────────
        LoginPanel(ct, gui);
        RegisterPanel(ct, gui);
        ForgotPasswordPanel(ct, gui);

        var card = pnlCont.transform.Find("Card");
        gui.lbl_gameProgress = card.Find("lbl_score").GetComponent<Text>();
        gui.lbl_result    = card.Find("lbl_result").GetComponent<Text>();
        gui.lbl_inARow    = card.Find("lbl_inARow").GetComponent<Text>();
        gui.lbl_bestScore = card.Find("lbl_bestScore").GetComponent<Text>();
        gui.lbl_correctAnswer = card.Find("lbl_correctAnswer").GetComponent<Text>();
        gui.lbl_btnText   = card.Find("btn_continue/btn_face/lbl_btn").GetComponent<Text>();
        UnityEventTools.AddPersistentListener(
            card.Find("btn_continue/btn_face").GetComponent<Button>().onClick,
            gui.StartNewGame);
        UnityEventTools.AddPersistentListener(
            card.Find("btn_backToMenu/btn_face").GetComponent<Button>().onClick,
            gui.OnBackToMenuPressed);

        // Start page buttons
        UnityEventTools.AddPersistentListener(
            pnlStart.transform.Find("btn_play/btn_face").GetComponent<Button>().onClick,
            gui.OnPlayPressed);
        UnityEventTools.AddPersistentListener(
            pnlStart.transform.Find("btn_arcade/btn_face").GetComponent<Button>().onClick,
            gui.OnArcadePressed);
        UnityEventTools.AddPersistentListener(
            pnlStart.transform.Find("btn_tutorial/btn_face").GetComponent<Button>().onClick,
            gui.OnTutorialPressed);
        UnityEventTools.AddPersistentListener(
            pnlTutorial.transform.Find("btn_tut_back/btn_face").GetComponent<Button>().onClick,
            gui.OnTutorialBackPressed);

        // Mode selection buttons
        UnityEventTools.AddPersistentListener(btnEasy.onClick, gui.OnModeEasy);
        UnityEventTools.AddPersistentListener(btnMed.onClick, gui.OnModeMedium);
        UnityEventTools.AddPersistentListener(btnHard.onClick, gui.OnModeHard);
        UnityEventTools.AddPersistentListener(
            pnlMode.transform.Find("btn_mode_back/btn_face").GetComponent<Button>().onClick,
            gui.OnModeBackPressed);

        // ── Arcade button wiring ─────────────────────────────────────────
        // Mode select
        UnityEventTools.AddPersistentListener(
            abEasy.transform.Find("btn_face").GetComponent<Button>().onClick,
            arcGui.OnSelectEasy);
        UnityEventTools.AddPersistentListener(
            abMed.transform.Find("btn_face").GetComponent<Button>().onClick,
            arcGui.OnSelectMedium);
        UnityEventTools.AddPersistentListener(
            abHard.transform.Find("btn_face").GetComponent<Button>().onClick,
            arcGui.OnSelectHard);
        UnityEventTools.AddPersistentListener(
            abRand.transform.Find("btn_face").GetComponent<Button>().onClick,
            arcGui.OnSelectRandom);

        // First-to
        UnityEventTools.AddPersistentListener(
            abFt3.transform.Find("btn_face").GetComponent<Button>().onClick,
            arcGui.OnSelectFirstTo3);
        UnityEventTools.AddPersistentListener(
            abFt5.transform.Find("btn_face").GetComponent<Button>().onClick,
            arcGui.OnSelectFirstTo5);
        UnityEventTools.AddPersistentListener(
            abFt7.transform.Find("btn_face").GetComponent<Button>().onClick,
            arcGui.OnSelectFirstTo7);

        // Actions
        UnityEventTools.AddPersistentListener(
            abRandom.transform.Find("btn_face").GetComponent<Button>().onClick,
            arcGui.OnRandomBattlePressed);
        UnityEventTools.AddPersistentListener(
            abInvite.transform.Find("btn_face").GetComponent<Button>().onClick,
            arcGui.OnShowLobbyPressed);
        UnityEventTools.AddPersistentListener(
            abBack.transform.Find("btn_face").GetComponent<Button>().onClick,
            arcGui.OnArcadeBackToMenu);

        // Lobby
        UnityEventTools.AddPersistentListener(
            lbBack.transform.Find("btn_face").GetComponent<Button>().onClick,
            arcGui.OnLobbyBackPressed);
        // Search input wired at runtime via ArcadeGUIManager.Start()

        // Waiting
        UnityEventTools.AddPersistentListener(
            awCancel.transform.Find("btn_face").GetComponent<Button>().onClick,
            arcGui.OnCancelSearchPressed);

        // Invite popup
        UnityEventTools.AddPersistentListener(
            invAccept.transform.Find("btn_face").GetComponent<Button>().onClick,
            arcGui.OnAcceptInvitePressed);
        UnityEventTools.AddPersistentListener(
            invDecline.transform.Find("btn_face").GetComponent<Button>().onClick,
            arcGui.OnDeclineInvitePressed);

        // Result
        UnityEventTools.AddPersistentListener(
            arRematch.transform.Find("btn_face").GetComponent<Button>().onClick,
            arcGui.OnRematchPressed);
        UnityEventTools.AddPersistentListener(
            arLobby.transform.Find("btn_face").GetComponent<Button>().onClick,
            arcGui.OnReturnToLobbyPressed);
        UnityEventTools.AddPersistentListener(
            arMenu.transform.Find("btn_face").GetComponent<Button>().onClick,
            arcGui.OnArcadeBackToMenu);

        // Volume button - must be LAST sibling so it renders on top of all panels
        btnVolGO.transform.SetAsLastSibling();
        gui.img_volume_on = btnVolImg;
        gui.img_volume_muted = btnVolGO.transform.Find("img_muted").gameObject;
        UnityEventTools.AddPersistentListener(
            btnVolBtn.onClick,
            gui.OnVolumePressed);

        mgrs.AddComponent<MessengerCleaner>();

        // Save
        if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
            AssetDatabase.CreateFolder("Assets", "Scenes");
        bool ok = EditorSceneManager.SaveScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene(),
            "Assets/Scenes/MainScene.unity");
        AssetDatabase.Refresh();
        Debug.Log(ok ? "PlusMinus scene saved." : "Scene save FAILED.");
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Volumetric Button — 3D-look with shadow, face, highlight
    // ═══════════════════════════════════════════════════════════════════════

    // ═══════════════════════════════════════════════════════════════════════
    //  Segment-framed button — minimalist, 7-segment themed
    //  Frame = 6 bars like digit "0": top, bottom, TL, TR, BL, BR
    // ═══════════════════════════════════════════════════════════════════════
    static GameObject SegButton(Transform parent, string name, string label,
        Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size, int fontSize,
        Color segColor)
    {
        var container = Panel(parent, name, aMin, aMax, pos, size);
        var ct = container.transform;

        // Clickable face (transparent)
        var face = new GameObject("btn_face");
        face.transform.SetParent(ct, false);
        var faceRt = face.AddComponent<RectTransform>();
        faceRt.anchorMin = V2(0,0); faceRt.anchorMax = V2(1,1);
        faceRt.offsetMin = faceRt.offsetMax = Vector2.zero;
        var faceImg = face.AddComponent<Image>();
        faceImg.color = new Color(0, 0, 0, 0.01f);
        var btn = face.AddComponent<Button>();
        btn.targetGraphic = faceImg;
        var bc = ColorBlock.defaultColorBlock;
        bc.normalColor = Color.white;
        bc.highlightedColor = new Color(1.3f, 1.2f, 1.0f);
        bc.pressedColor = new Color(0.6f, 0.55f, 0.4f);
        btn.colors = bc;

        // ── 6 beveled segments forming rectangle frame ─────────────────
        float st = Mathf.Max(7f, size.y * 0.10f);  // border thickness
        float hw = size.x / 2f;
        float hh = size.y / 2f;
        float hLen = size.x;                         // full width (bevels make corner gaps)
        float vLen = size.y * 0.5f;                  // each half-height
        float vUpY =  hh * 0.5f;
        float vDnY = -hh * 0.5f;

        ImgSeg(ct, "s_top", V2(0, hh - st/2f),       V2(hLen, st), segColor, true);
        ImgSeg(ct, "s_bot", V2(0, -hh + st/2f),      V2(hLen, st), segColor, true);
        ImgSeg(ct, "s_tl",  V2(-hw + st/2f, vUpY),   V2(st, vLen), segColor, false);
        ImgSeg(ct, "s_bl",  V2(-hw + st/2f, vDnY),   V2(st, vLen), segColor, false);
        ImgSeg(ct, "s_tr",  V2( hw - st/2f, vUpY),   V2(st, vLen), segColor, false);
        ImgSeg(ct, "s_br",  V2( hw - st/2f, vDnY),   V2(st, vLen), segColor, false);

        // Digital font label
        DigTxt(face.transform, "lbl_btn", label,
            V2(0,0), V2(1,1), V2(0,0), V2(0,0), (int)(size.y * 0.50f), segColor);

        return container;
    }

    // Convenience overloads
    static GameObject SegButton(Transform parent, string name, string label,
        Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size, int fontSize)
    {
        return SegButton(parent, name, label, aMin, aMax, pos, size, fontSize, ACCENT);
    }

    // Button with segment frame + regular text (for dynamic text updates at runtime)
    static GameObject VolumetricButton(Transform parent, string name, string label,
        Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size, int fontSize,
        Color faceColor, Color topColor, Color shadowColor)
    {
        var container = Panel(parent, name, aMin, aMax, pos, size);
        var ct = container.transform;

        var face = new GameObject("btn_face");
        face.transform.SetParent(ct, false);
        var faceRt = face.AddComponent<RectTransform>();
        faceRt.anchorMin = V2(0,0); faceRt.anchorMax = V2(1,1);
        faceRt.offsetMin = faceRt.offsetMax = Vector2.zero;
        var faceImg = face.AddComponent<Image>();
        faceImg.color = new Color(0, 0, 0, 0.01f);
        var btn = face.AddComponent<Button>();
        btn.targetGraphic = faceImg;
        var bc = ColorBlock.defaultColorBlock;
        bc.normalColor = Color.white;
        bc.highlightedColor = new Color(1.3f, 1.2f, 1.0f);
        bc.pressedColor = new Color(0.6f, 0.55f, 0.4f);
        btn.colors = bc;

        // Segment frame border
        float st = Mathf.Max(7f, size.y * 0.10f);
        float hw = size.x / 2f;
        float hh = size.y / 2f;
        float hLen = size.x;
        float vLen = size.y * 0.5f;

        ImgSeg(ct, "s_top", V2(0, hh - st/2f),       V2(hLen, st), ACCENT, true);
        ImgSeg(ct, "s_bot", V2(0, -hh + st/2f),      V2(hLen, st), ACCENT, true);
        ImgSeg(ct, "s_tl",  V2(-hw + st/2f,  hh*0.5f),  V2(st, vLen), ACCENT, false);
        ImgSeg(ct, "s_bl",  V2(-hw + st/2f, -hh*0.5f),  V2(st, vLen), ACCENT, false);
        ImgSeg(ct, "s_tr",  V2( hw - st/2f,  hh*0.5f),  V2(st, vLen), ACCENT, false);
        ImgSeg(ct, "s_br",  V2( hw - st/2f, -hh*0.5f),  V2(st, vLen), ACCENT, false);

        // Digital font label
        var lblBtn = DigTxt(face.transform, "lbl_btn", label,
            V2(0,0), V2(1,1), V2(0,0), V2(0,0), (int)(size.y * 0.50f), ACCENT);
        lblBtn.raycastTarget = false;

        return container;
    }

    static Sprite MakeFingerSprite()
    {
        // Create a hand/pointer cursor sprite
        int s = 80;
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
        var clear = new Color(0, 0, 0, 0);
        var white = Color.white;
        var outline = new Color(0.3f, 0.3f, 0.3f, 1f);

        // Clear
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
                tex.SetPixel(x, y, clear);

        // Finger pointing up-left: tip at top-left, body going down-right
        // Fingertip circle
        int tipCX = 20, tipCY = s - 16;
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float dist = Mathf.Sqrt((x-tipCX)*(x-tipCX) + (y-tipCY)*(y-tipCY));
                if (dist < 10) tex.SetPixel(x, y, white);
                else if (dist < 12) tex.SetPixel(x, y, outline);
            }

        // Finger body going down from tip
        for (int i = 0; i < 50; i++)
        {
            int bx = tipCX + (int)(i * 0.35f);
            int by = tipCY - i;
            for (int dx = -7; dx <= 7; dx++)
                for (int dy = -2; dy <= 2; dy++)
                {
                    int px = bx + dx, py = by + dy;
                    if (px >= 0 && px < s && py >= 0 && py < s)
                    {
                        float edgeDist = Mathf.Abs(dx) / 7f;
                        if (edgeDist > 0.8f)
                            tex.SetPixel(px, py, outline);
                        else
                            tex.SetPixel(px, py, white);
                    }
                }
        }

        // Palm (wider area at bottom)
        for (int y = 8; y < 25; y++)
            for (int x = 20; x < 55; x++)
            {
                float edgeX = Mathf.Min(x - 20, 55 - x) / 5f;
                float edgeY = Mathf.Min(y - 8, 25 - y) / 5f;
                float edge = Mathf.Min(edgeX, edgeY);
                if (edge > 0)
                    tex.SetPixel(x, y, edge < 0.4f ? outline : white);
            }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.25f, 0.85f));
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Continue Panel (redesigned)
    // ═══════════════════════════════════════════════════════════════════════

    static GameObject ContinuePanel(Transform parent)
    {
        var ov = new GameObject("pnl_continue");
        ov.transform.SetParent(parent, false); Stretch(ov);
        ov.AddComponent<Image>().color = new Color(0,0,0,0);

        // Dim overlay
        Img(ov.transform, "Dim", V2(0,0), V2(1,1), V2(0,0), V2(0,0),
            new Color(0,0,0,0.70f)).raycastTarget = false;

        // Card with border effect
        var cardBorder = RoundImg(ov.transform, "CardBorder",
            V2(.5f,.5f), V2(.5f,.5f), V2(0,0), V2(760,920), CARD_BORDER);
        cardBorder.raycastTarget = false;
        var card = RoundImg(ov.transform, "Card",
            V2(.5f,.5f), V2(.5f,.5f), V2(0,0), V2(750,910), CARD_COLOR);
        card.raycastTarget = false;

        // Result text
        Txt(card.transform, "lbl_result", "WELL DONE!",
            V2(.5f,1), V2(.5f,1), V2(0,-80), V2(640,72), 44, TEXT_PRIMARY,
            TextAnchor.MiddleCenter, FontStyle.Bold);

        // Divider with glow
        RoundImg(card.transform, "DivGlow", V2(.5f,1), V2(.5f,1), V2(0,-155),
            V2(580,6), Hex("#F59E0B15")).raycastTarget = false;
        RoundImg(card.transform, "Div", V2(.5f,1), V2(.5f,1), V2(0,-155),
            V2(560,2), Hex("#FFFFFF10")).raycastTarget = false;

        // Score number
        Txt(card.transform, "lbl_score", "0",
            V2(.5f,.5f), V2(.5f,.5f), V2(0,80), V2(500,230), 140, ACCENT_LIGHT,
            TextAnchor.MiddleCenter, FontStyle.Bold);

        // In a row
        Txt(card.transform, "lbl_inARow", "1 IN A ROW!",
            V2(.5f,.5f), V2(.5f,.5f), V2(0,-50), V2(600,60), 30, TEXT_DIM,
            TextAnchor.MiddleCenter, FontStyle.Bold);

        // Best score
        Txt(card.transform, "lbl_bestScore", "PERSONAL BEST: 0",
            V2(.5f,.5f), V2(.5f,.5f), V2(0,-120), V2(600,60), 32, TEXT_MUTED,
            TextAnchor.MiddleCenter, FontStyle.Normal);

        // Correct answer (shown on loss, hidden on win)
        var lblCorrect = Txt(card.transform, "lbl_correctAnswer", "",
            V2(.5f,.5f), V2(.5f,.5f), V2(0,-180), V2(650,50), 28, ACCENT,
            TextAnchor.MiddleCenter, FontStyle.Bold);
        lblCorrect.gameObject.SetActive(false);

        // Continue button (volumetric)
        VolumetricButton(card.transform, "btn_continue", "CONTINUE",
            V2(.5f,0), V2(.5f,0), V2(0,180), V2(560,100), 34,
            BTN_GREEN_FACE, BTN_GREEN_TOP, BTN_GREEN_SHADOW);

        // Back to menu button
        VolumetricButton(card.transform, "btn_backToMenu", "BACK TO MENU",
            V2(.5f,0), V2(.5f,0), V2(0,60), V2(560,90), 28,
            BTN_FACE, BTN_TOP, BTN_SHADOW);

        return ov;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Building blocks
    // ═══════════════════════════════════════════════════════════════════════

    static Number NumberGroup(Transform p, Vector2 pos)
    {
        return NumberGroup(p, "Number", pos);
    }

    static Number NumberGroup(Transform p, string name, Vector2 pos)
    {
        var go = Panel(p, name, V2(.5f,.5f), V2(.5f,.5f), pos, V2(DW*2+28, DH));
        var n  = go.AddComponent<Number>();
        n.FirstDigit  = Digit(go.transform, "DigitA", V2(-(DW/2+14), 0));
        n.SecondDigit = Digit(go.transform, "DigitB", V2(+(DW/2+14), 0));
        return n;
    }

    static Number NumberGroup3(Transform p, string name, Vector2 pos)
    {
        float spacing = DW + 14;
        var go = Panel(p, name, V2(.5f,.5f), V2(.5f,.5f), pos, V2(DW*3+42, DH));
        var n  = go.AddComponent<Number>();
        n.ThirdDigit  = Digit(go.transform, "DigitH", V2(-spacing, 0));
        n.FirstDigit  = Digit(go.transform, "DigitA", V2(0, 0));
        n.SecondDigit = Digit(go.transform, "DigitB", V2(+spacing, 0));
        return n;
    }

    static Digit Digit(Transform p, string name, Vector2 pos)
    {
        var go = Panel(p, name, V2(.5f,.5f), V2(.5f,.5f), pos, V2(DW,DH));
        var d  = go.AddComponent<global::Digit>();
        d.Lines = new Line[7];
        d.Lines[0] = Seg(go.transform, "Top",         V2( 0,  +SYT), V2(SHW,SHH));
        d.Lines[1] = Seg(go.transform, "Middle",      V2( 0,    0 ), V2(SHW,SHH));
        d.Lines[2] = Seg(go.transform, "Bottom",      V2( 0,  -SYT), V2(SHW,SHH));
        d.Lines[3] = Seg(go.transform, "TopLeft",     V2(-SX, +SYV), V2(SVW,SVH));
        d.Lines[4] = Seg(go.transform, "TopRight",    V2(+SX, +SYV), V2(SVW,SVH));
        d.Lines[5] = Seg(go.transform, "BottomLeft",  V2(-SX, -SYV), V2(SVW,SVH));
        d.Lines[6] = Seg(go.transform, "BottomRight", V2(+SX, -SYV), V2(SVW,SVH));
        return d;
    }

    static PlusMinus PlusMinusToggle(Transform p, Vector2 pos)
    {
        var go = Panel(p, "PlusMinus", V2(.5f,.5f), V2(.5f,.5f), pos, V2(120, 120));

        // Subtle circle background
        var bg = CircleImg(go.transform, "BG", V2(.5f,.5f), V2(.5f,.5f), V2(0,0), V2(110,110), Hex("#1E293B40"));
        bg.raycastTarget = false;

        var pm = go.AddComponent<PlusMinus>();
        pm.line1 = Seg(go.transform, "HBar", V2(0,0), V2(70, 14));
        pm.line2 = Seg(go.transform, "VBar", V2(0,0), V2(14, 70));

        return pm;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Helpers
    // ═══════════════════════════════════════════════════════════════════════

    static Line Seg(Transform p, string n, Vector2 pos, Vector2 sz)
    {
        var img = Img(p, n, V2(.5f,.5f), V2(.5f,.5f), pos, sz, SEG_OFF);
        img.raycastTarget = true;
        img.raycastPadding = new Vector4(-20, -20, -20, -20);
        return img.gameObject.AddComponent<Line>();
    }

    static Image Img(Transform p, string n,
        Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 sz, Color c)
    {
        var go = new GameObject(n);
        go.transform.SetParent(p, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = aMin; rt.anchorMax = aMax;
        rt.anchoredPosition = pos; rt.sizeDelta = sz;
        var img = go.AddComponent<Image>();
        img.color = c;
        return img;
    }

    // Border segment with beveled hexagonal ends
    static Image ImgSeg(Transform p, string n, Vector2 pos, Vector2 sz, Color c, bool horiz)
    {
        var img = Img(p, n, V2(.5f,.5f), V2(.5f,.5f), pos, sz, c);
        img.sprite = horiz ? HSeg : VSeg;
        img.type = Image.Type.Simple;
        img.preserveAspect = false;
        img.raycastTarget = false;
        return img;
    }

    static Image RoundImg(Transform p, string n,
        Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 sz, Color c)
    {
        var go = new GameObject(n);
        go.transform.SetParent(p, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = aMin; rt.anchorMax = aMax;
        rt.anchoredPosition = pos; rt.sizeDelta = sz;
        var img = go.AddComponent<Image>();
        img.sprite = RoundRect;
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = 1f;
        img.color = c;
        return img;
    }

    static Image CircleImg(Transform p, string n,
        Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 sz, Color c)
    {
        var go = new GameObject(n);
        go.transform.SetParent(p, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = aMin; rt.anchorMax = aMax;
        rt.anchoredPosition = pos; rt.sizeDelta = sz;
        var img = go.AddComponent<Image>();
        img.sprite = Circle;
        img.color = c;
        return img;
    }

    static GameObject Panel(Transform p, string n,
        Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 sz)
    {
        var go = new GameObject(n);
        go.transform.SetParent(p, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = aMin; rt.anchorMax = aMax;
        rt.anchoredPosition = pos; rt.sizeDelta = sz;
        return go;
    }

    static void Stretch(GameObject go)
    {
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    static Text Txt(Transform p, string n, string content,
        Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 sz,
        int fs, Color c, TextAnchor align, FontStyle style)
    {
        var go = new GameObject(n);
        go.transform.SetParent(p, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = aMin; rt.anchorMax = aMax;
        rt.anchoredPosition = pos; rt.sizeDelta = sz;
        var t = go.AddComponent<Text>();
        t.text = content; t.fontSize = fs; t.color = c;
        t.alignment = align; t.fontStyle = style;
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
              ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
        t.resizeTextForBestFit = false;
        return t;
    }

    // Digital font text — uses DSEG7 Classic Bold
    static Text DigTxt(Transform p, string n, string content,
        Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 sz,
        int fs, Color c, TextAnchor align = TextAnchor.MiddleCenter, FontStyle style = FontStyle.Normal)
    {
        var t = Txt(p, n, content, aMin, aMax, pos, sz, fs, c, align, style);
        if (DigitalFont != null) t.font = DigitalFont;
        return t;
    }

    // ── Back arrow button (circle with chevron) ─────────────────────────
    static Sprite s_backArrow;
    static Sprite BackArrowSprite => s_backArrow != null ? s_backArrow : (s_backArrow = MakeBackArrowSprite(128));

    static GameObject BackArrowButton(Transform parent, string name,
        Vector2 aMin, Vector2 aMax, Vector2 pos, float size, Color color)
    {
        var go = Panel(parent, name, aMin, aMax, pos, V2(size, size));
        var ct = go.transform;

        var face = new GameObject("btn_face");
        face.transform.SetParent(ct, false);
        var faceRt = face.AddComponent<RectTransform>();
        faceRt.anchorMin = V2(0,0); faceRt.anchorMax = V2(1,1);
        faceRt.offsetMin = faceRt.offsetMax = Vector2.zero;
        var faceImg = face.AddComponent<Image>();
        faceImg.sprite = BackArrowSprite;
        faceImg.color = color;
        faceImg.raycastTarget = true;
        var btn = face.AddComponent<Button>();
        btn.targetGraphic = faceImg;
        var bc = ColorBlock.defaultColorBlock;
        bc.normalColor = Color.white;
        bc.highlightedColor = new Color(1.3f, 1.3f, 1.3f);
        bc.pressedColor = new Color(0.6f, 0.6f, 0.6f);
        btn.colors = bc;

        return go;
    }

    static Sprite MakeBackArrowSprite(int s)
    {
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        var clear = new Color(0, 0, 0, 0);

        float cx = s * 0.5f, cy = s * 0.5f;
        float outerR = s * 0.48f;
        float innerR = s * 0.40f;

        float chevX = s * 0.44f;
        float chevCX = s * 0.56f;
        float chevHH = s * 0.18f;
        float chevT = s * 0.08f;

        for (int y = 0; y < s; y++)
        for (int x = 0; x < s; x++)
        {
            float dx = x - cx + 0.5f;
            float dy = y - cy + 0.5f;
            float dist = Mathf.Sqrt(dx * dx + dy * dy);

            float ringAlpha = 0f;
            if (dist <= outerR && dist >= innerR)
                ringAlpha = Mathf.Clamp01(Mathf.Min(outerR - dist, dist - innerR) + 1f);
            else if (dist < innerR && dist > innerR - 1f)
                ringAlpha = Mathf.Clamp01(dist - innerR + 1f);
            else if (dist > outerR && dist < outerR + 1f)
                ringAlpha = Mathf.Clamp01(outerR - dist + 1f);

            float px = x + 0.5f, py = y + 0.5f;
            float chevAlpha = 0f;

            for (int arm = -1; arm <= 1; arm += 2)
            {
                float ax = chevX, ay = cy;
                float bx = chevCX, by = cy + arm * chevHH;
                float abx = bx - ax, aby = by - ay;
                float abLen = Mathf.Sqrt(abx * abx + aby * aby);
                float t = ((px - ax) * abx + (py - ay) * aby) / (abLen * abLen);
                t = Mathf.Clamp01(t);
                float closestX = ax + t * abx;
                float closestY = ay + t * aby;
                float dd = Mathf.Sqrt((px - closestX) * (px - closestX) + (py - closestY) * (py - closestY));
                float aa = Mathf.Clamp01(chevT * 0.5f - dd + 1f);
                if (aa > chevAlpha) chevAlpha = aa;
            }

            float alpha = Mathf.Max(ringAlpha, chevAlpha);
            tex.SetPixel(x, y, alpha > 0 ? new Color(1, 1, 1, alpha) : clear);
        }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Procedural Sprite Generation
    // ═══════════════════════════════════════════════════════════════════════

    static Sprite MakeRoundRect(int w, int h, int radius)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            float dx = 0f, dy = 0f;

            // Find distance to nearest corner arc
            if (x < radius && y < radius) { dx = radius - x - 0.5f; dy = radius - y - 0.5f; }
            else if (x >= w - radius && y < radius) { dx = x - (w - radius) + 0.5f; dy = radius - y - 0.5f; }
            else if (x < radius && y >= h - radius) { dx = radius - x - 0.5f; dy = y - (h - radius) + 0.5f; }
            else if (x >= w - radius && y >= h - radius) { dx = x - (w - radius) + 0.5f; dy = y - (h - radius) + 0.5f; }

            float dist = Mathf.Sqrt(dx * dx + dy * dy);
            float alpha = Mathf.Clamp01(radius - dist + 1f);

            tex.SetPixel(x, y, new Color(1, 1, 1, alpha));
        }

        tex.Apply();

        // Border for 9-slice: radius on each side
        var border = new Vector4(radius, radius, radius, radius);
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect, border);
    }

    // ── Beveled hexagonal segment sprite ────────────────────────────────
    // Creates a horizontal or vertical segment with pointed/beveled ends
    // like real LCD/VFD 7-segment displays
    static Sprite MakeBeveledSeg(int w, int h, bool horizontal)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        var clear = new Color(1, 1, 1, 0);

        if (horizontal)
        {
            // Horizontal segment: pointed left and right ends, flat top/bottom
            // Shape: hexagon like  < ======== >
            float hh = h * 0.5f;
            float bevel = hh; // bevel length = half height (makes 45° points)

            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float fy = y - hh + 0.5f; // -hh..+hh centered
                float absY = Mathf.Abs(fy);
                float leftEdge = bevel * (absY / hh);    // left bevel boundary
                float rightEdge = w - bevel * (absY / hh); // right bevel boundary

                if (x >= leftEdge - 0.5f && x <= rightEdge + 0.5f)
                {
                    // Anti-alias edges
                    float alphaL = Mathf.Clamp01(x - leftEdge + 1f);
                    float alphaR = Mathf.Clamp01(rightEdge - x + 1f);
                    float alpha = Mathf.Min(alphaL, alphaR);
                    tex.SetPixel(x, y, new Color(1, 1, 1, alpha));
                }
                else
                    tex.SetPixel(x, y, clear);
            }
        }
        else
        {
            // Vertical segment: pointed top and bottom, flat left/right
            // Shape: hexagon rotated 90°
            float hw = w * 0.5f;
            float bevel = hw; // bevel length = half width

            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float fx = x - hw + 0.5f;
                float absX = Mathf.Abs(fx);
                float botEdge = bevel * (absX / hw);
                float topEdge = h - bevel * (absX / hw);

                if (y >= botEdge - 0.5f && y <= topEdge + 0.5f)
                {
                    float alphaB = Mathf.Clamp01(y - botEdge + 1f);
                    float alphaT = Mathf.Clamp01(topEdge - y + 1f);
                    float alpha = Mathf.Min(alphaB, alphaT);
                    tex.SetPixel(x, y, new Color(1, 1, 1, alpha));
                }
                else
                    tex.SetPixel(x, y, clear);
            }
        }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
    }

    static Sprite MakeCircle(int r)
    {
        var tex = new Texture2D(r, r, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        float c = r * 0.5f, rad = c - 0.5f;
        for (int y = 0; y < r; y++)
            for (int x = 0; x < r; x++)
            {
                float dx = x - c + 0.5f, dy = y - c + 0.5f;
                tex.SetPixel(x, y, new Color(1,1,1, Mathf.Clamp01(rad - Mathf.Sqrt(dx*dx+dy*dy) + 1f)));
            }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, r, r), new Vector2(0.5f, 0.5f));
    }


    // ── Login Panel ────────────────────────────────────────────────────────
    static void LoginPanel(Transform parent, GUIManager gui)
    {
        var pnlLogin = Panel(parent, "pnl_login", V2(0,0), V2(1,1), V2(0,0), V2(0,0));
        var lt = pnlLogin.transform;
        gui.pnl_login = pnlLogin;

        // Dark background
        RoundImg(lt, "LoginBG", V2(0,0), V2(1,1), V2(0,0), V2(0,0), Hex("#0A0E1AFA")).raycastTarget = false;

        // Title
        DigTxt(lt, "lbl_title", "MATHSTICK PUZZLE", V2(.5f,1), V2(.5f,1), V2(0,-300), V2(900,105), 70, ACCENT).raycastTarget = false;
        RoundImg(lt, "LoginAccent", V2(.5f,1), V2(.5f,1), V2(0,-415), V2(120,3), ACCENT).raycastTarget = false;

        // Email input — big, centered
        var emailGO = new GameObject("inp_email");
        emailGO.transform.SetParent(lt, false);
        var emailRt = emailGO.AddComponent<RectTransform>();
        emailRt.anchorMin = V2(.5f,.5f); emailRt.anchorMax = V2(.5f,.5f);
        emailRt.anchoredPosition = V2(0, 120);
        emailRt.sizeDelta = V2(700, 90);
        var emailImg = emailGO.AddComponent<Image>();
        emailImg.color = Hex("#151A28");
        var emailInp = emailGO.AddComponent<InputField>();

        var emailText = Txt(emailGO.transform, "Text", "", V2(0,0), V2(1,1), V2(0,0), V2(0,0), 28, TEXT_PRIMARY,
            TextAnchor.MiddleLeft, FontStyle.Normal);
        var emailTextRt = emailText.GetComponent<RectTransform>();
        emailTextRt.anchorMin = V2(0,0); emailTextRt.anchorMax = V2(1,1);
        emailTextRt.offsetMin = V2(20,0); emailTextRt.offsetMax = V2(-20,0);

        var emailPh = Txt(emailGO.transform, "Placeholder", "EMAIL", V2(0,0), V2(1,1), V2(0,0), V2(0,0), 26, TEXT_MUTED,
            TextAnchor.MiddleLeft, FontStyle.Normal);
        var emailPhRt = emailPh.GetComponent<RectTransform>();
        emailPhRt.anchorMin = V2(0,0); emailPhRt.anchorMax = V2(1,1);
        emailPhRt.offsetMin = V2(20,0); emailPhRt.offsetMax = V2(-20,0);

        emailInp.textComponent = emailText;
        emailInp.placeholder = emailPh;
        gui.inp_email = emailInp;

        // Password input — big, centered
        var passGO = new GameObject("inp_password");
        passGO.transform.SetParent(lt, false);
        var passRt = passGO.AddComponent<RectTransform>();
        passRt.anchorMin = V2(.5f,.5f); passRt.anchorMax = V2(.5f,.5f);
        passRt.anchoredPosition = V2(0, 10);
        passRt.sizeDelta = V2(700, 90);
        var passImg = passGO.AddComponent<Image>();
        passImg.color = Hex("#151A28");
        var passInp = passGO.AddComponent<InputField>();
        passInp.contentType = InputField.ContentType.Password;

        var passText = Txt(passGO.transform, "Text", "", V2(0,0), V2(1,1), V2(0,0), V2(0,0), 28, TEXT_PRIMARY,
            TextAnchor.MiddleLeft, FontStyle.Normal);
        var passTextRt = passText.GetComponent<RectTransform>();
        passTextRt.anchorMin = V2(0,0); passTextRt.anchorMax = V2(1,1);
        passTextRt.offsetMin = V2(20,0); passTextRt.offsetMax = V2(-20,0);

        var passPh = Txt(passGO.transform, "Placeholder", "PASSWORD", V2(0,0), V2(1,1), V2(0,0), V2(0,0), 26, TEXT_MUTED,
            TextAnchor.MiddleLeft, FontStyle.Normal);
        var passPhRt = passPh.GetComponent<RectTransform>();
        passPhRt.anchorMin = V2(0,0); passPhRt.anchorMax = V2(1,1);
        passPhRt.offsetMin = V2(20,0); passPhRt.offsetMax = V2(-20,0);

        passInp.textComponent = passText;
        passInp.placeholder = passPh;
        gui.inp_password = passInp;

        // Error label — between password and login
        gui.lbl_login_error = Txt(lt, "lbl_error", "", V2(.5f,.5f), V2(.5f,.5f), V2(0,-60), V2(700, 40), 20, Hex("#FF6666"),
            TextAnchor.MiddleCenter, FontStyle.Normal);
        gui.lbl_login_error.raycastTarget = false;

        // LOGIN button — below inputs
        var btnLogin = SegButton(lt, "btn_login", "LOGIN", V2(.5f,.5f), V2(.5f,.5f), V2(0,-130),
            V2(500, 100), 38, ACCENT);
        UnityEventTools.AddPersistentListener(
            btnLogin.transform.Find("btn_face").GetComponent<Button>().onClick,
            gui.OnLoginPressed);

        // Forgot password link
        var btnForgot = new GameObject("btn_forgot");
        btnForgot.transform.SetParent(lt, false);
        var btnForgotRt = btnForgot.AddComponent<RectTransform>();
        btnForgotRt.anchorMin = V2(.5f,.5f); btnForgotRt.anchorMax = V2(.5f,.5f);
        btnForgotRt.anchoredPosition = V2(0, -210);
        btnForgotRt.sizeDelta = V2(400, 50);
        var btnForgotImg = btnForgot.AddComponent<Image>();
        btnForgotImg.color = Color.clear;
        var btnForgotBtn = btnForgot.AddComponent<Button>();
        btnForgotBtn.targetGraphic = btnForgotImg;
        DigTxt(btnForgot.transform, "Text", "FORGOT PASSWORD?", V2(0,0), V2(1,1), V2(0,0), V2(0,0), 20, ACCENT_DARK);
        UnityEventTools.AddPersistentListener(btnForgotBtn.onClick, gui.OnShowForgotPasswordPanel);

        // SIGN UP link — bottom
        var btnReg = new GameObject("btn_register");
        btnReg.transform.SetParent(lt, false);
        var btnRegRt = btnReg.AddComponent<RectTransform>();
        btnRegRt.anchorMin = V2(.5f,0); btnRegRt.anchorMax = V2(.5f,0);
        btnRegRt.anchoredPosition = V2(0, 130);
        btnRegRt.sizeDelta = V2(700, 70);
        var btnRegImg = btnReg.AddComponent<Image>();
        btnRegImg.color = Color.clear;
        var btnRegBtn = btnReg.AddComponent<Button>();
        btnRegBtn.targetGraphic = btnRegImg;
        DigTxt(btnReg.transform, "Text", "NO ACCOUNT? SIGN UP", V2(0,0), V2(1,1), V2(0,0), V2(0,0), 28, ACCENT);
        UnityEventTools.AddPersistentListener(btnRegBtn.onClick, gui.OnShowRegisterPressed);
    }

    // ── Forgot Password Panel ──────────────────────────────────────────────
    static void ForgotPasswordPanel(Transform parent, GUIManager gui)
    {
        var pnlForgot = Panel(parent, "pnl_forgotPassword", V2(0,0), V2(1,1), V2(0,0), V2(0,0));
        var ft = pnlForgot.transform;
        gui.pnl_forgotPassword = pnlForgot;

        RoundImg(ft, "ForgotBG", V2(0,0), V2(1,1), V2(0,0), V2(0,0), Hex("#0A0E1AFA")).raycastTarget = false;

        DigTxt(ft, "lbl_forgot_title", "RESET PASSWORD", V2(.5f,1), V2(.5f,1), V2(0,-120), V2(900,57), 38, ACCENT).raycastTarget = false;
        RoundImg(ft, "ForgotAccent", V2(.5f,1), V2(.5f,1), V2(0,-165), V2(120,3), ACCENT).raycastTarget = false;

        DigTxt(ft, "lbl_forgot_desc1", "ENTER YOUR EMAIL", V2(.5f,.5f), V2(.5f,.5f), V2(0,175), V2(900,24), 16, TEXT_MUTED).raycastTarget = false;
        DigTxt(ft, "lbl_forgot_desc2", "TO RESET PASSWORD", V2(.5f,.5f), V2(.5f,.5f), V2(0,145), V2(900,24), 16, TEXT_MUTED).raycastTarget = false;

        // Email input
        var fEmailGO = new GameObject("inp_forgot_email");
        fEmailGO.transform.SetParent(ft, false);
        var fEmailRt = fEmailGO.AddComponent<RectTransform>();
        fEmailRt.anchorMin = V2(.5f,.5f); fEmailRt.anchorMax = V2(.5f,.5f);
        fEmailRt.anchoredPosition = V2(0, 60);
        fEmailRt.sizeDelta = V2(700, 90);
        var fEmailBg = fEmailGO.AddComponent<Image>();
        fEmailBg.color = Hex("#151A28");
        var fEmailInp = fEmailGO.AddComponent<InputField>();

        var fEmailText = Txt(fEmailGO.transform, "Text", "", V2(0,0), V2(1,1), V2(0,0), V2(0,0), 28, TEXT_PRIMARY,
            TextAnchor.MiddleLeft, FontStyle.Normal);
        var fEmailTextRt = fEmailText.GetComponent<RectTransform>();
        fEmailTextRt.anchorMin = V2(0,0); fEmailTextRt.anchorMax = V2(1,1);
        fEmailTextRt.offsetMin = V2(20,0); fEmailTextRt.offsetMax = V2(-20,0);

        var fEmailPh = Txt(fEmailGO.transform, "Placeholder", "EMAIL", V2(0,0), V2(1,1), V2(0,0), V2(0,0), 26, TEXT_MUTED,
            TextAnchor.MiddleLeft, FontStyle.Normal);
        var fEmailPhRt = fEmailPh.GetComponent<RectTransform>();
        fEmailPhRt.anchorMin = V2(0,0); fEmailPhRt.anchorMax = V2(1,1);
        fEmailPhRt.offsetMin = V2(20,0); fEmailPhRt.offsetMax = V2(-20,0);

        fEmailInp.textComponent = fEmailText;
        fEmailInp.placeholder = fEmailPh;
        gui.inp_forgot_email = fEmailInp;

        // Status/error label
        gui.lbl_forgot_status = Txt(ft, "lbl_forgot_status", "", V2(.5f,.5f), V2(.5f,.5f), V2(0,-10), V2(700, 40), 20, ACCENT,
            TextAnchor.MiddleCenter, FontStyle.Normal);
        gui.lbl_forgot_status.raycastTarget = false;

        // SEND RESET EMAIL button
        var btnSend = SegButton(ft, "btn_send_reset", "RESET PASSWORD", V2(.5f,.5f), V2(.5f,.5f), V2(0,-80),
            V2(600, 100), 30, ACCENT);
        UnityEventTools.AddPersistentListener(
            btnSend.transform.Find("btn_face").GetComponent<Button>().onClick,
            gui.OnSendResetEmailPressed);

        // BACK TO LOGIN
        var btnBack = BackArrowButton(ft, "btn_forgot_back", V2(0,0), V2(0,0), V2(70, 70), 80, ACCENT_DARK);
        UnityEventTools.AddPersistentListener(
            btnBack.transform.Find("btn_face").GetComponent<Button>().onClick,
            gui.OnBackToLoginPressed);

        pnlForgot.SetActive(false);
    }

    // ── Register Panel ─────────────────────────────────────────────────────
    static void RegisterPanel(Transform parent, GUIManager gui)
    {
        var pnlReg = Panel(parent, "pnl_register", V2(0,0), V2(1,1), V2(0,0), V2(0,0));
        var rt = pnlReg.transform;
        gui.pnl_register = pnlReg;

        // Full-screen dark background
        RoundImg(rt, "RegBG", V2(0,0), V2(1,1), V2(0,0), V2(0,0), Hex("#0A0E1AFA")).raycastTarget = false;

        // Title
        DigTxt(rt, "lbl_title", "CREATE ACCOUNT", V2(.5f,1), V2(.5f,1), V2(0,-270), V2(900,57), 38, ACCENT).raycastTarget = false;
        RoundImg(rt, "RegAccent", V2(.5f,1), V2(.5f,1), V2(0,-315), V2(120,3), ACCENT).raycastTarget = false;

        // Inputs centered on page — 4 fields starting from y=120 down
        float startY = 120f;
        float spacing = 110f;

        // Username
        var nameGO = new GameObject("inp_name");
        nameGO.transform.SetParent(rt, false);
        var nameRt = nameGO.AddComponent<RectTransform>();
        nameRt.anchorMin = V2(.5f,.5f); nameRt.anchorMax = V2(.5f,.5f);
        nameRt.anchoredPosition = V2(0, startY);
        nameRt.sizeDelta = V2(700, 90);
        nameGO.AddComponent<Image>().color = Hex("#151A28");
        var nameInp = nameGO.AddComponent<InputField>();
        var nameTxt = Txt(nameGO.transform, "Text", "", V2(0,0), V2(1,1), V2(0,0), V2(0,0), 28, TEXT_PRIMARY,
            TextAnchor.MiddleLeft, FontStyle.Normal);
        nameTxt.GetComponent<RectTransform>().offsetMin = V2(20,0);
        nameTxt.GetComponent<RectTransform>().offsetMax = V2(-20,0);
        var namePh = Txt(nameGO.transform, "Placeholder", "USERNAME", V2(0,0), V2(1,1), V2(0,0), V2(0,0), 26, TEXT_MUTED,
            TextAnchor.MiddleLeft, FontStyle.Normal);
        namePh.GetComponent<RectTransform>().offsetMin = V2(20,0);
        namePh.GetComponent<RectTransform>().offsetMax = V2(-20,0);
        nameInp.textComponent = nameTxt;
        nameInp.placeholder = namePh;
        gui.inp_reg_name = nameInp;

        // Email
        var emailGO = new GameObject("inp_email");
        emailGO.transform.SetParent(rt, false);
        var emailRt = emailGO.AddComponent<RectTransform>();
        emailRt.anchorMin = V2(.5f,.5f); emailRt.anchorMax = V2(.5f,.5f);
        emailRt.anchoredPosition = V2(0, startY - spacing);
        emailRt.sizeDelta = V2(700, 90);
        emailGO.AddComponent<Image>().color = Hex("#151A28");
        var emailInp = emailGO.AddComponent<InputField>();
        var emailTxt = Txt(emailGO.transform, "Text", "", V2(0,0), V2(1,1), V2(0,0), V2(0,0), 28, TEXT_PRIMARY,
            TextAnchor.MiddleLeft, FontStyle.Normal);
        emailTxt.GetComponent<RectTransform>().offsetMin = V2(20,0);
        emailTxt.GetComponent<RectTransform>().offsetMax = V2(-20,0);
        var emailPh = Txt(emailGO.transform, "Placeholder", "EMAIL", V2(0,0), V2(1,1), V2(0,0), V2(0,0), 26, TEXT_MUTED,
            TextAnchor.MiddleLeft, FontStyle.Normal);
        emailPh.GetComponent<RectTransform>().offsetMin = V2(20,0);
        emailPh.GetComponent<RectTransform>().offsetMax = V2(-20,0);
        emailInp.textComponent = emailTxt;
        emailInp.placeholder = emailPh;
        gui.inp_reg_email = emailInp;

        // Password
        var passGO = new GameObject("inp_password");
        passGO.transform.SetParent(rt, false);
        var passRt = passGO.AddComponent<RectTransform>();
        passRt.anchorMin = V2(.5f,.5f); passRt.anchorMax = V2(.5f,.5f);
        passRt.anchoredPosition = V2(0, startY - spacing * 2);
        passRt.sizeDelta = V2(700, 90);
        passGO.AddComponent<Image>().color = Hex("#151A28");
        var passInp = passGO.AddComponent<InputField>();
        passInp.contentType = InputField.ContentType.Password;
        var passTxt = Txt(passGO.transform, "Text", "", V2(0,0), V2(1,1), V2(0,0), V2(0,0), 28, TEXT_PRIMARY,
            TextAnchor.MiddleLeft, FontStyle.Normal);
        passTxt.GetComponent<RectTransform>().offsetMin = V2(20,0);
        passTxt.GetComponent<RectTransform>().offsetMax = V2(-20,0);
        var passPh = Txt(passGO.transform, "Placeholder", "PASSWORD", V2(0,0), V2(1,1), V2(0,0), V2(0,0), 26, TEXT_MUTED,
            TextAnchor.MiddleLeft, FontStyle.Normal);
        passPh.GetComponent<RectTransform>().offsetMin = V2(20,0);
        passPh.GetComponent<RectTransform>().offsetMax = V2(-20,0);
        passInp.textComponent = passTxt;
        passInp.placeholder = passPh;
        gui.inp_reg_pass = passInp;

        // Confirm password
        var confGO = new GameObject("inp_confirm_password");
        confGO.transform.SetParent(rt, false);
        var confRt = confGO.AddComponent<RectTransform>();
        confRt.anchorMin = V2(.5f,.5f); confRt.anchorMax = V2(.5f,.5f);
        confRt.anchoredPosition = V2(0, startY - spacing * 3);
        confRt.sizeDelta = V2(700, 90);
        confGO.AddComponent<Image>().color = Hex("#151A28");
        var confInp = confGO.AddComponent<InputField>();
        confInp.contentType = InputField.ContentType.Password;
        var confTxt = Txt(confGO.transform, "Text", "", V2(0,0), V2(1,1), V2(0,0), V2(0,0), 28, TEXT_PRIMARY,
            TextAnchor.MiddleLeft, FontStyle.Normal);
        confTxt.GetComponent<RectTransform>().offsetMin = V2(20,0);
        confTxt.GetComponent<RectTransform>().offsetMax = V2(-20,0);
        var confPh = Txt(confGO.transform, "Placeholder", "CONFIRM PASSWORD", V2(0,0), V2(1,1), V2(0,0), V2(0,0), 26, TEXT_MUTED,
            TextAnchor.MiddleLeft, FontStyle.Normal);
        confPh.GetComponent<RectTransform>().offsetMin = V2(20,0);
        confPh.GetComponent<RectTransform>().offsetMax = V2(-20,0);
        confInp.textComponent = confTxt;
        confInp.placeholder = confPh;
        gui.inp_reg_confirm_pass = confInp;

        // Error label — below inputs
        var errorLbl = Txt(rt, "lbl_error", "", V2(.5f,.5f), V2(.5f,.5f), V2(0, startY - spacing * 4 + 20), V2(700, 40), 20, Hex("#FF6666"),
            TextAnchor.MiddleCenter, FontStyle.Normal);
        errorLbl.raycastTarget = false;
        gui.lbl_login_error = errorLbl;

        // SIGN UP button
        var btnReg = SegButton(rt, "btn_register", "SIGN UP", V2(.5f,.5f), V2(.5f,.5f),
            V2(0, startY - spacing * 4 - 40), V2(600, 100), 32, ACCENT);
        UnityEventTools.AddPersistentListener(
            btnReg.transform.Find("btn_face").GetComponent<Button>().onClick,
            gui.OnRegisterPressed);

        // Back arrow — bottom left
        var btnBack = BackArrowButton(rt, "btn_back", V2(0,0), V2(0,0), V2(70, 70), 80, ACCENT_DARK);
        UnityEventTools.AddPersistentListener(
            btnBack.transform.Find("btn_face").GetComponent<Button>().onClick,
            gui.OnBackToLoginPressed);
    }

    // ── Speaker icon sprite generator ───────────────────────────────────
    static Sprite MakeSpeakerSprite(bool muted)
    {
        int s = 128;
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
        var clear = new Color(0, 0, 0, 0);
        var white = Color.white;
        var dark = new Color(0.12f, 0.14f, 0.18f);
        var amber = new Color(0.96f, 0.62f, 0.04f);
        var red = new Color(0.94f, 0.27f, 0.27f);

        // Fill with circle background
        int cx = s / 2, cy = s / 2, r = s / 2 - 2;
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float dist = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                if (dist <= r)
                    tex.SetPixel(x, y, dark);
                else if (dist <= r + 1.5f)
                    tex.SetPixel(x, y, new Color(dark.r, dark.g, dark.b, 0.5f));
                else
                    tex.SetPixel(x, y, clear);
            }

        // Draw speaker body (rectangle)
        for (int y = cy - 8; y <= cy + 8; y++)
            for (int x = 28; x <= 42; x++)
                tex.SetPixel(x, y, amber);

        // Draw speaker cone (triangle)
        for (int x = 42; x <= 60; x++)
        {
            float t = (x - 42f) / 18f;
            int half = (int)(8 + t * 14);
            for (int y = cy - half; y <= cy + half; y++)
                if (y >= 0 && y < s)
                    tex.SetPixel(x, y, amber);
        }

        if (!muted)
        {
            // Draw sound waves (3 arcs)
            for (int wave = 0; wave < 3; wave++)
            {
                int wr = 18 + wave * 12;
                for (int a = -45; a <= 45; a++)
                {
                    float rad = a * Mathf.Deg2Rad;
                    for (int t = -1; t <= 1; t++)
                    {
                        int px = (int)(60 + (wr + t) * Mathf.Cos(rad));
                        int py = (int)(cy + (wr + t) * Mathf.Sin(rad));
                        if (px >= 0 && px < s && py >= 0 && py < s)
                            tex.SetPixel(px, py, amber);
                    }
                }
            }
        }
        else
        {
            // Draw diagonal slash line for mute
            for (int i = 20; i < s - 20; i++)
            {
                int x = i;
                int y = s - i;
                for (int dx = -2; dx <= 2; dx++)
                    for (int dy = -2; dy <= 2; dy++)
                    {
                        int px = x + dx, py = y + dy;
                        if (px >= 0 && px < s && py >= 0 && py < s)
                            tex.SetPixel(px, py, red);
                    }
            }
        }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f));
    }

    static Vector2 V2(float x, float y) => new Vector2(x, y);
    static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out Color c); return c; }

    static string ColorToHex(Color c)
    {
        return "#" + ColorUtility.ToHtmlStringRGBA(c);
    }
}
#endif
