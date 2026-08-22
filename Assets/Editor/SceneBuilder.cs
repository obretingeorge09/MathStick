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

    // ── Segment dimensions (digital-clock proportions) ─────────────────
    const float DW  = 120f;   // digit cell width (tight)
    const float DH  = 280f;
    const float SHW = 100f;   // horizontal seg width  (long bar)
    const float SHH = 18f;    // horizontal seg height (thicker)
    const float SVW = 18f;    // vertical seg width    (thicker)
    const float SVH = 105f;   // vertical seg height   (long bar)
    const float SX  = 50f;    // vertical seg X offset from center
    const float SYT = 122f;   // top/bottom seg Y offset from center
    const float SYV = 62f;    // vertical seg Y offset from center

    const float TIMER_MAX = 90f;

    // ── Rounded rect sprite cache ────────────────────────────────────────
    static Sprite s_roundRect;
    static Sprite RoundRect => s_roundRect != null ? s_roundRect : (s_roundRect = MakeRoundRect(128, 128, 24));
    static Sprite s_roundRectLarge;
    static Sprite RoundRectLarge => s_roundRectLarge != null ? s_roundRectLarge : (s_roundRectLarge = MakeRoundRect(128, 128, 32));
    static Sprite s_roundRectTight;
    static Sprite RoundRectTight => s_roundRectTight != null ? s_roundRectTight : (s_roundRectTight = MakeRoundRect(64, 64, 11));
    static Sprite s_roundRectBtn;
    static Sprite RoundRectBtn => s_roundRectBtn != null ? s_roundRectBtn : (s_roundRectBtn = MakeRoundRect(96, 96, 22));
    static Sprite s_pill;
    static Sprite Pill => s_pill != null ? s_pill : (s_pill = MakeRoundRect(128, 64, 32));
    static Sprite s_spkCone;
    static Sprite SpeakerCone => s_spkCone != null ? s_spkCone : (s_spkCone = MakeSpeakerCone(96));
    static Sprite s_spkSlash;
    static Sprite SpeakerSlash => s_spkSlash != null ? s_spkSlash : (s_spkSlash = MakeSlash(96));

    static Sprite s_softDot;
    static Sprite SoftDot => s_softDot != null ? s_softDot : (s_softDot = MakeSoftDot(128));
    static Sprite s_ring;
    static Sprite Ring => s_ring != null ? s_ring : (s_ring = MakeRing(128, 0.12f));

    static Sprite s_vGrade;
    static Sprite VerticalGrade => s_vGrade != null ? s_vGrade : (s_vGrade = MakeVerticalGrade(4, 256));
    static Sprite s_vignette;
    static Sprite Vignette => s_vignette != null ? s_vignette : (s_vignette = MakeVignette(256));

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

    // Survives the domain reload that exiting Play mode triggers, which plain
    // statics and playModeStateChanged subscriptions do not. Cleared when the
    // editor closes.
    const string PENDING_BUILD = "PlusMinus.BuildAfterPlayModeStops";

    [MenuItem("PlusMinus/Build Scene")]
    public static void Build()
    {
        if (EditorApplication.isPlaying)
        {
            // This used to just refuse and say "Stop Play mode first!", which
            // is a message you read twice: once to learn the rule, once
            // because you forgot it. Rebuilding really cannot happen under
            // Play mode — it would delete the objects the running game is
            // holding, and Unity restores the pre-play scene on exit, so the
            // work would be thrown away either way. So stop Play mode here and
            // build once it has, instead of handing the job back.
            Debug.Log("PlusMinus: leaving Play mode, then building the scene.");
            SessionState.SetBool(PENDING_BUILD, true);
            EditorApplication.isPlaying = false;
            return;
        }

        BuildNow();
    }

    /// <summary>Runs after the domain reload that follows leaving Play mode.</summary>
    [InitializeOnLoadMethod]
    static void ResumePendingBuild()
    {
        if (!SessionState.GetBool(PENDING_BUILD, false)) return;

        // This also fires on the way INTO Play mode; only the way out counts.
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;

        SessionState.SetBool(PENDING_BUILD, false);

        // Deferred: Unity is still finishing the teardown at this point, and
        // NewScene during it leaves the hierarchy half torn down.
        EditorApplication.delayCall += BuildNow;
    }

    static void BuildNow()
    {
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
        canvasGO.AddComponent<CanvasOrientationAdapter>();
        var ct = canvasGO.transform;

        // ── Backdrop: flat base, vertical grade, then a vignette so the
        //    screen edges fall away and the content reads as lit. ─────────
        Img(ct, "BG", V2(0,0), V2(1,1), V2(0,0), V2(0,0), BG).raycastTarget = false;

        var bgGrad = Img(ct, "BG_Grade", V2(0,0), V2(1,1), V2(0,0), V2(0,0), Color.white);
        bgGrad.sprite = VerticalGrade;
        bgGrad.type = Image.Type.Simple;
        bgGrad.raycastTarget = false;

        var bgVig = Img(ct, "BG_Vignette", V2(0,0), V2(1,1), V2(0,0), V2(0,0), Color.white);
        bgVig.sprite = Vignette;
        bgVig.type = Image.Type.Simple;
        bgVig.raycastTarget = false;

        // Placeholder for login/register panels (created after gui is initialized)

        // ══════════════════════════════════════════════════════════════════
        //  pnl_start — Main Menu (segment-glow minimalist)
        // ══════════════════════════════════════════════════════════════════
        var pnlStart = Panel(ct, "pnl_start", V2(0,0), V2(1,1), V2(0,0), V2(0,0));
        pnlStart.AddComponent<ScrollablePanel>().referenceHeight = 1920f;
        var st = pnlStart.transform;

        // Dark background
        Img(st, "StartBG", V2(0,0), V2(1,1), V2(0,0), V2(0,0), Hex("#080C16")).raycastTarget = false;

        // ── Title — digital segment letters ─────────────────────────────
        // Nine segment glyphs at 140px overrun a 900px box, and Text wraps by
        // default with vertical overflow truncated — so the K went to a second
        // line and was clipped away, leaving MATHSTIC. Best-fit shrinks the
        // word to whatever the box holds instead of losing a letter, and both
        // copies of each word must carry identical settings or the glow slides
        // off the letters it is meant to sit behind.
        Fit(DigTxt(st, "lbl_title_glow", "MATHSTICK", V2(.5f,1), V2(.5f,1), V2(2,-398), V2(938,210), 140, Hex("#F59E0B10")), 140, 70);
        Fit(DigTxt(st, "lbl_title_main", "MATHSTICK", V2(.5f,1), V2(.5f,1), V2(0,-396), V2(938,210), 140, ACCENT), 140, 70);
        Fit(DigTxt(st, "lbl_title2_glow", "PUZZLE", V2(.5f,1), V2(.5f,1), V2(2,-558), V2(938,165), 110, Hex("#F59E0B10")), 110, 55);
        Fit(DigTxt(st, "lbl_title2_main", "PUZZLE", V2(.5f,1), V2(.5f,1), V2(0,-556), V2(938,165), 110, ACCENT), 110, 55);

        // Decorative segment-bar under title
        Img(st, "TitleSeg", V2(.5f,1), V2(.5f,1), V2(0,-630), V2(160, 4), Hex("#F59E0B30")).raycastTarget = false;

        // ── Digit legend — the one thing a new player cannot guess ───────
        // Showing all ten digits with their unlit segments still drawn is the
        // explanation; it needs no caption, and the one that used to sit here
        // said every digit is seven sticks, which only 8 is.

        // 10 cells at a 76px pitch spans 754px, inside the 898px of usable
        // width the 978px canvas leaves after its 40px gutters.
        for (int d = 0; d < 10; d++)
            LegendDigit(st, d, V2(-342f + d * 76f, -790f), 0.58f);

        // ── Menu stack — anchored to the top so it keeps a fixed distance
        //    from the logo instead of drifting on taller screens ────────
        SegButton(st, "btn_play", "TRAINING", V2(.5f,1), V2(.5f,1), V2(0, -1010),
            V2(440, 96), 34, ACCENT);

        SegButton(st, "btn_arcade", "ARCADE", V2(.5f,1), V2(.5f,1), V2(0, -1126),
            V2(440, 96), 34, ACCENT);
        DigTxt(st, "lbl_arcade_tag", "1V1 ONLINE", V2(.5f,1), V2(.5f,1), V2(0,-1186), V2(900,24), 16, TEXT_MUTED).raycastTarget = false;

        // ── Progression row — PROFILE / RANKING / DAILY ──────────────
        SegButton(st, "btn_profile", "PROFILE", V2(.5f,1), V2(.5f,1), V2(-148, -1252),
            V2(140, 78), 17, Hex("#4DD0E1"));
        SegButton(st, "btn_leaderboard", "RANKING", V2(.5f,1), V2(.5f,1), V2(0, -1252),
            V2(140, 78), 17, Hex("#FFD600"));
        var btnDaily = SegButton(st, "btn_daily", "DAILY", V2(.5f,1), V2(.5f,1), V2(148, -1252),
            V2(140, 78), 17, Hex("#B388FF"));

        // Unclaimed-reward dot on the DAILY button
        var dailyBadge = Img(btnDaily.transform, "badge_daily", V2(1,1), V2(1,1), V2(-4,-4), V2(20,20), Hex("#FF1744"));
        dailyBadge.sprite = Circle;
        dailyBadge.raycastTarget = false;
        dailyBadge.transform.SetAsLastSibling();
        dailyBadge.gameObject.SetActive(false);

        // ── HOW TO PLAY button ───────────────────────────────────────
        SegButton(st, "btn_tutorial", "HOW TO PLAY", V2(.5f,1), V2(.5f,1), V2(0, -1364),
            V2(440, 84), 26, TEXT_DIM);

        // ── SETTINGS button ─────────────────────────────────────────
        SegButton(st, "btn_settings", "SETTINGS", V2(.5f,1), V2(.5f,1), V2(0, -1464),
            V2(440, 84), 26, TEXT_DIM);

        // ── Top strip: rank on the left, level on the right ──────────
        // Settings also lives at the bottom of the menu stack, but that is the
        // sixth item down and dim — findable if you go looking, not if you are
        // trying to turn the volume down or change the language on your way
        // into a game. This mirrors the volume key on the opposite corner, so
        // the two controls that are not about playing sit together.
        // The volume key is a 120px box pivoted at its top-right corner, hung
        // at (-8,-135) — so its 76px visual centres at y -195, not -135. Match
        // that centre and that size, mirrored, or the two keys sit a clear
        // 60px apart and read as unrelated controls.
        var btnSettingsKey = GearButton(st, "btn_settings_key", V2(0,1), V2(0,1), V2(68, -195), 76, ACCENT_DARK);

        var lblMenuRank = Txt(st, "lbl_menu_rank", "SILVER  1000",
            V2(0,1), V2(0,1), V2(230,-55), V2(400,40), 22, Hex("#C0C0C0"),
            TextAnchor.MiddleLeft, FontStyle.Bold);

        var lblMenuLevel = Txt(st, "lbl_menu_xp", "LV 1",
            V2(1,1), V2(1,1), V2(-120,-55), V2(200,40), 22, Hex("#4DD0E1"),
            TextAnchor.MiddleRight, FontStyle.Bold);

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

        var tutEqGO = Panel(tt, "TutEquation", V2(.5f,.5f), V2(.5f,.5f), V2(0, 0), V2(700, 800));
        tutEqGO.transform.localScale = new Vector3(0.65f, 0.65f, 1f);
        var teq = tutEqGO.transform;

        // Number1 at top. A digit's visible half-height is 131px, so a 260 pitch
        // left the rows touching; 290 matches the pitch used below the divider.
        var tutNum1 = NumberGroup(teq, "TutNum1", V2(tnumX, 290));
        // Operator
        var tutPM = PlusMinusToggle(teq, V2(-185, 0));
        // Number2
        var tutNum2 = NumberGroup(teq, "TutNum2", V2(tnumX, 0));
        // Divider
        RoundImg(teq, "TutDiv", V2(.5f,.5f), V2(.5f,.5f), V2(tnumX, -145),
            V2(DW*2+80, 3), DIVIDER_C).raycastTarget = false;
        // Answer
        var tutAns = NumberGroup(teq, "TutAns", V2(tnumX, -290));

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
        // One entry: the operator is a single key now, not two sticks
        tapList.Add(tutPM.line1);
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

        // Hint popup card
        var hintCardGO = RoundImg(tt, "hint_card", V2(.5f,.5f), V2(.5f,.5f), V2(0, -330), V2(420, 80), Hex("#1E293BF0"));
        hintCardGO.raycastTarget = false;
        var lblHint = Txt(hintCardGO.transform, "lbl_hint", "",
            V2(0,0), V2(1,1), V2(10,0), V2(-10,0), 22, ACCENT_LIGHT,
            TextAnchor.MiddleCenter, FontStyle.Normal);
        lblHint.raycastTarget = false;

        // Congrats text
        // Above the equation, not on top of it — the demo flashes the lit
        // segments at this exact moment and the player needs to see them.
        var lblCongrats = Txt(tt, "lbl_congrats", "",
            V2(.5f,.5f), V2(.5f,.5f), V2(0, 340), V2(500, 80), 52, WIN_COLOR,
            TextAnchor.MiddleCenter, FontStyle.Bold);
        lblCongrats.raycastTarget = false;
        lblCongrats.gameObject.SetActive(false);

        // Tap marker — a core dot with rings radiating outward, the convention
        // players already know. It sits behind nothing and hides no segment.
        var tapGO = new GameObject("tap_indicator");
        tapGO.transform.SetParent(tt, false);
        var tapRt = tapGO.AddComponent<RectTransform>();
        tapRt.anchorMin = tapRt.anchorMax = V2(.5f,.5f);
        tapRt.sizeDelta = V2(36, 36);

        var tapComp = tapGO.AddComponent<TapIndicator>();
        var ringImgs = new Image[3];
        for (int r = 0; r < 3; r++)
        {
            var ringImg = Img(tapGO.transform, "ring_" + r, V2(0,0), V2(1,1), V2(0,0), V2(0,0), Color.white);
            ringImg.sprite = Ring;
            ringImg.raycastTarget = false;
            ringImgs[r] = ringImg;
        }

        // Core last so it draws over the rings
        var coreImg = Img(tapGO.transform, "core", V2(.5f,.5f), V2(.5f,.5f), V2(0,0), V2(10,10), Color.white);
        coreImg.sprite = SoftDot;
        coreImg.raycastTarget = false;

        tapComp.core = coreImg;
        tapComp.rings = ringImgs;
        tapGO.SetActive(false);

        // ── Wire up TutorialAnimator ─────────────────────────────────
        var tutAnim = pnlTutorial.AddComponent<TutorialAnimator>();
        tutAnim.segsToTap = tapList.ToArray();
        tutAnim.allSegs = allLines;
        tutAnim.tap = tapComp;
        tutAnim.lblHint = lblHint;
        tutAnim.hintBg = hintCardGO;
        tutAnim.lblCongrats = lblCongrats;

        // ── Back arrow (bottom-left) ─────────────────────────────────
        BackArrowButton(tt, "btn_tut_back", V2(0,1), V2(0,1), V2(70, -70), 80, ACCENT_DARK);

        pnlTutorial.SetActive(false);

        // ══════════════════════════════════════════════════════════════════
        //  pnl_modeSelect — Game Mode Selection
        // ══════════════════════════════════════════════════════════════════
        var pnlMode = Panel(ct, "pnl_modeSelect", V2(0,0), V2(1,1), V2(0,0), V2(0,0));
        pnlMode.AddComponent<ScrollablePanel>().referenceHeight = 1920f;
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

        var btnEasyGO = SegButton(ms, "card_easy", "EASY", V2(.5f,1), V2(.5f,1), V2(0,-800),
            V2(600, 140), 46, modeGreen);
        DigTxt(ms, "lbl_easy_desc", "2 DIGITS", V2(.5f,1), V2(.5f,1), V2(0,-896), V2(900,24), 16, modeGreen).raycastTarget = false;
        var btnEasy = btnEasyGO.transform.Find("btn_face").GetComponent<Button>();

        // ── MEDIUM (yellow) ──────────────────────────────────────────
        var btnMedGO = SegButton(ms, "card_medium", "MEDIUM", V2(.5f,1), V2(.5f,1), V2(0,-990),
            V2(600, 140), 46, modeYellow);
        DigTxt(ms, "lbl_med_desc", "3 DIGITS", V2(.5f,1), V2(.5f,1), V2(0,-1086), V2(900,24), 16, modeYellow).raycastTarget = false;
        var btnMed = btnMedGO.transform.Find("btn_face").GetComponent<Button>();

        // ── HARD (red) ───────────────────────────────────────────────
        var btnHardGO = SegButton(ms, "card_hard", "HARD", V2(.5f,1), V2(.5f,1), V2(0,-1180),
            V2(600, 140), 46, modeRed);
        DigTxt(ms, "lbl_hard_desc", "3 NUMBERS - 2 OPERATORS", V2(.5f,1), V2(.5f,1), V2(0,-1276), V2(900,24), 14, modeRed).raycastTarget = false;
        var btnHard = btnHardGO.transform.Find("btn_face").GetComponent<Button>();

        // ── BACK button (bottom-right) ───────────────────────────────
        BackArrowButton(ms, "btn_mode_back", V2(0,1), V2(0,1), V2(70, -70), 80, ACCENT_DARK);

        pnlMode.SetActive(false);

        // ══════════════════════════════════════════════════════════════════
        //  pnl_main — Game Screen (redesigned)
        // ══════════════════════════════════════════════════════════════════
        var pnlMain = Panel(ct, "pnl_main", V2(0,0), V2(1,1), V2(0,0), V2(0,0));
        var mt = pnlMain.transform;

        // Warm dark-gold background for game levels
        Img(mt, "GameBG", V2(0,0), V2(1,1), V2(0,0), V2(0,0), Hex("#141208")).raycastTarget = false;

        // Everything from here down sits ON GameBG, and GameBG can now be
        // paper or mint. Adapt() makes each piece follow it — without that the
        // whole header stays the bright amber it was designed in, which on an
        // off-white board is not readable text.

        // ── Header bar (minimalist) ──────────────────────────────────────
        Adapt(Img(mt, "HeaderLine", V2(0,1), V2(1,1), V2(0,-110), V2(0,3), Hex("#F59E0B15")), Hex("#00000018")).raycastTarget = false;

        // Best score — top left. DigTxt centres by default, so a 900-wide rect
        // used to centre "BEST" on x=60 and print it straight over the digits.
        Adapt(DigTxt(mt, "lbl_best_label", "BEST", V2(0,1), V2(0,1), V2(160,-138), V2(200,33), 22, TEXT_MUTED,
            TextAnchor.MiddleLeft)).raycastTarget = false;
        var lblHS = Txt(mt, "lbl_highscore", "0",
            V2(0,1), V2(0,1), V2(150,-192), V2(180,60), 52, ACCENT_LIGHT,
            TextAnchor.MiddleLeft, FontStyle.Bold);
        Adapt(lblHS);

        // ── Timer section ────────────────────────────────────────────────
        // Pushed below the score row, which owns y 125..185
        var lblTimeLabel = DigTxt(mt, "lbl_time_label", "TIME REMAINING", V2(.5f,1), V2(.5f,1), V2(0,-248), V2(460,24), 16, TEXT_MUTED);
        lblTimeLabel.raycastTarget = false;
        Adapt(lblTimeLabel);

        var lblTimer = Txt(mt, "lbl_timer", "90:00",
            V2(.5f,1), V2(.5f,1), V2(0,-306), V2(460,70), 58, TEXT_PRIMARY,
            TextAnchor.MiddleCenter, FontStyle.Bold);

        // Timer bar with rounded ends
        var timerBarBg = RoundImg(mt, "timer_bar_bg", V2(.5f,1), V2(.5f,1), V2(0,-354), V2(860,10), TIMER_BG);
        timerBarBg.raycastTarget = false;
        Adapt(timerBarBg, Hex("#00000018"));
        var barFill = RoundImg(mt, "timer_bar_fill", V2(.5f,1), V2(.5f,1), V2(0,-354), V2(860,10), ACCENT);
        barFill.type = Image.Type.Filled;
        barFill.fillMethod = Image.FillMethod.Horizontal;
        barFill.fillOrigin = 0; barFill.fillAmount = 1f;
        barFill.raycastTarget = false;

        // ── Equation container ───────────────────────────────────────────
        // A translucent dark card over paper would just be a grey board, so on
        // a light theme it flips to a soft white one instead of darkening.
        var eqBg = RoundImg(mt, "EqBG", V2(.5f,.5f), V2(.5f,.5f), V2(0, -110), V2(880,960), Hex("#0F172A80"));
        Adapt(eqBg, Hex("#FFFFFF5C"));
        eqBg.raycastTarget = false;

        var eqGO = Panel(mt, "Equation", V2(.5f,.5f), V2(.5f,.5f), V2(0,-110), V2(860,940));
        var eq = eqGO.transform;

        float numX = 50f;

        var num1 = NumberGroup(eq, "Number1", V2(numX, 310));
        var pm   = PlusMinusToggle(eq, V2(-200, 0));
        var num2 = NumberGroup(eq, "Number2", V2(numX, 0));

        // Gold divider line with glow
        var divGlow = RoundImg(eq, "DividerGlow", V2(.5f,.5f), V2(.5f,.5f), V2(numX, -165),
            V2(DW * 2 + 120, 8), Hex("#F59E0B20"));
        divGlow.raycastTarget = false;
        RoundImg(eq, "Divider", V2(.5f,.5f), V2(.5f,.5f), V2(numX, -165),
            V2(DW * 2 + 100, 3), DIVIDER_C).raycastTarget = false;

        var ans = NumberGroup(eq, "Answer", V2(numX, -330));

        // Equals sign for landscape (hidden in portrait)
        var eqSign = Txt(eq, "lbl_equals", "=", V2(.5f,.5f), V2(.5f,.5f), V2(0,0), V2(60,80), 60, ACCENT,
            TextAnchor.MiddleCenter, FontStyle.Bold);
        Adapt(eqSign);
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
            new EquationLayout.ElementPos { rt = num1.GetComponent<RectTransform>(), portraitPos = V2(numX, 310), landscapePos = V2(lx, 0) },
            new EquationLayout.ElementPos { rt = pm.GetComponent<RectTransform>(), portraitPos = V2(-200, 0), landscapePos = V2(lx + 260, 0) },
            new EquationLayout.ElementPos { rt = num2.GetComponent<RectTransform>(), portraitPos = V2(numX, 0), landscapePos = V2(lx + 500, 0) },
            new EquationLayout.ElementPos { rt = eqSign.rectTransform, portraitPos = V2(0, 0), landscapePos = V2(lx + 700, 0) },
            new EquationLayout.ElementPos { rt = ans.GetComponent<RectTransform>(), portraitPos = V2(numX, -330), landscapePos = V2(lx + 900, 0) },
        };

        // ── 3-digit equation (Medium mode) ───────────────────────────────
        var eqBg3 = RoundImg(mt, "EqBG3", V2(.5f,.5f), V2(.5f,.5f), V2(0, -110), V2(880,960), Hex("#0F172A80"));
        eqBg3.raycastTarget = false;

        var eqGO3 = Panel(mt, "Equation3D", V2(.5f,.5f), V2(.5f,.5f), V2(0,-110), V2(900,940));
        var eq3 = eqGO3.transform;
        // Scale down to fit 3-digit numbers (digits are bigger now)
        eqGO3.transform.localScale = new Vector3(0.75f, 0.75f, 1f);

        float numX3 = 60f;

        var num1_3d = NumberGroup3(eq3, "Number1_3D", V2(numX3, 310));
        var pm3d    = PlusMinusToggle(eq3, V2(-240, 0));
        var num2_3d = NumberGroup3(eq3, "Number2_3D", V2(numX3, 0));

        // Gold divider
        RoundImg(eq3, "DivGlow3", V2(.5f,.5f), V2(.5f,.5f), V2(numX3, -165),
            V2(DW * 3 + 120, 8), Hex("#F59E0B20")).raycastTarget = false;
        RoundImg(eq3, "Divider3", V2(.5f,.5f), V2(.5f,.5f), V2(numX3, -165),
            V2(DW * 3 + 100, 3), DIVIDER_C).raycastTarget = false;

        var ans_3d = NumberGroup3(eq3, "Answer_3D", V2(numX3, -330));

        // Equals sign for landscape (hidden in portrait)
        var eqSign3 = Txt(eq3, "lbl_equals3", "=", V2(.5f,.5f), V2(.5f,.5f), V2(0,0), V2(60,80), 60, ACCENT,
            TextAnchor.MiddleCenter, FontStyle.Bold);
        Adapt(eqSign3);
        eqSign3.gameObject.SetActive(false);

        // EquationLayout for Medium mode
        var elMed = eqGO3.AddComponent<EquationLayout>();
        elMed.container = eqGO3.GetComponent<RectTransform>();
        elMed.containerPortraitSize = new Vector2(900, 940);
        elMed.containerLandscapeSize = new Vector2(1800, 300);
        elMed.portraitScale = 0.75f;
        elMed.landscapeScale = 0.50f;
        elMed.divider = eq3.Find("Divider3").GetComponent<RectTransform>();
        elMed.dividerGlow = eq3.Find("DivGlow3").GetComponent<RectTransform>();
        elMed.equalsSign = eqSign3.rectTransform;
        elMed.eqBackground = eqBg3.rectTransform;
        float lx3 = -600f;
        elMed.elements = new EquationLayout.ElementPos[] {
            new EquationLayout.ElementPos { rt = num1_3d.GetComponent<RectTransform>(), portraitPos = V2(numX3, 310), landscapePos = V2(lx3, 0) },
            new EquationLayout.ElementPos { rt = pm3d.GetComponent<RectTransform>(), portraitPos = V2(-240, 0), landscapePos = V2(lx3 + 320, 0) },
            new EquationLayout.ElementPos { rt = num2_3d.GetComponent<RectTransform>(), portraitPos = V2(numX3, 0), landscapePos = V2(lx3 + 640, 0) },
            new EquationLayout.ElementPos { rt = eqSign3.rectTransform, portraitPos = V2(0, 0), landscapePos = V2(lx3 + 920, 0) },
            new EquationLayout.ElementPos { rt = ans_3d.GetComponent<RectTransform>(), portraitPos = V2(numX3, -330), landscapePos = V2(lx3 + 1180, 0) },
        };

        // Hide 3-digit panel by default
        eqBg3.gameObject.SetActive(false);
        eqGO3.SetActive(false);

        // ── Hard mode equation (A ± B ± C = D, 2 digits each) ─────────
        var eqBgH = RoundImg(mt, "EqBGHard", V2(.5f,.5f), V2(.5f,.5f), V2(0, -110), V2(880,960), Hex("#0F172A80"));
        eqBgH.raycastTarget = false;

        var eqGOH = Panel(mt, "EquationHard", V2(.5f,.5f), V2(.5f,.5f), V2(0, -110), V2(880,940));
        var eqH = eqGOH.transform;
        eqGOH.transform.localScale = new Vector3(0.72f, 0.72f, 1f);

        float hx = 50f;
        float rowH = 300f; // generous vertical spacing for taller digits

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
        Adapt(eqSignH);
        eqSignH.gameObject.SetActive(false);

        // EquationLayout for Hard mode
        var elHard = eqGOH.AddComponent<EquationLayout>();
        elHard.container = eqGOH.GetComponent<RectTransform>();
        elHard.containerPortraitSize = new Vector2(880, 940);
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
        Adapt(DigTxt(mt, "lbl_hint", "TAP STICKS - BUILD DIGITS", V2(.5f,0), V2(.5f,0), V2(0,112), V2(900,21), 14, TEXT_MUTED)).raycastTarget = false;

        pnlMain.SetActive(false);

        // ══════════════════════════════════════════════════════════════════
        //  pnl_continue — Result Screen (redesigned)
        // ══════════════════════════════════════════════════════════════════
        var pnlCont = ContinuePanel(ct);
        pnlCont.SetActive(false);

        // ── Volume icon (top right corner) ───────────────────────────────
        // ── Audio key + stepped volume popover ───────────────────────────
        // The old control was a 70px glyph that could only mute. 70px is 28pt
        // at this scale factor, under the 44pt minimum touch target, so it had
        // to be rebuilt regardless — which made this the moment to give it a
        // real level readout instead of an on/off state.
        //
        // The container is the 120px touch target; the 76px key is the visual.
        var volCtlGO = Panel(ct, "VolumeControl", V2(1,1), V2(1,1), V2(-8, -135), V2(120, 120));
        var volCtlRt = volCtlGO.GetComponent<RectTransform>();
        volCtlRt.pivot = V2(1, 1);
        var volKeyGroup = volCtlGO.AddComponent<CanvasGroup>();
        var vct = volCtlGO.transform;

        var volBtnGO = Panel(vct, "btn_volume", V2(0,0), V2(1,1), V2(0,0), V2(0,0));
        var volHit = volBtnGO.AddComponent<Image>();
        volHit.color = new Color(0, 0, 0, 0.01f);
        var volBtn = volBtnGO.AddComponent<Button>();
        volBtn.targetGraphic = volHit;
        var volBc = ColorBlock.defaultColorBlock;
        volBc.normalColor = Color.white;
        volBc.highlightedColor = new Color(1.25f, 1.25f, 1.25f);
        volBc.pressedColor = new Color(0.6f, 0.6f, 0.6f);
        volBtn.colors = volBc;
        var vkt = volBtnGO.transform;

        // Same three layers and the same offsets as every SegButton, so the key
        // is built in the game's existing button language rather than beside it.
        var volShadow = Img(vkt, "key_shadow", V2(.5f,.5f), V2(.5f,.5f), V2(0,-BTN_ELEV), V2(76,76), Hex("#0A1400D9"));
        volShadow.sprite = RoundRectTight; volShadow.type = Image.Type.Sliced;
        volShadow.pixelsPerUnitMultiplier = 1f; volShadow.raycastTarget = false;

        var volRim = Img(vkt, "key_rim", V2(.5f,.5f), V2(.5f,.5f), V2(0,0), V2(76,76), ACCENT_DARK);
        volRim.sprite = RoundRectTight; volRim.type = Image.Type.Sliced;
        volRim.pixelsPerUnitMultiplier = 1f; volRim.raycastTarget = false;

        var volFace = Img(vkt, "key_face", V2(.5f,.5f), V2(.5f,.5f), V2(0,0), V2(70,70), Hex("#142B01"));
        volFace.sprite = RoundRectTight; volFace.type = Image.Type.Sliced;
        volFace.pixelsPerUnitMultiplier = 1f; volFace.raycastTarget = false;

        var volCone = Img(vkt, "glyph_cone", V2(.5f,.5f), V2(.5f,.5f), V2(-14,0), V2(28,28), ACCENT);
        volCone.sprite = SpeakerCone; volCone.raycastTarget = false;

        var volSlash = Img(vkt, "glyph_slash", V2(.5f,.5f), V2(.5f,.5f), V2(-8,0), V2(34,34), ACCENT_DARK);
        volSlash.sprite = SpeakerSlash; volSlash.raycastTarget = false;
        volSlash.gameObject.SetActive(false);

        // Level readout: a column of cells, which is this game's own idiom.
        // Five cells map 1:1 onto AudioManager.STEPS with no lossy rounding —
        // speaker wave arcs cannot show five levels without two of them
        // drawing identically.
        var volCells = new Image[5];
        var volCellOutlines = new Image[5];
        for (int i = 0; i < 5; i++)
        {
            float cy = -20f + i * 10f;
            var outline = Img(vkt, "cell_outline_" + i, V2(.5f,.5f), V2(.5f,.5f), V2(15, cy), V2(15, 8), ACCENT_DARK);
            outline.sprite = RoundRectTight; outline.type = Image.Type.Sliced;
            outline.pixelsPerUnitMultiplier = 1f; outline.raycastTarget = false;
            outline.gameObject.SetActive(false);
            volCellOutlines[i] = outline;

            var cell = Img(vkt, "cell_" + i, V2(.5f,.5f), V2(.5f,.5f), V2(15, cy), V2(13, 6), ACCENT);
            cell.sprite = RoundRectTight; cell.type = Image.Type.Sliced;
            cell.pixelsPerUnitMultiplier = 1f; cell.raycastTarget = false;
            volCells[i] = cell;
        }

        // Invisible dismiss catcher. A visible dim would be exactly the
        // "dominates the screen" failure this redesign is avoiding.
        var volScrim = Panel(ct, "ScrimVolume", V2(0,0), V2(1,1), V2(0,0), V2(0,0));
        var volScrimImg = volScrim.AddComponent<Image>();
        volScrimImg.color = new Color(0, 0, 0, 0f);
        var volScrimBtn = volScrim.AddComponent<Button>();
        volScrimBtn.targetGraphic = volScrimImg;
        volScrim.SetActive(false);

        // Vertical, hugging the right edge. The canvas resolves to ~978 ref px
        // wide, not 1080, so the 920-wide equation leaves only ~29px of margin —
        // a horizontal popover would sit straight across the digits.
        var volPop = Panel(vct, "pnl_volume_popover", V2(1,0), V2(1,0), V2(-22, -14), V2(152, 486));
        var volPopRt = volPop.GetComponent<RectTransform>();
        volPopRt.pivot = V2(1, 1);
        var volPopGroup = volPop.AddComponent<CanvasGroup>();
        var vpt = volPop.transform;

        var volPopShadow = Img(vpt, "card_shadow", V2(0,0), V2(1,1), V2(0,-8), V2(0,0), Hex("#030703CC"));
        volPopShadow.sprite = RoundRect; volPopShadow.type = Image.Type.Sliced;
        volPopShadow.pixelsPerUnitMultiplier = 1f; volPopShadow.raycastTarget = false;

        var volPopRim = Img(vpt, "card_rim", V2(0,0), V2(1,1), V2(0,0), V2(0,0), ACCENT_DARK);
        volPopRim.sprite = RoundRect; volPopRim.type = Image.Type.Sliced;
        volPopRim.pixelsPerUnitMultiplier = 1f;

        // Opaque: this overlays live gameplay, and a translucent wash over lit
        // 7-segment digits is unreadable.
        var volPopFace = Img(vpt, "card_face", V2(0,0), V2(1,1), V2(0,0), V2(-4,-4), Hex("#0E140EFA"));
        volPopFace.sprite = RoundRect; volPopFace.type = Image.Type.Sliced;
        volPopFace.pixelsPerUnitMultiplier = 1f; volPopFace.raycastTarget = false;

        var volPlus = SegButton(vpt, "btn_vol_plus", "", V2(.5f,1), V2(.5f,1), V2(0,-76), V2(128,128), 1, ACCENT);
        var volMinus = SegButton(vpt, "btn_vol_minus", "", V2(.5f,1), V2(.5f,1), V2(0,-402), V2(128,128), 1, ACCENT);

        // Drawn bars rather than glyphs: Orbitron's hyphen is short and thin
        // and reads as a dash, not as a minus key.
        var plusFace = volPlus.transform.Find("btn_face");
        Img(plusFace, "bar_h", V2(.5f,.5f), V2(.5f,.5f), V2(0,0), V2(44,8), ACCENT).raycastTarget = false;
        Img(plusFace, "bar_v", V2(.5f,.5f), V2(.5f,.5f), V2(0,0), V2(8,44), ACCENT).raycastTarget = false;
        var minusFace = volMinus.transform.Find("btn_face");
        Img(minusFace, "bar_h", V2(.5f,.5f), V2(.5f,.5f), V2(0,0), V2(44,8), ACCENT).raycastTarget = false;

        // Ladder reads top-down: pip 0 is the loudest step
        var volPips = new Image[5];
        var volPipOutlines = new Image[5];
        for (int i = 0; i < 5; i++)
        {
            float py = -167f - i * 36f;
            var outline = Img(vpt, "pip_outline_" + i, V2(.5f,1), V2(.5f,1), V2(0, py), V2(116, 30), ACCENT_DARK);
            outline.sprite = RoundRectTight; outline.type = Image.Type.Sliced;
            outline.pixelsPerUnitMultiplier = 1f; outline.raycastTarget = false;
            outline.gameObject.SetActive(false);
            volPipOutlines[i] = outline;

            var pip = Img(vpt, "pip_" + i, V2(.5f,1), V2(.5f,1), V2(0, py), V2(112, 26), ACCENT);
            pip.sprite = RoundRectTight; pip.type = Image.Type.Sliced;
            pip.pixelsPerUnitMultiplier = 1f; pip.raycastTarget = false;
            volPips[i] = pip;
        }

        volPop.SetActive(false);

        var volCtl = volCtlGO.AddComponent<VolumeControl>();
        volCtl.glyphCone = volCone;
        volCtl.glyphSlash = volSlash;
        volCtl.keyCells = volCells;
        volCtl.keyOutlines = volCellOutlines;
        volCtl.keyGroup = volKeyGroup;
        volCtl.popover = volPop;
        volCtl.scrim = volScrim;
        volCtl.popGroup = volPopGroup;
        volCtl.pips = volPips;
        volCtl.pipOutlines = volPipOutlines;
        volCtl.btnPlus = volPlus.transform.Find("btn_face").GetComponent<Button>();
        volCtl.btnMinus = volMinus.transform.Find("btn_face").GetComponent<Button>();

        UnityEventTools.AddPersistentListener(volBtn.onClick, volCtl.Toggle);
        UnityEventTools.AddPersistentListener(volScrimBtn.onClick, volCtl.Close);
        UnityEventTools.AddPersistentListener(volCtl.btnPlus.onClick, volCtl.StepUp);
        UnityEventTools.AddPersistentListener(volCtl.btnMinus.onClick, volCtl.StepDown);

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
        pnlArcMode.AddComponent<ScrollablePanel>().referenceHeight = 1920f;
        var amt = pnlArcMode.transform;
        RoundImg(amt, "ArcModeBG", V2(0,0), V2(1,1), V2(0,0), V2(0,0), Hex("#0A0E1AFA")).raycastTarget = false;

        DigTxt(amt, "lbl_arcmode_title", "ARCADE 1V1", V2(.5f,1), V2(.5f,1), V2(0,-70), V2(900,63), 42, ACCENT).raycastTarget = false;
        RoundImg(amt, "ArcModeAccent", V2(.5f,1), V2(.5f,1), V2(0,-115), V2(120,3), ACCENT).raycastTarget = false;

        // Mode title (updates dynamically)
        var lblArcModeTitle = Txt(amt, "lbl_arcmode_info", "EASY  ·  " + MatchLength.Label(MatchLength.DefaultFirstTo),
            V2(.5f,1), V2(.5f,1), V2(0,-150), V2(500,30), 16, TEXT_MUTED,
            TextAnchor.MiddleCenter, FontStyle.Normal);

        // Mode buttons
        Color mGreen2  = Hex("#76FF03");
        Color mYellow2 = Hex("#FFD600");
        Color mRed2    = Hex("#FF1744");
        Color mCyan    = Hex("#00E5FF");

        var abEasy = SegButton(amt, "btn_arc_easy", "EASY", V2(.5f,1), V2(.5f,1), V2(0,-230),
            V2(380, 80), 28, mGreen2);
        DigTxt(amt, "lbl_easy_info", "2 DIGITS  ·  PLAY 3  ·  WIN 10  ·  RANK x0.75",
            V2(.5f,1), V2(.5f,1), V2(0,-292), V2(560,22), 14, TEXT_MUTED).raycastTarget = false;

        var abMed = SegButton(amt, "btn_arc_medium", "MEDIUM", V2(.5f,1), V2(.5f,1), V2(0,-370),
            V2(380, 80), 28, mYellow2);
        DigTxt(amt, "lbl_med_info", "3 DIGITS  ·  PLAY 5  ·  WIN 18  ·  RANK x1.0",
            V2(.5f,1), V2(.5f,1), V2(0,-432), V2(560,22), 14, TEXT_MUTED).raycastTarget = false;

        var abHard = SegButton(amt, "btn_arc_hard", "HARD", V2(.5f,1), V2(.5f,1), V2(0,-510),
            V2(380, 80), 28, mRed2);
        DigTxt(amt, "lbl_hard_info", "3 NUMBERS  ·  PLAY 9  ·  WIN 30  ·  RANK x1.25",
            V2(.5f,1), V2(.5f,1), V2(0,-572), V2(560,22), 14, TEXT_MUTED).raycastTarget = false;

        var abRand = SegButton(amt, "btn_arc_random", "RANDOM", V2(.5f,1), V2(.5f,1), V2(0,-650),
            V2(380, 80), 28, mCyan);
        DigTxt(amt, "lbl_rand_info", "DIFFICULTY PICKED WHEN THE MATCH STARTS",
            V2(.5f,1), V2(.5f,1), V2(0,-712), V2(560,22), 14, TEXT_MUTED).raycastTarget = false;

        // Match length, counted in rounds played rather than rounds won — "5"
        // used to mean first to five wins, which is a nine-round match. The
        // rule line underneath spells out how many of them decide it.
        DigTxt(amt, "lbl_match_length", "MATCH LENGTH", V2(.5f,1), V2(.5f,1), V2(0,-782), V2(900,24), 16, TEXT_MUTED).raycastTarget = false;

        var abRounds1 = SegButton(amt, "btn_rounds_1", MatchLength.OPTIONS[0].ToString(), V2(.5f,1), V2(.5f,1), V2(-140,-848),
            V2(110, 70), 28, arcCol);
        var abRounds2 = SegButton(amt, "btn_rounds_3", MatchLength.OPTIONS[1].ToString(), V2(.5f,1), V2(.5f,1), V2(0,-848),
            V2(110, 70), 28, arcCol);
        var abRounds3 = SegButton(amt, "btn_rounds_5", MatchLength.OPTIONS[2].ToString(), V2(.5f,1), V2(.5f,1), V2(140,-848),
            V2(110, 70), 28, arcCol);

        var lblRoundsRule = DigTxt(amt, "lbl_rounds_rule",
            MatchLength.DEFAULT_ROUNDS + " ROUNDS  ·  FIRST TO " + MatchLength.DefaultFirstTo + " WINS",
            V2(.5f,1), V2(.5f,1), V2(0,-908), V2(620,22), 14, TEXT_MUTED);
        lblRoundsRule.raycastTarget = false;

        // A group only reads as a choice if the chosen key looks chosen. These
        // were coloured once at build time and never touched again, so tapping
        // any but the first appeared to do nothing.
        var selRounds = new[] { Selectable(abRounds1, arcCol), Selectable(abRounds2, arcCol), Selectable(abRounds3, arcCol) };
        var selModes  = new[] { Selectable(abEasy, mGreen2), Selectable(abMed, mYellow2),
                                Selectable(abHard, mRed2),  Selectable(abRand, mCyan) };

        // Bake the opening state — EASY, best of 3 — so the panel is already
        // right on the first frame instead of at the first tap.
        for (int i = 0; i < selRounds.Length; i++)
            selRounds[i].SetSelected(MatchLength.OPTIONS[i] == MatchLength.DEFAULT_ROUNDS);
        for (int i = 0; i < selModes.Length; i++)  selModes[i].SetSelected(i == 0);

        // Action buttons
        var abRandom = SegButton(amt, "btn_random_battle", "RANDOM BATTLE", V2(.5f,1), V2(.5f,1), V2(0,-975),
            V2(420, 90), 30, ACCENT);
        var abInvite = SegButton(amt, "btn_show_lobby", "INVITE PLAYER", V2(.5f,1), V2(.5f,1), V2(0,-1085),
            V2(420, 80), 26, arcDim);

        // Back
        var lblArcLevel = Txt(amt, "lbl_arcade_xp", "LV 1",
            V2(1,1), V2(1,1), V2(-120,-72), V2(200,40), 22, Hex("#4DD0E1"),
            TextAnchor.MiddleRight, FontStyle.Bold);
        lblArcLevel.raycastTarget = false;

        var abBack = BackArrowButton(amt, "btn_arc_back", V2(0,1), V2(0,1), V2(70, -70), 80, arcDim);

        pnlArcMode.SetActive(false);

        // ── pnl_lobby ────────────────────────────────────────────────────
        var pnlLobby = Panel(ct, "pnl_lobby", V2(0,0), V2(1,1), V2(0,0), V2(0,0));
        var lbt = pnlLobby.transform;
        RoundImg(lbt, "LobbyBG", V2(0,0), V2(1,1), V2(0,0), V2(0,0), Hex("#0A0E1AFA")).raycastTarget = false;

        DigTxt(lbt, "lbl_lobby_title", "LOBBY", V2(.5f,1), V2(.5f,1), V2(0,-70), V2(900,54), 36, ACCENT).raycastTarget = false;

        var lblLobbyStatus = Txt(lbt, "lbl_lobby_status", "0 ONLINE",
            V2(1,1), V2(1,1), V2(-140,-560), V2(200,30), 14, TEXT_MUTED,
            TextAnchor.MiddleRight, FontStyle.Normal);

        // Search field
        var searchGO = new GameObject("inp_search");
        searchGO.transform.SetParent(lbt, false);
        var searchRt = searchGO.AddComponent<RectTransform>();
        searchRt.anchorMin = V2(.5f,1); searchRt.anchorMax = V2(.5f,1);
        searchRt.anchoredPosition = V2(0,-130); searchRt.sizeDelta = V2(700,50);
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
        var scrollRect = TuneScroll(scrollGO.AddComponent<UnityEngine.UI.ScrollRect>());
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

        // User row prefab (template) — name + ADD FRIEND + INVITE
        var userRowGO = new GameObject("UserRowTemplate");
        userRowGO.transform.SetParent(lbt, false);
        var urRt = userRowGO.AddComponent<RectTransform>();
        urRt.sizeDelta = V2(0,70);
        var urImg = userRowGO.AddComponent<Image>();
        urImg.color = Hex("#151A28");
        var urLE = userRowGO.AddComponent<LayoutElement>();
        urLE.preferredHeight = 70;

        // x = inset + width/2, because the rect pivot is centred: at x=20 the
        // rect started at -105 and the name drew outside the row entirely.
        Txt(userRowGO.transform, "lbl_name", "Player",
            V2(0,.5f), V2(0,.5f), V2(145,0), V2(250,40), 20, TEXT_PRIMARY,
            TextAnchor.MiddleLeft, FontStyle.Normal);

        // "+" add friend button
        SegButton(userRowGO.transform, "btn_add_friend", "+",
            V2(1,.5f), V2(1,.5f), V2(-175,0), V2(50,50), 24, Hex("#4ADE80"));

        // INVITE button
        SegButton(userRowGO.transform, "btn_invite", "INVITE",
            V2(1,.5f), V2(1,.5f), V2(-80,0), V2(120,50), 18, arcCol);

        userRowGO.SetActive(false); // template

        // ── FRIENDS SECTION ──────────────────────────────────────────────
        // Title divider
        Img(lbt, "FriendDivider", V2(.5f,1), V2(.5f,1), V2(0,-215), V2(800,2), Hex("#334155")).raycastTarget = false;

        DigTxt(lbt, "lbl_friends_title", "FRIENDS", V2(0,1), V2(0,1), V2(200,-225), V2(300,30), 22, ACCENT,
            TextAnchor.MiddleLeft).raycastTarget = false;

        var lblFriendsStatus = Txt(lbt, "lbl_friends_status", "0 FRIENDS",
            V2(1,1), V2(1,1), V2(-140,-225), V2(200,30), 14, TEXT_MUTED,
            TextAnchor.MiddleRight, FontStyle.Normal);

        // Friends scroll view (top half)
        var friendScrollGO = new GameObject("FriendScrollView");
        friendScrollGO.transform.SetParent(lbt, false);
        var fScrollRt = friendScrollGO.AddComponent<RectTransform>();
        fScrollRt.anchorMin = V2(0,1); fScrollRt.anchorMax = V2(1,1);
        fScrollRt.offsetMin = V2(40,-540); fScrollRt.offsetMax = V2(-40,-250);
        friendScrollGO.AddComponent<Image>().color = new Color(0,0,0,0.01f);
        var fScrollRect = TuneScroll(friendScrollGO.AddComponent<UnityEngine.UI.ScrollRect>());
        fScrollRect.horizontal = false;
        friendScrollGO.AddComponent<Mask>().showMaskGraphic = false;

        var fContentGO = new GameObject("Content");
        fContentGO.transform.SetParent(friendScrollGO.transform, false);
        var fContentRt = fContentGO.AddComponent<RectTransform>();
        fContentRt.anchorMin = V2(0,1); fContentRt.anchorMax = V2(1,1);
        fContentRt.pivot = V2(0.5f,1); fContentRt.sizeDelta = V2(0,0);
        var fVlg = fContentGO.AddComponent<VerticalLayoutGroup>();
        fVlg.spacing = 6;
        fVlg.childForceExpandWidth = true;
        fVlg.childForceExpandHeight = false;
        fVlg.childControlWidth = true;
        fVlg.childControlHeight = false;
        var fCsf = fContentGO.AddComponent<ContentSizeFitter>();
        fCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        fScrollRect.content = fContentRt;

        // Friend row prefab — green dot + name + status + INVITE + X remove
        var friendRowGO = new GameObject("FriendRowTemplate");
        friendRowGO.transform.SetParent(lbt, false);
        var frRt = friendRowGO.AddComponent<RectTransform>();
        frRt.sizeDelta = V2(0,65);
        friendRowGO.AddComponent<Image>().color = Hex("#121825");
        var frLE = friendRowGO.AddComponent<LayoutElement>();
        frLE.preferredHeight = 65;

        // Online status dot
        var statusDot = Img(friendRowGO.transform, "img_status", V2(0,.5f), V2(0,.5f), V2(18,0), V2(12,12), Hex("#4ADE80"));
        statusDot.raycastTarget = false;

        // Name
        Txt(friendRowGO.transform, "lbl_name", "Friend",
            V2(0,.5f), V2(0,.5f), V2(145,8), V2(220,30), 20, TEXT_PRIMARY,
            TextAnchor.MiddleLeft, FontStyle.Normal);

        // Online/Offline label
        Txt(friendRowGO.transform, "lbl_status", "ONLINE",
            V2(0,.5f), V2(0,.5f), V2(145,-14), V2(220,20), 12, TEXT_MUTED,
            TextAnchor.MiddleLeft, FontStyle.Normal);

        // INVITE button
        SegButton(friendRowGO.transform, "btn_invite", "INVITE",
            V2(1,.5f), V2(1,.5f), V2(-100,0), V2(110,45), 16, arcCol);

        // X remove button
        SegButton(friendRowGO.transform, "btn_remove", "X",
            V2(1,.5f), V2(1,.5f), V2(-25,0), V2(40,40), 18, Hex("#EF4444"));

        friendRowGO.SetActive(false); // template

        // ── Online Players section title (moved below friends) ───────
        var onlineDivider = Img(lbt, "OnlineDivider", V2(.5f,1), V2(.5f,1), V2(0,-550), V2(800,2), Hex("#334155"));
        onlineDivider.raycastTarget = false;
        var lblOnlineTitle = DigTxt(lbt, "lbl_online_title", "ONLINE PLAYERS", V2(0,1), V2(0,1), V2(250,-560), V2(400,30), 22, ACCENT,
            TextAnchor.MiddleLeft);
        lblOnlineTitle.raycastTarget = false;

        // Move online player scroll to bottom half
        scrollRt.anchorMin = V2(0,0); scrollRt.anchorMax = V2(1,1);
        scrollRt.offsetMin = V2(40,100); scrollRt.offsetMax = V2(-40,-590);

        // A directory of everyone online is a feature of a game that HAS
        // people online. At launch it is a heading over an empty box that says
        // 0 ONLINE, which reads as broken rather than new — so it is built but
        // switched off, and the friends list takes the whole panel. Flip
        // LobbyManager.ShowOnlinePlayers when there is a population to show.
        if (!LobbyManager.SHOW_ONLINE_PLAYERS)
        {
            onlineDivider.gameObject.SetActive(false);
            lblOnlineTitle.gameObject.SetActive(false);
            lblLobbyStatus.gameObject.SetActive(false);
            scrollGO.SetActive(false);

            fScrollRt.offsetMin = V2(40, 100);   // friends fill the panel
        }

        var lbBack = BackArrowButton(lbt, "btn_lobby_back", V2(0,1), V2(0,1), V2(70, -70), 80, arcDim);

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
            V2(.5f,.5f), V2(.5f,.5f), V2(0,150), V2(600,80), 56, ACCENT,
            TextAnchor.MiddleCenter, FontStyle.Bold);

        var lblResScore = Txt(art, "lbl_result_score", "3 - 1",
            V2(.5f,.5f), V2(.5f,.5f), V2(0,62), V2(400,60), 48, TEXT_PRIMARY,
            TextAnchor.MiddleCenter, FontStyle.Bold);

        var lblResDetail = Txt(art, "lbl_result_detail", "VS OPPONENT",
            V2(.5f,.5f), V2(.5f,.5f), V2(0,6), V2(400,30), 18, TEXT_MUTED,
            TextAnchor.MiddleCenter, FontStyle.Normal);

        var lblResAns = Txt(art, "lbl_result_answer", "",
            V2(.5f,.5f), V2(.5f,.5f), V2(0,-42), V2(800,40), 26, ACCENT_LIGHT,
            TextAnchor.MiddleCenter, FontStyle.Bold);
        lblResAns.raycastTarget = false;

        // Rating change — the reason to play "one more"
        var lblResElo = Txt(art, "lbl_result_elo", "",
            V2(.5f,.5f), V2(.5f,.5f), V2(0,-88), V2(500,40), 26, ACCENT,
            TextAnchor.MiddleCenter, FontStyle.Bold);

        var arRematch = SegButton(art, "btn_rematch", "REMATCH", V2(.5f,.5f), V2(.5f,.5f), V2(0,-170),
            V2(380, 80), 28, arcCol);

        var lblRematchStatus = Txt(art, "lbl_rematch_status", "",
            V2(.5f,.5f), V2(.5f,.5f), V2(0,-251), V2(500,30), 16, TEXT_MUTED,
            TextAnchor.MiddleCenter, FontStyle.Normal);

        var arLobby = SegButton(art, "btn_return_lobby", "RETURN TO LOBBY", V2(.5f,.5f), V2(.5f,.5f), V2(0,-321),
            V2(380, 70), 22, arcDim);
        var arMenu = SegButton(art, "btn_return_menu", "MAIN MENU", V2(.5f,.5f), V2(.5f,.5f), V2(0,-417),
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

        var lblInvMode = Txt(ict, "lbl_invite_mode", "EASY  ·  " + MatchLength.Label(MatchLength.DefaultFirstTo),
            V2(.5f,1), V2(.5f,1), V2(0,-130), V2(400,30), 18, TEXT_MUTED,
            TextAnchor.MiddleCenter, FontStyle.Normal);

        var invAccept = SegButton(ict, "btn_accept", "ACCEPT", V2(.5f,.5f), V2(.5f,.5f), V2(0,-10),
            V2(350, 70), 26, Hex("#76FF03"));
        var invDecline = SegButton(ict, "btn_decline", "DECLINE", V2(.5f,0), V2(.5f,0), V2(0,40),
            V2(350, 60), 22, Hex("#FF1744"));

        pnlInvite.SetActive(false);

        // The out-of-coins sheet used to live here. It existed because a coin
        // balance could hit zero, and XP cannot — so the sheet, the ad button
        // on it, the free-entry counter and the new-player shield all went
        // with it. The rewarded ad now sits on the result screen instead,
        // where it reaches everyone rather than only a player who has run out.

        // ── Simulated-ad placeholder, used until a real SDK is installed ──
        var pnlAdStub = Panel(ct, "pnl_ad_stub", V2(0,0), V2(1,1), V2(0,0), V2(0,0));
        Img(pnlAdStub.transform, "AdStubBG", V2(0,0), V2(1,1), V2(0,0), V2(0,0), new Color(0,0,0,0.95f)).raycastTarget = true;
        DigTxt(pnlAdStub.transform, "lbl_ad_stub_tag", "NO AD SDK INSTALLED", V2(.5f,.5f), V2(.5f,.5f), V2(0,60), V2(900,26), 18, Hex("#FF1744"))
            .raycastTarget = false;
        var lblAdStub = Txt(pnlAdStub.transform, "lbl_ad_stub", "SIMULATED AD",
            V2(.5f,.5f), V2(.5f,.5f), V2(0,0), V2(900,50), 32, TEXT_PRIMARY,
            TextAnchor.MiddleCenter, FontStyle.Bold);
        lblAdStub.raycastTarget = false;
        pnlAdStub.SetActive(false);

        // ── pnl_roundOverlay (brief "ROUND WON/LOST" flash) ─────────────
        var pnlRoundOvr = Panel(ct, "pnl_roundOverlay", V2(0,0), V2(1,1), V2(0,0), V2(0,0));
        var rot = pnlRoundOvr.transform;
        Img(rot, "RoundOvrBG", V2(0,0), V2(1,1), V2(0,0), V2(0,0), new Color(0,0,0,0.8f)).raycastTarget = true;
        var lblRoundRes = Txt(rot, "lbl_round_result", "ROUND WON!",
            V2(.5f,.5f), V2(.5f,.5f), V2(0,40), V2(600,80), 52, ACCENT,
            TextAnchor.MiddleCenter, FontStyle.Bold);

        DigTxt(rot, "lbl_round_answer_tag", "CORRECT ANSWER", V2(.5f,.5f), V2(.5f,.5f),
            V2(0,-30), V2(600,24), 16, TEXT_MUTED).raycastTarget = false;

        var lblRoundAns = Txt(rot, "lbl_round_answer", "",
            V2(.5f,.5f), V2(.5f,.5f), V2(0,-78), V2(800,60), 40, ACCENT_LIGHT,
            TextAnchor.MiddleCenter, FontStyle.Bold);
        lblRoundAns.raycastTarget = false;

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
        mgrs.AddComponent<OrientationManager>();
        mgrs.AddComponent<GoogleSignInBridge>();
        var fbBridge = mgrs.AddComponent<FacebookSignInBridge>();
        fbBridge.facebookAppId = "1409681074530831";
        mgrs.AddComponent<FirebaseDBManager>();
        mgrs.AddComponent<GameSettings>();
        mgrs.AddComponent<LobbyManager>();
        mgrs.AddComponent<ArcadeMatchManager>();
        mgrs.AddComponent<BotMatchManager>();
        mgrs.AddComponent<PlayerStatsManager>();
        mgrs.AddComponent<LeaderboardManager>();
        mgrs.AddComponent<DailyManager>();
        var adMgr = mgrs.AddComponent<AdManager>();
        adMgr.stubPanel = pnlAdStub;
        adMgr.stubCountdown = lblAdStub;

        var arcGui = mgrs.AddComponent<ArcadeGUIManager>();
        arcGui.pnl_arcadeModeSelect = pnlArcMode;
        arcGui.pnl_lobby = pnlLobby;
        arcGui.pnl_arcadeWaiting = pnlArcWait;
        arcGui.pnl_arcadeHUD = pnlArcHUD;
        arcGui.pnl_arcadeResult = pnlArcResult;
        arcGui.pnl_invitePopup = pnlInvite;
        arcGui.lbl_modeSelectTitle = lblArcModeTitle;
        arcGui.lbl_roundsRule = lblRoundsRule;
        arcGui.sel_rounds = selRounds;
        arcGui.sel_mode = selModes;
        arcGui.inp_search = searchInp;
        arcGui.userListContent = contentRt;
        arcGui.userRowPrefab = userRowGO;
        arcGui.friendListContent = fContentRt;
        arcGui.friendRowPrefab = friendRowGO;
        arcGui.lbl_lobbyStatus = lblLobbyStatus;
        arcGui.lbl_friendsStatus = lblFriendsStatus;
        arcGui.lbl_waitingStatus = lblWaiting;
        arcGui.lbl_myScore = lblMyScore;
        arcGui.lbl_oppScore = lblOppScore;
        arcGui.lbl_roundInfo = lblRoundInfo;
        arcGui.lbl_oppName = lblOppName;
        arcGui.lbl_resultTitle = lblResTitle;
        arcGui.lbl_resultScore = lblResScore;
        arcGui.lbl_resultDetail = lblResDetail;
        arcGui.lbl_resultElo = lblResElo;
        arcGui.lbl_rematchStatus = lblRematchStatus;
        arcGui.btn_rematch = arRematch;
        arcGui.lbl_inviteFrom = lblInvFrom;
        arcGui.lbl_inviteMode = lblInvMode;
        arcGui.pnl_roundOverlay = pnlRoundOvr;
        arcGui.lbl_roundResult = lblRoundRes;
        arcGui.lbl_roundAnswer = lblRoundAns;
        arcGui.lbl_resultAnswer = lblResAns;

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
        gui.gameBG = pnlMain.transform.Find("GameBG").GetComponent<Image>();
        gui.lbl_timer = lblTimer;
        gui.lbl_timeLabel = lblTimeLabel;
        gui.lbl_highscore = lblHS;
        gui.lbl_startHighscore = lblStartHS;
        gui.timerBarFill = barFill;
        gui.timerMaxTime = TIMER_MAX;

        // ── Login, Register, Settings panels (created after gui exists) ──
        LoginPanel(ct, gui);
        RegisterPanel(ct, gui);
        ForgotPasswordPanel(ct, gui);
        SettingsPanel(ct, gui);
        BuildLanguagePanel(ct, gui);

        // ── Progression screens: profile, leaderboard, daily ─────────────
        var prog = mgrs.AddComponent<ProgressionGUIManager>();
        ProfilePanel(ct, prog);
        LeaderboardPanel(ct, prog);
        DailyPanel(ct, prog);

        prog.lbl_menu_rank  = lblMenuRank;
        prog.lbl_menu_xp = lblMenuLevel;
        prog.lbl_arcade_xp = lblArcLevel;
        prog.badge_daily    = dailyBadge.gameObject;

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
            pnlStart.transform.Find("btn_settings/btn_face").GetComponent<Button>().onClick,
            gui.OnSettingsPressed);
        UnityEventTools.AddPersistentListener(
            pnlStart.transform.Find("btn_profile/btn_face").GetComponent<Button>().onClick,
            gui.OnProfilePressed);
        UnityEventTools.AddPersistentListener(
            pnlStart.transform.Find("btn_leaderboard/btn_face").GetComponent<Button>().onClick,
            gui.OnLeaderboardPressed);
        UnityEventTools.AddPersistentListener(
            pnlStart.transform.Find("btn_daily/btn_face").GetComponent<Button>().onClick,
            gui.OnDailyPressed);
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

        UnityEventTools.AddPersistentListener(
            btnSettingsKey.transform.Find("btn_face").GetComponent<Button>().onClick,
            gui.OnSettingsPressed);

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

        // Match length
        UnityEventTools.AddPersistentListener(
            abRounds1.transform.Find("btn_face").GetComponent<Button>().onClick,
            arcGui.OnSelectRounds1);
        UnityEventTools.AddPersistentListener(
            abRounds2.transform.Find("btn_face").GetComponent<Button>().onClick,
            arcGui.OnSelectRounds2);
        UnityEventTools.AddPersistentListener(
            abRounds3.transform.Find("btn_face").GetComponent<Button>().onClick,
            arcGui.OnSelectRounds3);

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

        // Out of coins

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

        // Audio controls draw above every panel; the scrim sits under the key
        // (which owns the popover as a child) and over everything else.
        volScrim.transform.SetAsLastSibling();
        volCtlGO.transform.SetAsLastSibling();

        volCtl.gameplayPanel = pnlMain;

        mgrs.AddComponent<MessengerCleaner>();

        // ── Safe area ────────────────────────────────────────────────────
        // The backdrop keeps bleeding to the physical edges; everything
        // interactive moves inside the notch-free region so no button or
        // label ends up under a camera cutout or the gesture bar.
        var safeGO = new GameObject("SafeArea");
        safeGO.transform.SetParent(ct, false);
        var safeRt = safeGO.AddComponent<RectTransform>();
        safeRt.anchorMin = Vector2.zero; safeRt.anchorMax = Vector2.one;
        safeRt.offsetMin = safeRt.offsetMax = Vector2.zero;
        safeGO.AddComponent<SafeAreaFitter>();

        var fullBleed = new System.Collections.Generic.HashSet<string>
            { "BG", "BG_Grade", "BG_Vignette", "SafeArea" };

        var canvasKids = new System.Collections.Generic.List<Transform>();
        for (int i = 0; i < ct.childCount; i++) canvasKids.Add(ct.GetChild(i));
        foreach (var kid in canvasKids)
            if (!fullBleed.Contains(kid.name))
                kid.SetParent(safeGO.transform, false);

        // Last, so it sees every label the build produced — including the
        // ones inside panels that are switched off.
        LocalizeAll(canvasGO);

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
    // Elevation and rim thickness, in reference pixels. Buttons read as
    // physical keys sitting above the page rather than outlines drawn on it.
    const float BTN_ELEV = 6f;
    const float BTN_RIM  = 3f;

    static GameObject SegButton(Transform parent, string name, string label,
        Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size, int fontSize,
        Color segColor)
    {
        var container = Panel(parent, name, aMin, aMax, pos, size);
        var ct = container.transform;

        // One corner radius across every regular button so neighbours match;
        // only the very small icon keys drop to a tighter corner, because a
        // 22px radius would eat a 40px control whole.
        Sprite shape = Mathf.Min(Mathf.Abs(size.x), Mathf.Abs(size.y)) < 60f
            ? RoundRectTight : RoundRectBtn;

        // Every surface is derived from the accent so the palette stays coherent
        Color fill   = new Color(segColor.r * 0.17f, segColor.g * 0.17f, segColor.b * 0.17f, 1f);
        Color shadow = new Color(segColor.r * 0.05f, segColor.g * 0.05f, segColor.b * 0.05f, 0.85f);

        // Shadow sits behind and below — this is what creates the depth
        var sh = Shape(ct, "btn_shadow", shape, V2(0,-BTN_ELEV), Vector2.zero, shadow);
        sh.raycastTarget = false;

        // Accent rim
        var rim = Shape(ct, "btn_rim", shape, Vector2.zero, Vector2.zero, segColor);
        rim.raycastTarget = false;

        // Face — inset by the rim width so the rim reads as a border.
        // This is the clickable surface and the Button's tint target, so the
        // press state is actually visible.
        var face = new GameObject("btn_face");
        face.transform.SetParent(ct, false);
        var faceRt = face.AddComponent<RectTransform>();
        faceRt.anchorMin = V2(0,0); faceRt.anchorMax = V2(1,1);
        faceRt.offsetMin = V2(BTN_RIM, BTN_RIM);
        faceRt.offsetMax = V2(-BTN_RIM, -BTN_RIM);

        var faceImg = face.AddComponent<Image>();
        faceImg.sprite = shape;
        faceImg.type = Image.Type.Sliced;
        faceImg.pixelsPerUnitMultiplier = 1f;
        faceImg.color = fill;

        var btn = face.AddComponent<Button>();
        btn.targetGraphic = faceImg;
        var bc = ColorBlock.defaultColorBlock;
        bc.normalColor      = Color.white;
        bc.highlightedColor = new Color(1.25f, 1.25f, 1.25f);
        bc.pressedColor     = new Color(0.5f, 0.5f, 0.5f);
        bc.disabledColor    = new Color(0.4f, 0.4f, 0.4f, 0.55f);
        bc.fadeDuration     = 0.07f;
        btn.colors = bc;

        // Gloss over the top half — the highlight that sells the curvature
        var gloss = Shape(face.transform, "btn_gloss", shape, Vector2.zero, Vector2.zero,
            new Color(segColor.r, segColor.g, segColor.b, 0.11f));
        gloss.rectTransform.anchorMin = V2(0, 0.48f);
        gloss.rectTransform.anchorMax = V2(1, 1f);
        gloss.rectTransform.offsetMin = V2(2, 0);
        gloss.rectTransform.offsetMax = V2(-2, -2);
        gloss.raycastTarget = false;

        // The caller's font size is the ceiling, not a suggestion that gets
        // thrown away: best-fit shrinks long labels instead of letting them
        // spill past the frame or wrap into a clipped second line.
        var lbl = DigTxt(face.transform, "lbl_btn", label,
            V2(0,0), V2(1,1), V2(0,0), V2(-28, -16), fontSize, segColor);
        lbl.raycastTarget = false;
        lbl.alignment = TextAnchor.MiddleCenter;
        lbl.horizontalOverflow = HorizontalWrapMode.Wrap;
        lbl.verticalOverflow = VerticalWrapMode.Truncate;
        lbl.resizeTextForBestFit = true;
        lbl.resizeTextMinSize = 8;
        lbl.resizeTextMaxSize = Mathf.Max(fontSize, 8);

        return container;
    }

    /// <summary>
    /// Lets a SegButton switch between a chosen and an unchosen look. Finds the
    /// pieces SegButton built rather than taking them as arguments, so the two
    /// cannot fall out of step.
    /// </summary>
    static SegButtonSelect Selectable(GameObject btn, Color accent)
    {
        var face = btn.transform.Find("btn_face");

        var sel = btn.AddComponent<SegButtonSelect>();
        sel.accent = accent;
        sel.rim   = btn.transform.Find("btn_rim").GetComponent<Image>();
        sel.face  = face.GetComponent<Image>();
        sel.gloss = face.Find("btn_gloss").GetComponent<Image>();
        sel.label = face.Find("lbl_btn").GetComponent<Text>();
        return sel;
    }

    /// <summary>
    /// Lets a label shrink to fit its box rather than wrap and lose the tail.
    /// Both copies of a glowing title must be given the same numbers or the
    /// glow and the word settle at different sizes.
    /// </summary>
    static Text Fit(Text t, int max, int min)
    {
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Truncate;
        t.resizeTextForBestFit = true;
        t.resizeTextMaxSize = max;
        t.resizeTextMinSize = min;
        return t;
    }

    /// <summary>
    /// Marks a graphic drawn over the game board so it re-tints when the board
    /// does. Derives the light-theme colour by darkening what it already has.
    /// </summary>
    static T Adapt<T>(T g) where T : Graphic
    {
        var a = g.gameObject.AddComponent<BgAdaptiveTint>();
        a.darkThemeColor = g.color;
        return g;
    }

    /// <summary>As Adapt, but the light-theme colour is given rather than derived.</summary>
    static T Adapt<T>(T g, Color onLight) where T : Graphic
    {
        var a = g.gameObject.AddComponent<BgAdaptiveTint>();
        a.darkThemeColor = g.color;
        a.overrideOnLight = true;
        a.lightThemeColor = onLight;
        return g;
    }

    /// <summary>
    /// Makes a ScrollRect behave like a list you can actually move.
    ///
    /// ScrollRect ships with scrollSensitivity 1, which moves the content by
    /// one unit per wheel notch. On a list nearly two thousand pixels long
    /// that is indistinguishable from not scrolling at all — and the wheel is
    /// how it gets tested, in the editor, long before a finger touches it.
    /// </summary>
    static ScrollRect TuneScroll(ScrollRect sr)
    {
        sr.scrollSensitivity = 60f;

        // With no viewport ScrollRect falls back to its own rect. That mostly
        // works, until a ContentSizeFitter resizes the content mid-layout and
        // it measures against the wrong thing. Being explicit costs nothing.
        if (sr.viewport == null) sr.viewport = sr.GetComponent<RectTransform>();

        // A list has ends. Rubber-banding past them suits a page you are
        // reading, not a menu you are trying to pick a row out of.
        sr.movementType = ScrollRect.MovementType.Clamped;
        sr.inertia = true;
        sr.decelerationRate = 0.135f;

        return sr;
    }

    /// <summary>
    /// One centred row of colour swatches. colorType is 0 for segments, 1 for
    /// the background — the same code SettingsColorPicker reads back.
    /// </summary>
    static void SwatchRow(Transform parent, string namePrefix, int colorType,
                          Color[] colors, string[] names, float y, float size, bool showEdge)
    {
        const float GAP = 16f;

        float totalW = colors.Length * size + (colors.Length - 1) * GAP;
        float startX = -totalW * 0.5f + size * 0.5f;

        for (int i = 0; i < colors.Length; i++)
        {
            var swatch = new GameObject(namePrefix + "_" + i);
            swatch.transform.SetParent(parent, false);

            var rt = swatch.AddComponent<RectTransform>();
            rt.anchorMin = V2(.5f,1); rt.anchorMax = V2(.5f,1);
            rt.anchoredPosition = V2(startX + i * (size + GAP), y);
            rt.sizeDelta = V2(size, size);

            var ring = SwatchRing(swatch.transform);

            var img = swatch.AddComponent<Image>();
            img.sprite = RoundRect;
            img.type = Image.Type.Sliced;
            img.color = colors[i];

            // Black on a near-black panel has no silhouette of its own, so the
            // background swatches get a hairline to sit inside.
            if (showEdge)
            {
                var edge = Shape(swatch.transform, "swatch_edge", RoundRect, Vector2.zero, V2(3,3), Hex("#FFFFFF33"));
                edge.raycastTarget = false;
                edge.transform.SetAsFirstSibling();
            }

            var btn = swatch.AddComponent<Button>();
            btn.targetGraphic = img;

            DigTxt(swatch.transform, "lbl", names[i],
                V2(0,0), V2(1,0), V2(0,-8), V2(0,20), 11, Color.white).raycastTarget = false;

            var picker = swatch.AddComponent<SettingsColorPicker>();
            picker.colorType = colorType;
            picker.colorIndex = i;
            picker.ring = ring;
        }
    }

    /// <summary>Frame behind a colour swatch, shown only while it is the chosen one.</summary>
    static Image SwatchRing(Transform swatch)
    {
        var ring = Shape(swatch, "swatch_ring", RoundRect, Vector2.zero, V2(12, 12), ACCENT);
        ring.raycastTarget = false;
        ring.enabled = false;          // SettingsColorPicker turns it on
        return ring;
    }

    /// <summary>Sliced rounded image stretched to its parent, offset by pos.</summary>
    static Image Shape(Transform p, string n, Sprite sprite, Vector2 pos, Vector2 sizeDelta, Color c)
    {
        var go = new GameObject(n);
        go.transform.SetParent(p, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = V2(0,0); rt.anchorMax = V2(1,1);
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        rt.anchoredPosition = pos;
        rt.sizeDelta = sizeDelta;
        var img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = 1f;
        img.color = c;
        return img;
    }

    // Convenience overloads
    static GameObject SegButton(Transform parent, string name, string label,
        Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size, int fontSize)
    {
        return SegButton(parent, name, label, aMin, aMax, pos, size, fontSize, ACCENT);
    }

    // Kept for the continue-panel call sites. Now just the standard button so
    // the whole game shares one look; topColor carries the accent.
    static GameObject VolumetricButton(Transform parent, string name, string label,
        Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size, int fontSize,
        Color faceColor, Color topColor, Color shadowColor)
    {
        return SegButton(parent, name, label, aMin, aMax, pos, size, fontSize, topColor);
    }

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
        float gap = 10f;
        var go = Panel(p, name, V2(.5f,.5f), V2(.5f,.5f), pos, V2(DW*2+gap*2, DH));
        var n  = go.AddComponent<Number>();
        n.FirstDigit  = Digit(go.transform, "DigitA", V2(-(DW/2+gap), 0));
        n.SecondDigit = Digit(go.transform, "DigitB", V2(+(DW/2+gap), 0));
        return n;
    }

    static Number NumberGroup3(Transform p, string name, Vector2 pos)
    {
        float gap = 10f;
        float spacing = DW + gap;
        var go = Panel(p, name, V2(.5f,.5f), V2(.5f,.5f), pos, V2(DW*3+gap*4, DH));
        var n  = go.AddComponent<Number>();
        n.ThirdDigit  = Digit(go.transform, "DigitH", V2(-spacing, 0));
        n.FirstDigit  = Digit(go.transform, "DigitA", V2(0, 0));
        n.SecondDigit = Digit(go.transform, "DigitB", V2(+spacing, 0));
        return n;
    }

    // Which of the seven segments are lit for each numeral, in the order the
    // Digit builder places them: Top, Middle, Bottom, TopLeft, TopRight,
    // BottomLeft, BottomRight.
    static readonly bool[][] DIGIT_SEGMENTS =
    {
        //          Top    Mid    Bot    TL     TR     BL     BR
        new[] {     true,  false, true,  true,  true,  true,  true  }, // 0
        new[] {     false, false, false, false, true,  false, true  }, // 1
        new[] {     true,  true,  true,  false, true,  true,  false }, // 2
        new[] {     true,  true,  true,  false, true,  false, true  }, // 3
        new[] {     false, true,  false, true,  true,  false, true  }, // 4
        new[] {     true,  true,  true,  true,  false, false, true  }, // 5
        new[] {     true,  true,  true,  true,  false, true,  true  }, // 6
        new[] {     true,  false, false, false, true,  false, true  }, // 7
        new[] {     true,  true,  true,  true,  true,  true,  true  }, // 8
        new[] {     true,  true,  true,  true,  true,  false, true  }, // 9
    };

    /// <summary>
    /// A non-interactive seven-segment numeral for the menu legend.
    ///
    /// Deliberately draws the UNLIT segments too, in the same dim colour the
    /// game uses: the whole point is to show that every digit lives inside the
    /// same eight-shaped housing and differs only by which sticks are lit.
    /// A picture of just the lit sticks would teach the shapes but not the rule.
    /// </summary>
    static void LegendDigit(Transform parent, int value, Vector2 pos, float scale)
    {
        var go = Panel(parent, "legend_" + value, V2(.5f,1), V2(.5f,1), pos, V2(DW * scale, DH * scale));
        var t  = go.transform;

        var lit = DIGIT_SEGMENTS[value];

        float hw = SHW * scale, hh = SHH * scale;
        float vw = SVW * scale, vh = SVH * scale;
        float sx = SX * scale,  syt = SYT * scale, syv = SYV * scale;

        var placements = new[]
        {
            new { n = "Top",         p = V2(  0f,  syt), s = V2(hw, hh) },
            new { n = "Middle",      p = V2(  0f,   0f), s = V2(hw, hh) },
            new { n = "Bottom",      p = V2(  0f, -syt), s = V2(hw, hh) },
            new { n = "TopLeft",     p = V2(-sx,   syv), s = V2(vw, vh) },
            new { n = "TopRight",    p = V2( sx,   syv), s = V2(vw, vh) },
            new { n = "BottomLeft",  p = V2(-sx,  -syv), s = V2(vw, vh) },
            new { n = "BottomRight", p = V2( sx,  -syv), s = V2(vw, vh) },
        };

        for (int i = 0; i < placements.Length; i++)
        {
            var seg = Img(t, placements[i].n, V2(.5f,.5f), V2(.5f,.5f),
                placements[i].p, placements[i].s, lit[i] ? ACCENT : SEG_OFF);
            seg.sprite = placements[i].s.x > placements[i].s.y ? HSeg : VSeg;
            seg.raycastTarget = false;
        }

        // The numeral underneath, so the shape reads as an answer not a puzzle
        Txt(t, "lbl_value", value.ToString(), V2(.5f,0), V2(.5f,0),
            V2(0, -22f), V2(DW * scale, 30f), 22, TEXT_DIM,
            TextAnchor.MiddleCenter, FontStyle.Bold).raycastTarget = false;
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
        // A key with a symbol on it. The frame is what turns two crossing bars
        // into one control: 170px, one tap, and no way to land in a state that
        // means neither plus nor minus.
        var go = Panel(p, "PlusMinus", V2(.5f,.5f), V2(.5f,.5f), pos, V2(170, 170));
        var ct = go.transform;

        var pm = go.AddComponent<PlusMinus>();

        var shadow = Img(ct, "pm_shadow", V2(.5f,.5f), V2(.5f,.5f), V2(0,-BTN_ELEV), V2(170,170), Hex("#0A1400D9"));
        shadow.sprite = RoundRectBtn; shadow.type = Image.Type.Sliced;
        shadow.pixelsPerUnitMultiplier = 1f; shadow.raycastTarget = false;

        var rim = Img(ct, "pm_rim", V2(.5f,.5f), V2(.5f,.5f), V2(0,0), V2(170,170), ACCENT_DARK);
        rim.sprite = RoundRectBtn; rim.type = Image.Type.Sliced;
        rim.pixelsPerUnitMultiplier = 1f; rim.raycastTarget = false;

        // The whole face takes the tap — there is nothing smaller to aim at
        var faceGO = new GameObject("btn_face");
        faceGO.transform.SetParent(ct, false);
        var faceRt = faceGO.AddComponent<RectTransform>();
        faceRt.anchorMin = faceRt.anchorMax = V2(.5f,.5f);
        faceRt.sizeDelta = V2(164, 164);
        var faceImg = faceGO.AddComponent<Image>();
        faceImg.sprite = RoundRectBtn; faceImg.type = Image.Type.Sliced;
        faceImg.pixelsPerUnitMultiplier = 1f;
        faceImg.color = Hex("#101A10");

        var btn = faceGO.AddComponent<Button>();
        btn.targetGraphic = faceImg;
        var bc = ColorBlock.defaultColorBlock;
        bc.normalColor = Color.white;
        bc.highlightedColor = new Color(1.25f, 1.25f, 1.25f);
        bc.pressedColor = new Color(0.5f, 0.5f, 0.5f);
        bc.fadeDuration = 0.07f;
        btn.colors = bc;
        UnityEventTools.AddPersistentListener(btn.onClick, pm.Toggle);

        // Bars stay Line components so they keep the segment palette, but the
        // key owns the raycast now.
        pm.line2 = Seg(ct, "VBar", V2(0,0), V2(26, 104), Vector4.zero);
        pm.line1 = Seg(ct, "HBar", V2(0,0), V2(104, 26), Vector4.zero);
        pm.line2.GetComponent<Image>().raycastTarget = false;
        pm.line1.GetComponent<Image>().raycastTarget = false;

        return pm;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Helpers
    // ═══════════════════════════════════════════════════════════════════════

    static Line Seg(Transform p, string n, Vector2 pos, Vector2 sz)
    {
        return Seg(p, n, pos, sz, new Vector4(-20, -20, -20, -20));
    }

    // Negative padding EXPANDS the hit rect. The default grows a segment on all
    // four sides, which is right for digits but wrong for the +/- toggle, where
    // the two bars cross and would swallow each other's arms.
    static Line Seg(Transform p, string n, Vector2 pos, Vector2 sz, Vector4 hitPadding)
    {
        var img = Img(p, n, V2(.5f,.5f), V2(.5f,.5f), pos, sz, SEG_OFF);
        img.raycastTarget = true;
        img.raycastPadding = hitPadding;
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

    static Sprite s_gear;
    static Sprite GearSprite => s_gear != null ? s_gear : (s_gear = MakeGearSprite(128));

    static GameObject BackArrowButton(Transform parent, string name,
        Vector2 aMin, Vector2 aMax, Vector2 pos, float size, Color color)
    {
        return RoundIconButton(parent, name, aMin, aMax, pos, size, color, BackArrowSprite);
    }

    static GameObject GearButton(Transform parent, string name,
        Vector2 aMin, Vector2 aMax, Vector2 pos, float size, Color color)
    {
        return RoundIconButton(parent, name, aMin, aMax, pos, size, color, GearSprite);
    }

    /// <summary>A circular key with an icon on its face.</summary>
    static GameObject RoundIconButton(Transform parent, string name,
        Vector2 aMin, Vector2 aMax, Vector2 pos, float size, Color color, Sprite icon)
    {
        var go = Panel(parent, name, aMin, aMax, pos, V2(size, size));
        var ct = go.transform;

        // Circular key behind the chevron, matching the elevation of SegButton
        var bsh = Img(ct, "back_shadow", V2(0,0), V2(1,1), V2(0,-4), V2(0,0),
            new Color(color.r * 0.05f, color.g * 0.05f, color.b * 0.05f, 0.85f));
        bsh.sprite = Circle; bsh.raycastTarget = false;

        var brim = Img(ct, "back_rim", V2(0,0), V2(1,1), V2(0,0), V2(0,0), color);
        brim.sprite = Circle; brim.raycastTarget = false;

        var bfill = Img(ct, "back_fill", V2(0,0), V2(1,1), V2(0,0), V2(-6,-6),
            new Color(color.r * 0.17f, color.g * 0.17f, color.b * 0.17f, 1f));
        bfill.sprite = Circle; bfill.raycastTarget = false;

        var face = new GameObject("btn_face");
        face.transform.SetParent(ct, false);
        var faceRt = face.AddComponent<RectTransform>();
        faceRt.anchorMin = V2(0,0); faceRt.anchorMax = V2(1,1);
        faceRt.offsetMin = V2(14,14); faceRt.offsetMax = V2(-14,-14);
        var faceImg = face.AddComponent<Image>();
        faceImg.sprite = icon;
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

    /// <summary>
    /// A gear: a hub, a body, and eight teeth. Drawn rather than shipped as a
    /// PNG so it scales with the key and takes its colour from the palette
    /// like every other surface here.
    /// </summary>
    static Sprite MakeGearSprite(int s)
    {
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        var clear = new Color(0, 0, 0, 0);

        const int TEETH = 8;
        const float TOOTH_SHARE = 0.46f;   // of each tooth pitch that is tooth

        float cx = s * 0.5f, cy = s * 0.5f;
        float rHub  = s * 0.15f;   // the hole through the middle
        float rBody = s * 0.32f;   // between the teeth
        float rTip  = s * 0.46f;   // tooth tip

        for (int y = 0; y < s; y++)
        for (int x = 0; x < s; x++)
        {
            float dx = x - cx + 0.5f;
            float dy = y - cy + 0.5f;
            float dist = Mathf.Sqrt(dx * dx + dy * dy);

            // Position within this tooth's pitch: 0 at a tooth centre, 1 at a gap
            float turn = Mathf.Atan2(dy, dx) / (2f * Mathf.PI) + 0.5f;
            float phase = turn * TEETH;
            float frac = phase - Mathf.Floor(phase);
            float away = Mathf.Abs(frac - 0.5f) * 2f;

            // Softened across the flank so the teeth are not stair-stepped
            float onTooth = Mathf.Clamp01(Mathf.InverseLerp(TOOTH_SHARE + 0.10f, TOOTH_SHARE - 0.10f, away));
            float outer = Mathf.Lerp(rBody, rTip, onTooth);

            float alpha = Mathf.Min(Mathf.Clamp01(outer - dist + 1f),
                                    Mathf.Clamp01(dist - rHub + 1f));

            tex.SetPixel(x, y, alpha > 0f ? new Color(1, 1, 1, alpha) : clear);
        }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f);
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

    // Top-to-bottom lift: brighter at the top of the screen, sinking to black.
    // Soft-edged filled dot — the core of a tap ripple. Alpha falls off over the
    // outer third so it reads as a glow rather than a hard disc.
    // Speaker cone only — no waves, no slash, no disc. The key supplies the
    // backing and the cell column carries the level, so this sprite is pure
    // identity. Coverage is computed rather than SetPixel on/off; the old
    // hard stair-stepped diagonal is half of why the icon looked cheap.
    static Sprite MakeSpeakerCone(int s)
    {
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        float k = s / 96f;
        float bodyL = 22f * k, bodyR = 40f * k, coneR = 70f * k;
        float bodyHalf = 10f * k, mouthHalf = 30f * k;
        float cy = s * 0.5f;

        for (int y = 0; y < s; y++)
        for (int x = 0; x < s; x++)
        {
            float px = x + 0.5f, py = y + 0.5f;
            float cov = 0f;

            // 2x2 supersample gives a smooth edge without a blur pass
            for (int sy = 0; sy < 2; sy++)
            for (int sx = 0; sx < 2; sx++)
            {
                float ax = px + (sx - 0.5f) * 0.5f;
                float ay = py + (sy - 0.5f) * 0.5f;
                float dy = Mathf.Abs(ay - cy);

                bool inside;
                if (ax >= bodyL && ax <= bodyR) inside = dy <= bodyHalf;
                else if (ax > bodyR && ax <= coneR)
                {
                    float t = (ax - bodyR) / (coneR - bodyR);
                    inside = dy <= Mathf.Lerp(bodyHalf, mouthHalf, t);
                }
                else inside = false;

                if (inside) cov += 0.25f;
            }

            tex.SetPixel(x, y, new Color(1f, 1f, 1f, cov));
        }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f));
    }

    // Round-capped diagonal bar. Distance to the segment gives both the cap
    // shape and the antialiased edge for free.
    static Sprite MakeSlash(int s)
    {
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        float inset = s * 0.14f;
        float half = s * 0.09f * 0.5f;
        Vector2 a = new Vector2(inset, s - inset);
        Vector2 b = new Vector2(s - inset, inset);
        Vector2 ab = b - a;
        float abLen2 = ab.sqrMagnitude;

        for (int y = 0; y < s; y++)
        for (int x = 0; x < s; x++)
        {
            Vector2 pt = new Vector2(x + 0.5f, y + 0.5f);
            float t = Mathf.Clamp01(Vector2.Dot(pt - a, ab) / abLen2);
            float d = Vector2.Distance(pt, a + ab * t);

            float cov = Mathf.Clamp01(half - d + 0.5f);
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, cov));
        }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f));
    }

    static Sprite MakeSoftDot(int s)
    {
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        float half = s / 2f;
        for (int y = 0; y < s; y++)
        for (int x = 0; x < s; x++)
        {
            float d = Mathf.Sqrt((x - half + 0.5f) * (x - half + 0.5f)
                               + (y - half + 0.5f) * (y - half + 0.5f)) / half;

            float a = d >= 1f ? 0f : Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(0.62f, 1f, d));
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
        }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f));
    }

    // Hollow ring with antialiased edges, for the expanding wave of a tap ripple.
    // thickness is a fraction of the radius.
    static Sprite MakeRing(int s, float thickness)
    {
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        float half = s / 2f;
        float outer = 1f;
        float inner = 1f - thickness * 2f;
        float feather = 2f / half;

        for (int y = 0; y < s; y++)
        for (int x = 0; x < s; x++)
        {
            float d = Mathf.Sqrt((x - half + 0.5f) * (x - half + 0.5f)
                               + (y - half + 0.5f) * (y - half + 0.5f)) / half;

            float a = Mathf.Clamp01((outer - d) / feather)
                    * Mathf.Clamp01((d - inner) / feather);

            tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(a)));
        }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f));
    }

    static Sprite MakeVerticalGrade(int w, int h)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        for (int y = 0; y < h; y++)
        {
            float t = (float)y / (h - 1);          // 0 = bottom, 1 = top
            float a = Mathf.Lerp(0.55f, 0f, t);     // darkest at the bottom
            for (int x = 0; x < w; x++)
                tex.SetPixel(x, y, new Color(0f, 0.02f, 0f, a));
        }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
    }

    // Corner falloff so the frame darkens toward the edges of the device.
    static Sprite MakeVignette(int s)
    {
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        float half = s / 2f;
        for (int y = 0; y < s; y++)
        for (int x = 0; x < s; x++)
        {
            float dx = (x - half) / half;
            float dy = (y - half) / half;
            float d = Mathf.Sqrt(dx * dx + dy * dy) / 1.41421f;

            // Flat through the middle, ramping up only near the corners
            float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 1f, d)) * 0.72f;
            tex.SetPixel(x, y, new Color(0f, 0f, 0f, a));
        }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f));
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
        pnlLogin.AddComponent<ScrollablePanel>().referenceHeight = 1920f;
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
        emailRt.anchorMin = V2(.5f,1); emailRt.anchorMax = V2(.5f,1);
        emailRt.anchoredPosition = V2(0, -840);
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
        passRt.anchorMin = V2(.5f,1); passRt.anchorMax = V2(.5f,1);
        passRt.anchoredPosition = V2(0, -950);
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
        gui.lbl_login_error = Txt(lt, "lbl_error", "", V2(.5f,1), V2(.5f,1), V2(0,-1020), V2(700, 40), 20, Hex("#FF6666"),
            TextAnchor.MiddleCenter, FontStyle.Normal);
        gui.lbl_login_error.raycastTarget = false;

        // LOGIN button — below inputs
        var btnLogin = SegButton(lt, "btn_login", "LOGIN", V2(.5f,1), V2(.5f,1), V2(0,-1090),
            V2(500, 100), 38, ACCENT);
        UnityEventTools.AddPersistentListener(
            btnLogin.transform.Find("btn_face").GetComponent<Button>().onClick,
            gui.OnLoginPressed);

        // Forgot password link
        var btnForgot = new GameObject("btn_forgot");
        btnForgot.transform.SetParent(lt, false);
        var btnForgotRt = btnForgot.AddComponent<RectTransform>();
        btnForgotRt.anchorMin = V2(.5f,1); btnForgotRt.anchorMax = V2(.5f,1);
        btnForgotRt.anchoredPosition = V2(0, -1189);
        btnForgotRt.sizeDelta = V2(400, 44);
        var btnForgotImg = btnForgot.AddComponent<Image>();
        btnForgotImg.color = Color.clear;
        var btnForgotBtn = btnForgot.AddComponent<Button>();
        btnForgotBtn.targetGraphic = btnForgotImg;
        DigTxt(btnForgot.transform, "Text", "FORGOT PASSWORD?", V2(0,0), V2(1,1), V2(0,0), V2(0,0), 20, ACCENT_DARK);
        UnityEventTools.AddPersistentListener(btnForgotBtn.onClick, gui.OnShowForgotPasswordPanel);

        // ── PLAY AS GUEST — most prominent, right after login ─────────
        var btnGuest = SegButton(lt, "btn_guest", "PLAY AS GUEST", V2(.5f,1), V2(.5f,1), V2(0, -1277),
            V2(500, 90), 30, Hex("#00E5FF"));
        UnityEventTools.AddPersistentListener(
            btnGuest.transform.Find("btn_face").GetComponent<Button>().onClick,
            gui.OnGuestLoginPressed);
        DigTxt(lt, "lbl_guest_desc", "NO ACCOUNT NEEDED", V2(.5f,1), V2(.5f,1),
            V2(0, -1349), V2(880, 21), 14, Hex("#00ACC1")).raycastTarget = false;

        // ── OR divider ──────────────────────────────────────────────────
        Img(lt, "DivL", V2(.5f,1), V2(.5f,1), V2(-160, -1399), V2(200, 2), Hex("#334155")).raycastTarget = false;
        DigTxt(lt, "lbl_or", "OR", V2(.5f,1), V2(.5f,1), V2(0, -1399), V2(60, 30), 18, TEXT_MUTED).raycastTarget = false;
        Img(lt, "DivR", V2(.5f,1), V2(.5f,1), V2(160, -1399), V2(200, 2), Hex("#334155")).raycastTarget = false;

        // ── Google login button (white bg, Google colors) ───────────────
        SocialLoginButton(lt, "btn_google", "SIGN IN WITH GOOGLE", V2(0, -1477),
            Color.white, Hex("#333333"), MakeGoogleIcon(), gui.OnGoogleLoginPressed);

        // ── Facebook login button (blue bg, white text) ─────────────────
        SocialLoginButton(lt, "btn_facebook", "SIGN IN WITH FACEBOOK", V2(0, -1582),
            Hex("#1877F2"), Color.white, MakeFacebookIcon(), gui.OnFacebookLoginPressed);

        // SIGN UP link — bottom
        var btnReg = new GameObject("btn_register");
        btnReg.transform.SetParent(lt, false);
        var btnRegRt = btnReg.AddComponent<RectTransform>();
        btnRegRt.anchorMin = V2(.5f,1); btnRegRt.anchorMax = V2(.5f,1);
        btnRegRt.anchoredPosition = V2(0, -1790);
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

        DigTxt(ft, "lbl_forgot_desc1", "ENTER YOUR EMAIL", V2(.5f,1), V2(.5f,1), V2(0,-785), V2(900,24), 16, TEXT_MUTED).raycastTarget = false;
        DigTxt(ft, "lbl_forgot_desc2", "TO RESET PASSWORD", V2(.5f,1), V2(.5f,1), V2(0,-815), V2(900,24), 16, TEXT_MUTED).raycastTarget = false;

        // Email input
        var fEmailGO = new GameObject("inp_forgot_email");
        fEmailGO.transform.SetParent(ft, false);
        var fEmailRt = fEmailGO.AddComponent<RectTransform>();
        fEmailRt.anchorMin = V2(.5f,1); fEmailRt.anchorMax = V2(.5f,1);
        fEmailRt.anchoredPosition = V2(0, -900);
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
        gui.lbl_forgot_status = Txt(ft, "lbl_forgot_status", "", V2(.5f,1), V2(.5f,1), V2(0,-990), V2(700, 80), 20, ACCENT,
            TextAnchor.MiddleCenter, FontStyle.Normal);
        gui.lbl_forgot_status.raycastTarget = false;
        gui.lbl_forgot_status.verticalOverflow = VerticalWrapMode.Overflow;

        // SEND RESET EMAIL button
        var btnSend = SegButton(ft, "btn_send_reset", "RESET PASSWORD", V2(.5f,1), V2(.5f,1), V2(0,-1100),
            V2(600, 100), 30, ACCENT);
        UnityEventTools.AddPersistentListener(
            btnSend.transform.Find("btn_face").GetComponent<Button>().onClick,
            gui.OnSendResetEmailPressed);

        // BACK TO LOGIN
        var btnBack = BackArrowButton(ft, "btn_forgot_back", V2(0,1), V2(0,1), V2(70, -70), 80, ACCENT_DARK);
        UnityEventTools.AddPersistentListener(
            btnBack.transform.Find("btn_face").GetComponent<Button>().onClick,
            gui.OnBackToLoginPressed);

        pnlForgot.SetActive(false);
    }

    // ── Register Panel ─────────────────────────────────────────────────────
    static void RegisterPanel(Transform parent, GUIManager gui)
    {
        var pnlReg = Panel(parent, "pnl_register", V2(0,0), V2(1,1), V2(0,0), V2(0,0));
        pnlReg.AddComponent<ScrollablePanel>().referenceHeight = 1920f;
        var rt = pnlReg.transform;
        gui.pnl_register = pnlReg;

        // Full-screen dark background
        RoundImg(rt, "RegBG", V2(0,0), V2(1,1), V2(0,0), V2(0,0), Hex("#0A0E1AFA")).raycastTarget = false;

        // Title
        DigTxt(rt, "lbl_title", "CREATE ACCOUNT", V2(.5f,1), V2(.5f,1), V2(0,-270), V2(900,57), 38, ACCENT).raycastTarget = false;
        RoundImg(rt, "RegAccent", V2(.5f,1), V2(.5f,1), V2(0,-315), V2(120,3), ACCENT).raycastTarget = false;

        // Top-anchored so the four fields keep a fixed distance from the
        // header instead of sliding 100px away from it on a tall phone.
        // -840 is the old centre value 120 converted against the 1920 design.
        float startY = -840f;
        float spacing = 110f;

        // Username
        var nameGO = new GameObject("inp_name");
        nameGO.transform.SetParent(rt, false);
        var nameRt = nameGO.AddComponent<RectTransform>();
        nameRt.anchorMin = V2(.5f,1); nameRt.anchorMax = V2(.5f,1);
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
        emailRt.anchorMin = V2(.5f,1); emailRt.anchorMax = V2(.5f,1);
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
        passRt.anchorMin = V2(.5f,1); passRt.anchorMax = V2(.5f,1);
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
        confRt.anchorMin = V2(.5f,1); confRt.anchorMax = V2(.5f,1);
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
        var errorLbl = Txt(rt, "lbl_error", "", V2(.5f,1), V2(.5f,1), V2(0, -1255), V2(700, 40), 20, Hex("#FF6666"),
            TextAnchor.MiddleCenter, FontStyle.Normal);
        errorLbl.raycastTarget = false;
        gui.lbl_login_error = errorLbl;

        // SIGN UP button
        var btnReg = SegButton(rt, "btn_register", "SIGN UP", V2(.5f,1), V2(.5f,1),
            V2(0, -1340), V2(600, 100), 32, ACCENT);
        UnityEventTools.AddPersistentListener(
            btnReg.transform.Find("btn_face").GetComponent<Button>().onClick,
            gui.OnRegisterPressed);

        // Back arrow — bottom left
        var btnBack = BackArrowButton(rt, "btn_back", V2(0,1), V2(0,1), V2(70, -70), 80, ACCENT_DARK);
        UnityEventTools.AddPersistentListener(
            btnBack.transform.Find("btn_face").GetComponent<Button>().onClick,
            gui.OnBackToLoginPressed);
    }

    // ── Settings Panel ───────────────────────────────────────────────────
    static void SettingsPanel(Transform parent, GUIManager gui)
    {
        var pnl = Panel(parent, "pnl_settings", V2(0,0), V2(1,1), V2(0,0), V2(0,0));
        pnl.AddComponent<ScrollablePanel>().referenceHeight = 1920f;
        var st = pnl.transform;
        gui.pnl_settings = pnl;

        RoundImg(st, "SettingsBG", V2(0,0), V2(1,1), V2(0,0), V2(0,0), Hex("#0A0E1AFA")).raycastTarget = false;

        DigTxt(st, "lbl_title", "SETTINGS", V2(.5f,1), V2(.5f,1), V2(0,-120), V2(900,57), 42, ACCENT).raycastTarget = false;
        RoundImg(st, "SettingsAccent", V2(.5f,1), V2(.5f,1), V2(0,-170), V2(120,3), ACCENT).raycastTarget = false;

        // ── SEGMENT COLOR section ────────────────────────────────────
        // Both palettes are one row now, sized from the array rather than a
        // hardcoded 8, so changing the palette cannot leave a gap or an
        // IndexOutOfRange behind.
        DigTxt(st, "lbl_seg_title", "SEGMENT COLOR", V2(.5f,1), V2(.5f,1), V2(0,-230), V2(900,36), 24, TEXT_PRIMARY).raycastTarget = false;

        SwatchRow(st, "swatch_seg", 0, GameSettings.SegmentColors,
                  GameSettings.SegmentColorNames, -310f, 80f, false);

        // ── BACKGROUND COLOR section ─────────────────────────────────
        DigTxt(st, "lbl_bg_title", "BACKGROUND COLOR", V2(.5f,1), V2(.5f,1), V2(0,-430), V2(900,36), 24, TEXT_PRIMARY).raycastTarget = false;

        SwatchRow(st, "swatch_bg", 1, GameSettings.BackgroundColors,
                  GameSettings.BackgroundColorNames, -510f, 96f, true);

        // ── AUDIO section ────────────────────────────────────────────
        // The corner key rides the master level, which is the situational
        // control. Music vs effects is a set-once balance, so it lives here —
        // and a drag is fine on a settings screen in a way it is not in the
        // top corner mid-game.
        //
        // pnl_settings carries ScrollablePanel, so every child must be TOP
        // anchored; anything centred would resolve against the fixed 1920
        // content box instead of the screen.
        DigTxt(st, "lbl_audio_title", "AUDIO", V2(.5f,1), V2(.5f,1), V2(0,-640), V2(900,36), 24, TEXT_PRIMARY)
            .raycastTarget = false;

        AudioSliderRow(st, "row_music", "MUSIC", -710, AudioSliderBinder.Channel.Music);
        AudioSliderRow(st, "row_sfx",   "EFFECTS", -800, AudioSliderBinder.Channel.SFX);

        // ── LANGUAGE section ─────────────────────────────────────────
        DigTxt(st, "lbl_language_title", "LANGUAGE", V2(.5f,1), V2(.5f,1), V2(0,-900), V2(900,36), 24, TEXT_PRIMARY)
            .raycastTarget = false;

        var btnLang = SegButton(st, "btn_language", "English", V2(.5f,1), V2(.5f,1), V2(0,-975),
            V2(460, 78), 26, ACCENT);
        btnLang.transform.Find("btn_face/lbl_btn").gameObject.AddComponent<LanguageLabel>();
        UnityEventTools.AddPersistentListener(
            btnLang.transform.Find("btn_face").GetComponent<Button>().onClick,
            gui.OnLanguagePressed);

        // Back arrow
        var btnBack = BackArrowButton(st, "btn_settings_back", V2(0,1), V2(0,1), V2(70, -70), 80, ACCENT_DARK);
        UnityEventTools.AddPersistentListener(
            btnBack.transform.Find("btn_face").GetComponent<Button>().onClick,
            gui.OnSettingsBackPressed);

        pnl.SetActive(false);
    }

    /// <summary>
    /// The language list. Twenty rows do not belong under the audio sliders,
    /// so this is its own screen — and every row names its language in that
    /// language, because a list that reads "German" only once you are already
    /// reading German cannot rescue someone who picked the wrong one.
    /// </summary>
    static void BuildLanguagePanel(Transform parent, GUIManager gui)
    {
        var pnl = Panel(parent, "pnl_language", V2(0,0), V2(1,1), V2(0,0), V2(0,0));
        var lt = pnl.transform;
        gui.pnl_language = pnl;

        RoundImg(lt, "LangBG", V2(0,0), V2(1,1), V2(0,0), V2(0,0), Hex("#0A0E1AFA")).raycastTarget = false;

        DigTxt(lt, "lbl_lang_title", "LANGUAGE", V2(.5f,1), V2(.5f,1), V2(0,-120), V2(900,57), 42, ACCENT).raycastTarget = false;
        RoundImg(lt, "LangAccent", V2(.5f,1), V2(.5f,1), V2(0,-170), V2(120,3), ACCENT).raycastTarget = false;

        var scrollGO = new GameObject("LangScrollView");
        scrollGO.transform.SetParent(lt, false);
        var scrollRt = scrollGO.AddComponent<RectTransform>();
        scrollRt.anchorMin = V2(0,0); scrollRt.anchorMax = V2(1,1);
        scrollRt.offsetMin = V2(60, 80); scrollRt.offsetMax = V2(-60, -220);
        scrollGO.AddComponent<Image>().color = new Color(0,0,0,0.01f);

        var scroll = TuneScroll(scrollGO.AddComponent<ScrollRect>());
        scroll.horizontal = false;

        // RectMask2D rather than Mask: it clips by rectangle instead of by
        // stencil, so it needs no second draw of the graphic and does not
        // spend a stencil bit the rest of the UI might want.
        scrollGO.AddComponent<RectMask2D>();

        var contentGO = new GameObject("Content");
        contentGO.transform.SetParent(scrollGO.transform, false);
        var contentRt = contentGO.AddComponent<RectTransform>();
        contentRt.anchorMin = V2(0,1); contentRt.anchorMax = V2(1,1);
        contentRt.pivot = V2(0.5f,1); contentRt.sizeDelta = V2(0,0);

        var vlg = contentGO.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 10;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;

        // Twenty rows very nearly fill the panel, so without this the last one
        // sits flush against the bottom edge and reads as the end of the list
        // rather than the end of the visible part of it.
        vlg.padding = new RectOffset(6, 6, 4, 40);

        var csf = contentGO.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.content = contentRt;

        for (int i = 0; i < Loc.Languages.Length; i++)
        {
            var row = SegButton(contentGO.transform, "row_lang_" + Loc.Languages[i].code,
                Loc.Languages[i].nativeName, V2(0,1), V2(1,1), V2(0,0), V2(0, 82), 26, ACCENT);

            // SegButton already anchored it across the row at a fixed height;
            // the layout group drives the rest.
            row.AddComponent<LayoutElement>().preferredHeight = 82;

            var picker = row.AddComponent<LanguagePicker>();
            picker.languageIndex = i;
            picker.ring = SwatchRing(row.transform);
        }

        var back = BackArrowButton(lt, "btn_lang_back", V2(0,1), V2(0,1), V2(70, -70), 80, ACCENT_DARK);
        UnityEventTools.AddPersistentListener(
            back.transform.Find("btn_face").GetComponent<Button>().onClick,
            gui.OnLanguageBackPressed);

        pnl.SetActive(false);
    }

    /// <summary>
    /// Attaches LocalizedText to everything the string table can translate.
    ///
    /// Done as one pass over the finished canvas rather than at each of the
    /// hundred-odd call sites: a label added later is picked up automatically,
    /// and there is no second list of strings to keep in step with the first.
    /// </summary>
    static void LocalizeAll(GameObject root)
    {
        var keys = LoadTableKeys();

        // The title is the brand. Other studios leave theirs in Latin too, and
        // it is the one place the segment lettering has to survive.
        var brand = new System.Collections.Generic.HashSet<string> {
            "lbl_title_glow", "lbl_title_main", "lbl_title2_glow", "lbl_title2_main",
        };

        int translated = 0, fontOnly = 0;

        foreach (var t in root.GetComponentsInChildren<Text>(true))
        {
            if (brand.Contains(t.gameObject.name)) continue;
            if (t.GetComponent<LocalizedText>() != null) continue;
            if (t.GetComponent<LanguageLabel>() != null) continue;
            // (true) — the language panel is switched off by the time this
            // runs, and the no-arg overload does not look through inactive
            // parents, so every row would have been localized after all.
            if (t.GetComponentInParent<LanguagePicker>(true) != null) continue;

            string text = t.text ?? "";

            if (keys.Contains(text))
            {
                var lt = t.gameObject.AddComponent<LocalizedText>();
                lt.key = text;
                translated++;
                continue;
            }

            // A score, a timer, an equals sign — no letters now and none later,
            // so leave it in the segment font it was drawn for.
            bool symbolic = true;
            foreach (char c in text)
                if (char.IsLetter(c)) { symbolic = false; break; }
            if (symbolic) continue;

            // Everything else carries text written at runtime — a name, a
            // status line. It needs a font that can draw whatever arrives.
            t.gameObject.AddComponent<LocalizedText>().fontOnly = true;
            fontOnly++;
        }

        Debug.Log("SceneBuilder: localized " + translated + " labels, " +
                  fontOnly + " font-only, from a table of " + keys.Count + " keys.");
    }

    static System.Collections.Generic.HashSet<string> LoadTableKeys()
    {
        var keys = new System.Collections.Generic.HashSet<string>();

        var asset = Resources.Load<TextAsset>("i18n");
        if (asset == null)
        {
            Debug.LogError("SceneBuilder: Assets/Resources/i18n.txt is missing — nothing will be localized.");
            return keys;
        }

        var lines = asset.text.Split('\n');
        for (int i = 1; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd('\r');
            if (line.Length == 0 || line[0] == '#') continue;

            int tab = line.IndexOf('\t');
            if (tab > 0) keys.Add(line.Substring(0, tab).Replace("\\n", "\n"));
        }
        return keys;
    }

    /// <summary>One labelled volume slider bound to an AudioManager channel.</summary>
    static void AudioSliderRow(Transform parent, string name, string label, float y,
        AudioSliderBinder.Channel channel)
    {
        var row = Panel(parent, name, V2(.5f,1), V2(.5f,1), V2(0, y), V2(760, 70));
        var rt = row.transform;

        DigTxt(rt, "lbl_name", label, V2(0,.5f), V2(0,.5f), V2(90,0), V2(180,28), 18, TEXT_DIM,
            TextAnchor.MiddleLeft).raycastTarget = false;

        var lblVal = Txt(rt, "lbl_value", "100%", V2(1,.5f), V2(1,.5f), V2(-45,0), V2(90,28), 16, ACCENT,
            TextAnchor.MiddleRight, FontStyle.Bold);
        lblVal.raycastTarget = false;

        var sliderGO = Panel(rt, "slider", V2(.5f,.5f), V2(.5f,.5f), V2(15,0), V2(400, 44));
        var slider = sliderGO.AddComponent<Slider>();

        var bg = Img(sliderGO.transform, "Background", V2(0,.5f), V2(1,.5f), V2(0,0), V2(0,14), Hex("#1A2E1A"));
        bg.sprite = Pill; bg.type = Image.Type.Sliced;
        bg.pixelsPerUnitMultiplier = 1f; bg.raycastTarget = false;

        var fillArea = Panel(sliderGO.transform, "Fill Area", V2(0,.5f), V2(1,.5f), V2(0,0), V2(-30,14));
        var fill = Img(fillArea.transform, "Fill", V2(0,0), V2(1,1), V2(0,0), V2(30,0), ACCENT);
        fill.sprite = Pill; fill.type = Image.Type.Sliced;
        fill.pixelsPerUnitMultiplier = 1f; fill.raycastTarget = false;

        var handleArea = Panel(sliderGO.transform, "Handle Slide Area", V2(0,0), V2(1,1), V2(0,0), V2(-30,0));
        var handle = Img(handleArea.transform, "Handle", V2(0,0), V2(0,1), V2(0,0), V2(30,-6), ACCENT_LIGHT);
        handle.sprite = Circle;

        slider.fillRect = fill.rectTransform;
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f; slider.maxValue = 1f; slider.value = 1f;

        var binder = sliderGO.AddComponent<AudioSliderBinder>();
        binder.channel = channel;
        binder.valueLabel = lblVal;
    }

    // ── Social login button (icon + text, rounded rect) ───────────────
    static void SocialLoginButton(Transform parent, string name, string label, Vector2 pos,
        Color bgColor, Color textColor, Sprite icon, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = V2(.5f,1); rt.anchorMax = V2(.5f,1);
        rt.anchoredPosition = pos;
        rt.sizeDelta = V2(560, 85);

        var img = go.AddComponent<Image>();
        img.sprite = RoundRect;
        img.type = Image.Type.Sliced;
        img.color = bgColor;

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;

        // Icon on the left
        var iconGO = new GameObject("Icon");
        iconGO.transform.SetParent(go.transform, false);
        var iconRt = iconGO.AddComponent<RectTransform>();
        iconRt.anchorMin = V2(0,.5f); iconRt.anchorMax = V2(0,.5f);
        iconRt.anchoredPosition = V2(50, 0);
        iconRt.sizeDelta = V2(40, 40);
        var iconImg = iconGO.AddComponent<Image>();
        iconImg.sprite = icon;
        iconImg.raycastTarget = false;

        // Text label
        var txt = Txt(go.transform, "Label", label, V2(0,0), V2(1,1), V2(20,0), V2(0,0), 24, textColor,
            TextAnchor.MiddleCenter, FontStyle.Bold);
        txt.raycastTarget = false;

        UnityEventTools.AddPersistentListener(btn.onClick, onClick);
    }

    // ── Google "G" icon (procedural, multi-color) ────────────────────
    static Sprite MakeGoogleIcon()
    {
        int s = 64;
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;

        float cx = s / 2f, cy = s / 2f;
        float outerR = s / 2f - 2f;
        float innerR = outerR * 0.55f;

        Color red    = new Color(0.92f, 0.26f, 0.21f);
        Color yellow = new Color(0.98f, 0.74f, 0.02f);
        Color green  = new Color(0.21f, 0.65f, 0.33f);
        Color blue   = new Color(0.26f, 0.52f, 0.96f);

        for (int y = 0; y < s; y++)
        for (int x = 0; x < s; x++)
        {
            float dx = x - cx + 0.5f, dy = y - cy + 0.5f;
            float dist = Mathf.Sqrt(dx * dx + dy * dy);
            float angle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
            if (angle < 0) angle += 360f;

            // Google G shape: ring with a gap on the right + horizontal bar
            bool inRing = dist <= outerR && dist >= innerR;
            bool inBar = (dx >= 0 && dx <= outerR && Mathf.Abs(dy) <= innerR * 0.35f);
            bool inGap = (angle > 330f || angle < 30f) && dist < outerR;

            if ((inRing && !inGap) || inBar)
            {
                Color c;
                if (inBar) c = blue;
                else if (angle >= 30f && angle < 120f) c = red;
                else if (angle >= 120f && angle < 210f) c = yellow;
                else if (angle >= 210f && angle < 300f) c = green;
                else c = blue;

                float aa = Mathf.Clamp01(outerR - dist + 1f);
                if (inRing) aa = Mathf.Min(aa, Mathf.Clamp01(dist - innerR + 1f));
                tex.SetPixel(x, y, new Color(c.r, c.g, c.b, aa));
            }
            else
                tex.SetPixel(x, y, Color.clear);
        }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, s, s), V2(0.5f, 0.5f));
    }

    // ── Facebook "f" icon (procedural, white on transparent) ─────────
    static Sprite MakeFacebookIcon()
    {
        int s = 64;
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;

        // Clear
        for (int y = 0; y < s; y++)
        for (int x = 0; x < s; x++)
            tex.SetPixel(x, y, Color.clear);

        // Draw "f" shape — vertical bar + horizontal bar
        Color w = Color.white;

        // Vertical bar (main stem)
        for (int y = 8; y < 56; y++)
        for (int x = 28; x <= 36; x++)
            tex.SetPixel(x, y, w);

        // Top curve (from vertical bar going right then up)
        for (int x = 36; x <= 44; x++)
            for (int y = 48; y <= 56; y++)
                tex.SetPixel(x, y, w);
        for (int x = 36; x <= 44; x++)
            tex.SetPixel(x, 48, w);

        // Horizontal bar (crossbar)
        for (int x = 18; x <= 44; x++)
        for (int y = 34; y <= 38; y++)
            tex.SetPixel(x, y, w);

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, s, s), V2(0.5f, 0.5f));
    }

    // ── Speaker icon sprite generator ───────────────────────────────────
    static Vector2 V2(float x, float y) => new Vector2(x, y);
    static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out Color c); return c; }

    static string ColorToHex(Color c)
    {
        return "#" + ColorUtility.ToHtmlStringRGBA(c);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  PROFILE — rating, rank progress and career record
    // ═══════════════════════════════════════════════════════════════════════

    static void ProfilePanel(Transform ct, ProgressionGUIManager prog)
    {
        var pnl = Panel(ct, "pnl_profile", V2(0,0), V2(1,1), V2(0,0), V2(0,0));
        var profBg = pnl.AddComponent<Image>();
        profBg.color = Hex("#0A0E1AFA");
        pnl.AddComponent<ScrollablePanel>().referenceHeight = 1920f;
        var pt = pnl.transform;

        DigTxt(pt, "lbl_prof_title", "PROFILE", V2(.5f,1), V2(.5f,1), V2(0,-70), V2(900,63), 42, ACCENT).raycastTarget = false;
        RoundImg(pt, "ProfAccent", V2(.5f,1), V2(.5f,1), V2(0,-115), V2(120,3), ACCENT).raycastTarget = false;

        var lblName = Txt(pt, "lbl_prof_name", "PLAYER",
            V2(.5f,1), V2(.5f,1), V2(0,-175), V2(900,50), 34, TEXT_PRIMARY,
            TextAnchor.MiddleCenter, FontStyle.Bold);

        // ── Rank card ────────────────────────────────────────────────────
        var card = Panel(pt, "RankCard", V2(.5f,1), V2(.5f,1), V2(0,-350), V2(900, 280));
        RoundImg(card.transform, "CardBG", V2(0,0), V2(1,1), V2(0,0), V2(0,0), Hex("#111A11")).raycastTarget = false;

        var lblRank = Txt(card.transform, "lbl_prof_rank", "SILVER",
            V2(.5f,1), V2(.5f,1), V2(0,-45), V2(700,60), 44, Hex("#C0C0C0"),
            TextAnchor.MiddleCenter, FontStyle.Bold);

        var lblElo = Txt(card.transform, "lbl_prof_elo", "1000",
            V2(.5f,1), V2(.5f,1), V2(0,-120), V2(700,70), 56, TEXT_PRIMARY,
            TextAnchor.MiddleCenter, FontStyle.Bold);

        RoundImg(card.transform, "RankBarBG", V2(.5f,1), V2(.5f,1), V2(0,-185), V2(760,16), Hex("#1A2E1A")).raycastTarget = false;
        var rankBar = RoundImg(card.transform, "bar_fill", V2(.5f,1), V2(.5f,1), V2(0,-185), V2(760,16), ACCENT);
        rankBar.type = Image.Type.Filled;
        rankBar.fillMethod = Image.FillMethod.Horizontal;
        rankBar.fillOrigin = 0; rankBar.fillAmount = 0f;
        rankBar.raycastTarget = false;

        var lblNext = Txt(card.transform, "lbl_prof_next", "150 TO GOLD",
            V2(.5f,1), V2(.5f,1), V2(0,-225), V2(700,30), 18, TEXT_MUTED,
            TextAnchor.MiddleCenter, FontStyle.Normal);

        // ── Stat grid — caption above value, two columns ─────────────────
        DigTxt(pt, "lbl_stats_title", "CAREER", V2(.5f,1), V2(.5f,1), V2(0,-540), V2(900,30), 22, TEXT_DIM).raycastTarget = false;

        Text StatCell(string name, string caption, float x, float y, Color valueColor)
        {
            DigTxt(pt, "cap_" + name, caption, V2(.5f,1), V2(.5f,1), V2(x, y), V2(420,24), 15, TEXT_MUTED)
                .raycastTarget = false;

            return Txt(pt, name, "-", V2(.5f,1), V2(.5f,1), V2(x, y - 38), V2(420,50), 32, valueColor,
                TextAnchor.MiddleCenter, FontStyle.Bold);
        }

        var lblRecord  = StatCell("lbl_prof_record",  "RECORD",       -230, -600, TEXT_PRIMARY);
        var lblWinrate = StatCell("lbl_prof_winrate", "WIN RATE",      230, -600, ACCENT_LIGHT);
        var lblStreak  = StatCell("lbl_prof_streak",  "WIN STREAK",   -230, -715, ACCENT);
        var lblBest    = StatCell("lbl_prof_best",    "BEST STREAK",   230, -715, ACCENT);
        var lblRounds  = StatCell("lbl_prof_rounds",  "ROUNDS WON",   -230, -830, TEXT_PRIMARY);
        var lblXpCell   = StatCell("lbl_prof_xp",      "XP THIS LEVEL", 230, -830, Hex("#4DD0E1"));
        var lblLevel   = StatCell("lbl_prof_level",   "LEVEL",         230, -930, Hex("#4DD0E1"));

        var back = BackArrowButton(pt, "btn_prof_back", V2(0,1), V2(0,1), V2(70, -70), 80, ACCENT_DARK);
        UnityEventTools.AddPersistentListener(
            back.transform.Find("btn_face").GetComponent<Button>().onClick, prog.OnBackPressed);

        prog.pnl_profile      = pnl;
        prog.lbl_prof_name    = lblName;
        prog.lbl_prof_rank    = lblRank;
        prog.lbl_prof_elo     = lblElo;
        prog.lbl_prof_next    = lblNext;
        prog.img_prof_rankbar = rankBar;
        prog.lbl_prof_record  = lblRecord;
        prog.lbl_prof_winrate = lblWinrate;
        prog.lbl_prof_streak  = lblStreak;
        prog.lbl_prof_best    = lblBest;
        prog.lbl_prof_rounds  = lblRounds;
        prog.lbl_prof_xp      = lblXpCell;
        prog.lbl_prof_level   = lblLevel;

        pnl.SetActive(false);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  LEADERBOARD — monthly season, global / country / friends
    // ═══════════════════════════════════════════════════════════════════════

    static void LeaderboardPanel(Transform ct, ProgressionGUIManager prog)
    {
        var pnl = Panel(ct, "pnl_leaderboard", V2(0,0), V2(1,1), V2(0,0), V2(0,0));
        var lt = pnl.transform;

        RoundImg(lt, "LbBG", V2(0,0), V2(1,1), V2(0,0), V2(0,0), Hex("#0A0E1AFA")).raycastTarget = false;

        DigTxt(lt, "lbl_lb_title", "RANKING", V2(.5f,1), V2(.5f,1), V2(0,-70), V2(900,63), 42, ACCENT).raycastTarget = false;

        var lblMonth = Txt(lt, "lbl_lb_month", "SEASON",
            V2(.5f,1), V2(.5f,1), V2(0,-118), V2(600,28), 16, TEXT_MUTED,
            TextAnchor.MiddleCenter, FontStyle.Normal);

        // ── Tabs ─────────────────────────────────────────────────────────
        var tabGlobal  = SegButton(lt, "btn_tab_global",  "GLOBAL",  V2(.5f,1), V2(.5f,1), V2(-240,-190), V2(210,66), 18, ACCENT);
        var tabCountry = SegButton(lt, "btn_tab_country", "COUNTRY", V2(.5f,1), V2(.5f,1), V2(0,-190),    V2(210,66), 18, ACCENT_DARK);
        var tabFriends = SegButton(lt, "btn_tab_friends", "FRIENDS", V2(.5f,1), V2(.5f,1), V2(240,-190),  V2(210,66), 18, ACCENT_DARK);

        UnityEventTools.AddPersistentListener(
            tabGlobal.transform.Find("btn_face").GetComponent<Button>().onClick, prog.OnTabGlobal);
        UnityEventTools.AddPersistentListener(
            tabCountry.transform.Find("btn_face").GetComponent<Button>().onClick, prog.OnTabCountry);
        UnityEventTools.AddPersistentListener(
            tabFriends.transform.Find("btn_face").GetComponent<Button>().onClick, prog.OnTabFriends);

        Img(lt, "LbDivider", V2(.5f,1), V2(.5f,1), V2(0,-240), V2(880,2), Hex("#1A2E1A")).raycastTarget = false;

        var lblStatus = Txt(lt, "lbl_lb_status", "LOADING...",
            V2(.5f,1), V2(.5f,1), V2(0,-268), V2(700,30), 15, TEXT_MUTED,
            TextAnchor.MiddleCenter, FontStyle.Normal);

        // ── Scrolling board ──────────────────────────────────────────────
        var scrollGO = new GameObject("LbScrollView");
        scrollGO.transform.SetParent(lt, false);
        var scrollRt = scrollGO.AddComponent<RectTransform>();
        scrollRt.anchorMin = V2(0,0); scrollRt.anchorMax = V2(1,1);
        scrollRt.offsetMin = V2(40,120); scrollRt.offsetMax = V2(-40,-295);
        scrollGO.AddComponent<Image>().color = new Color(0,0,0,0.01f);
        var scrollRect = TuneScroll(scrollGO.AddComponent<ScrollRect>());
        scrollRect.horizontal = false;
        scrollGO.AddComponent<Mask>().showMaskGraphic = false;

        var contentGO = new GameObject("Content");
        contentGO.transform.SetParent(scrollGO.transform, false);
        var contentRt = contentGO.AddComponent<RectTransform>();
        contentRt.anchorMin = V2(0,1); contentRt.anchorMax = V2(1,1);
        contentRt.pivot = V2(0.5f,1); contentRt.sizeDelta = V2(0,0);
        var vlg = contentGO.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 6;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        var csf = contentGO.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scrollRect.content = contentRt;

        // ── Row template (cloned per entry at runtime) ───────────────────
        var rowGO = new GameObject("LbRowTemplate");
        rowGO.transform.SetParent(lt, false);
        var rowRt = rowGO.AddComponent<RectTransform>();
        rowRt.sizeDelta = V2(0,78);
        rowGO.AddComponent<Image>().color = Hex("#121A12");
        rowGO.AddComponent<LayoutElement>().preferredHeight = 78;

        Txt(rowGO.transform, "lbl_rank", "#1",
            V2(0,.5f), V2(0,.5f), V2(60,0), V2(100,40), 22, TEXT_DIM,
            TextAnchor.MiddleCenter, FontStyle.Bold);

        Txt(rowGO.transform, "lbl_name", "Player",
            V2(0,.5f), V2(0,.5f), V2(330,11), V2(400,32), 21, TEXT_PRIMARY,
            TextAnchor.MiddleLeft, FontStyle.Normal);

        Txt(rowGO.transform, "lbl_sub", "0W",
            V2(0,.5f), V2(0,.5f), V2(330,-15), V2(400,24), 14, TEXT_MUTED,
            TextAnchor.MiddleLeft, FontStyle.Normal);

        Txt(rowGO.transform, "lbl_elo", "1000",
            V2(1,.5f), V2(1,.5f), V2(-80,0), V2(150,40), 26, ACCENT,
            TextAnchor.MiddleRight, FontStyle.Bold);

        rowGO.SetActive(false);

        var back = BackArrowButton(lt, "btn_lb_back", V2(0,1), V2(0,1), V2(70, -70), 80, ACCENT_DARK);
        UnityEventTools.AddPersistentListener(
            back.transform.Find("btn_face").GetComponent<Button>().onClick, prog.OnBackPressed);

        prog.pnl_leaderboard  = pnl;
        prog.lbContent        = contentRt;
        prog.lbRowPrefab      = rowGO;
        prog.lbl_lb_status    = lblStatus;
        prog.lbl_lb_month     = lblMonth;
        prog.lbl_tab_global   = tabGlobal.transform.Find("btn_face/lbl_btn").GetComponent<Text>();
        prog.lbl_tab_country  = tabCountry.transform.Find("btn_face/lbl_btn").GetComponent<Text>();
        prog.lbl_tab_friends  = tabFriends.transform.Find("btn_face/lbl_btn").GetComponent<Text>();

        pnl.SetActive(false);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  DAILY — login streak and three rotating challenges
    // ═══════════════════════════════════════════════════════════════════════

    static void DailyPanel(Transform ct, ProgressionGUIManager prog)
    {
        var pnl = Panel(ct, "pnl_daily", V2(0,0), V2(1,1), V2(0,0), V2(0,0));
        var dailyBg = pnl.AddComponent<Image>();
        dailyBg.color = Hex("#0A0E1AFA");
        pnl.AddComponent<ScrollablePanel>().referenceHeight = 1920f;
        var dt = pnl.transform;

        DigTxt(dt, "lbl_daily_title", "DAILY", V2(.5f,1), V2(.5f,1), V2(0,-70), V2(900,63), 42, Hex("#B388FF")).raycastTarget = false;
        RoundImg(dt, "DailyAccent", V2(.5f,1), V2(.5f,1), V2(0,-115), V2(120,3), Hex("#B388FF")).raycastTarget = false;

        var coinDot = Img(dt, "img_daily_coin", V2(1,1), V2(1,1), V2(-215,-72), V2(22,22), Hex("#FFD600"));
        coinDot.sprite = Circle;
        coinDot.raycastTarget = false;

        var lblXpCell = Txt(dt, "lbl_daily_xp", "LV 1",
            V2(1,1), V2(1,1), V2(-110,-72), V2(180,40), 22, Hex("#FFD600"),
            TextAnchor.MiddleLeft, FontStyle.Bold);

        // ── Streak card ──────────────────────────────────────────────────
        var card = Panel(dt, "StreakCard", V2(.5f,1), V2(.5f,1), V2(0,-300), V2(900, 300));
        RoundImg(card.transform, "CardBG", V2(0,0), V2(1,1), V2(0,0), V2(0,0), Hex("#150F1F")).raycastTarget = false;

        var lblStreak = Txt(card.transform, "lbl_daily_streak", "0",
            V2(.5f,1), V2(.5f,1), V2(0,-75), V2(700,110), 84, Hex("#B388FF"),
            TextAnchor.MiddleCenter, FontStyle.Bold);

        var lblSub = Txt(card.transform, "lbl_daily_sub", "DAY STREAK",
            V2(.5f,1), V2(.5f,1), V2(0,-145), V2(700,32), 22, TEXT_DIM,
            TextAnchor.MiddleCenter, FontStyle.Normal);

        var lblBest = Txt(card.transform, "lbl_daily_best", "BEST: 0",
            V2(.5f,1), V2(.5f,1), V2(0,-180), V2(700,26), 15, TEXT_MUTED,
            TextAnchor.MiddleCenter, FontStyle.Normal);

        var btnClaim = SegButton(card.transform, "btn_claim_streak", "PLAY A MATCH TODAY",
            V2(.5f,1), V2(.5f,1), V2(0,-240), V2(560, 72), 20, Hex("#B388FF"));
        UnityEventTools.AddPersistentListener(
            btnClaim.transform.Find("btn_face").GetComponent<Button>().onClick, prog.OnClaimStreakPressed);

        // ── Challenges ───────────────────────────────────────────────────
        DigTxt(dt, "lbl_chal_title", "TODAY'S CHALLENGES", V2(.5f,1), V2(.5f,1), V2(0,-500), V2(900,30), 22, TEXT_DIM)
            .raycastTarget = false;

        var row0 = ChallengeRow(dt, "chal_row_0", -580);
        var row1 = ChallengeRow(dt, "chal_row_1", -710);
        var row2 = ChallengeRow(dt, "chal_row_2", -840);

        UnityEventTools.AddPersistentListener(
            row0.Find("btn_claim/btn_face").GetComponent<Button>().onClick, prog.OnClaimChallenge0);
        UnityEventTools.AddPersistentListener(
            row1.Find("btn_claim/btn_face").GetComponent<Button>().onClick, prog.OnClaimChallenge1);
        UnityEventTools.AddPersistentListener(
            row2.Find("btn_claim/btn_face").GetComponent<Button>().onClick, prog.OnClaimChallenge2);

        var back = BackArrowButton(dt, "btn_daily_back", V2(0,1), V2(0,1), V2(70, -70), 80, ACCENT_DARK);
        UnityEventTools.AddPersistentListener(
            back.transform.Find("btn_face").GetComponent<Button>().onClick, prog.OnBackPressed);

        prog.pnl_daily        = pnl;
        prog.lbl_daily_streak = lblStreak;
        prog.lbl_daily_sub    = lblSub;
        prog.lbl_daily_best   = lblBest;
        prog.lbl_daily_xp     = lblXpCell;
        prog.btn_claim_streak = btnClaim;
        prog.lbl_claim_streak = btnClaim.transform.Find("btn_face/lbl_btn").GetComponent<Text>();
        prog.challengeRow0    = row0;
        prog.challengeRow1    = row1;
        prog.challengeRow2    = row2;

        pnl.SetActive(false);
    }

    /// <summary>
    /// One challenge line: description, progress bar, counter and a claim button.
    /// ProgressionGUIManager looks these children up by name.
    /// </summary>
    static Transform ChallengeRow(Transform parent, string name, float y)
    {
        var row = Panel(parent, name, V2(.5f,1), V2(.5f,1), V2(0, y), V2(900, 115));
        RoundImg(row.transform, "RowBG", V2(0,0), V2(1,1), V2(0,0), V2(0,0), Hex("#111A11")).raycastTarget = false;

        Txt(row.transform, "lbl_desc", "WIN 3 MATCHES",
            V2(0,1), V2(0,1), V2(230,-32), V2(420,34), 21, TEXT_PRIMARY,
            TextAnchor.MiddleLeft, FontStyle.Normal);

        Txt(row.transform, "lbl_prog", "0 / 3",
            V2(0,1), V2(0,1), V2(505,-32), V2(140,30), 17, TEXT_MUTED,
            TextAnchor.MiddleRight, FontStyle.Normal);

        var bar = Panel(row.transform, "bar", V2(0,1), V2(0,1), V2(230,-72), V2(420,12));
        RoundImg(bar.transform, "bar_bg", V2(0,0), V2(1,1), V2(0,0), V2(0,0), Hex("#1A2E1A")).raycastTarget = false;
        var fill = RoundImg(bar.transform, "bar_fill", V2(0,0), V2(1,1), V2(0,0), V2(0,0), Hex("#B388FF"));
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = 0; fill.fillAmount = 0f;
        fill.raycastTarget = false;

        SegButton(row.transform, "btn_claim", "+15",
            V2(1,.5f), V2(1,.5f), V2(-100,0), V2(160, 66), 18, Hex("#76FF03"));

        return row.transform;
    }
}
#endif
