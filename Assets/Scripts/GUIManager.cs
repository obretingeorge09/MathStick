using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class GUIManager : MonoBehaviour
{
    public GameObject pnl_login     = null;
    public GameObject pnl_register  = null;
    public GameObject pnl_start     = null;
    public GameObject pnl_modeSelect = null;
    public GameObject pnl_tutorial  = null;
    public GameObject pnl_main      = null;
    public GameObject pnl_continue  = null;
    public GameObject pnl_fader     = null;

    public Text  lbl_gameProgress   = null;  // big streak number
    public Text  lbl_timer          = null;
    public Text  lbl_timeLabel      = null;  // caption above the clock
    public Text  lbl_highscore      = null;
    public Text  lbl_startHighscore = null;  // highscore on start screen
    public Text  lbl_result         = null;  // "WELL DONE!" / "TIME'S UP!"
    public Text  lbl_inARow         = null;  // "X IN A ROW!"
    public Text  lbl_bestScore      = null;  // "PERSONAL BEST: X"
    public Text  lbl_correctAnswer  = null;  // shows correct equation on loss
    public Text  lbl_btnText        = null;  // button label
    public Image timerBarFill       = null;
    public float timerMaxTime       = 60f;

    // Login fields
    public InputField inp_email     = null;
    public InputField inp_password  = null;
    public InputField inp_reg_name  = null;
    public InputField inp_reg_email = null;
    public InputField inp_reg_pass  = null;
    public InputField inp_reg_confirm_pass = null;
    public Text lbl_login_error     = null;

    // Forgot password
    public GameObject pnl_forgotPassword = null;
    public InputField inp_forgot_email   = null;
    public Text lbl_forgot_status        = null;

    Coroutine autoContinueCo = null;

    static readonly Color ColWarm   = new Color(0.98f, 0.92f, 0.78f);
    static readonly Color ColYellow = new Color(1.00f, 0.70f, 0.08f);
    static readonly Color ColRed    = new Color(1.00f, 0.27f, 0.14f);

    void Awake()
    {
        Messenger.AddListener(Message.GameWon,  GameWon);
        Messenger.AddListener(Message.GameLost, OnGameLost);
        Messenger.AddListener<string>(Message.OnEndFadeToOpaque,      OnEndFadeToOpaque);
        Messenger.AddListener<string>(Message.OnEndFadeToTransparent, OnEndFadeToTransparent);
        Messenger.AddListener<float>(Message.OnSetTimer,    OnSetTimer);
        Messenger.AddListener<float>(Message.OnSetTimerMax, OnSetTimerMax);
        Messenger.AddListener<int, int>(Message.SaveGame,
            (hs, cs) => { UpdateHighscoreDisplay(hs); });
        Messenger.AddListener<int, int>(Message.SetHighscoreAndCurrentScore,
            (hs, cs) => { UpdateHighscoreDisplay(hs); });
    }

    void UpdateHighscoreDisplay(int hs)
    {
        string hsText = hs.ToString();
        if (lbl_highscore) lbl_highscore.text = hsText;
        if (lbl_startHighscore) lbl_startHighscore.text = hsText;
    }

    void Start()
    {
        // Every one of these is guarded: this is the very first thing that runs,
        // and a single unassigned reference here means the app never reaches
        // InitFirebase and the player stares at a black screen.
        Show(pnl_fader, false);
        Show(pnl_continue, false);
        Show(pnl_main, false);
        Show(pnl_tutorial, false);
        Show(pnl_start, false);
        Show(pnl_modeSelect, false);
        Show(pnl_login, false);
        Show(pnl_register, false);
        Show(pnl_forgotPassword, false);

        AuthManager.Instance.InitFirebase(() => {
            UnityMainThreadDispatcher.Enqueue(() => {
                // Initialize social login bridges after Firebase is ready
                if (GoogleSignInBridge.Instance != null)
                    GoogleSignInBridge.Instance.Initialize();
                if (FacebookSignInBridge.Instance != null)
                    FacebookSignInBridge.Instance.Initialize();

                if (AuthManager.Instance.IsLoggedIn)
                    ShowStartScreen();
                else
                    ShowLoginScreen();
            });
        });
    }

    // ── Start screen buttons ──────────────────────────────────────────────
    public void OnPlayPressed()
    {
        Debug.Log("OnPlayPressed - pnl_modeSelect is " + (pnl_modeSelect == null ? "NULL" : "assigned"));
        HideAllPanels();
        if (pnl_modeSelect != null)
        {
            pnl_modeSelect.SetActive(true);
            Debug.Log("pnl_modeSelect activated, childCount=" + pnl_modeSelect.transform.childCount);
        }
    }

    public void OnModeEasy()
    {
        GameManager.Instance.SetMode(GameMode.Easy);
        HideAllPanels();
        StartNewGame();
    }

    public void OnModeMedium()
    {
        GameManager.Instance.SetMode(GameMode.Medium);
        HideAllPanels();
        StartNewGame();
    }

    public void OnModeHard()
    {
        GameManager.Instance.SetMode(GameMode.Hard);
        HideAllPanels();
        StartNewGame();
    }

    public void OnModeBackPressed()
    {
        HideAllPanels();
        Show(pnl_start, true);
    }

    public void OnBackToMenuPressed()
    {
        if (autoContinueCo != null) { StopCoroutine(autoContinueCo); autoContinueCo = null; }
        HideAllPanels();
        Show(pnl_start, true);
    }

    public void OnArcadePressed()
    {
        if (ArcadeGUIManager.Instance == null)
        {
            Debug.LogError("ArcadeGUIManager.Instance is null! Did you run Build > PlusMinus Scene?");
            return;
        }
        HideAllPanels();
        ArcadeGUIManager.Instance.ShowModeSelect();
    }

    public void OnTutorialPressed()
    {
        HideAllPanels();
        Show(pnl_tutorial, true);
    }

    public void OnTutorialBackPressed()
    {
        HideAllPanels();
        Show(pnl_start, true);
    }

    // ── Called by button and auto-continue ─────────────────────────────────
    public void StartNewGame()
    {
        if (autoContinueCo != null) { StopCoroutine(autoContinueCo); autoContinueCo = null; }
        Messenger.Broadcast<float, string>(Message.OnStartFadeToOpaque, 0.2f, "fadeToOpaqueBeforeGameStarts");
    }

    // ── Timer ──────────────────────────────────────────────────────────────
    void OnSetTimerMax(float max)
    {
        timerMaxTime = max;
    }

    void OnSetTimer(float timer)
    {
        // Above a minute show M:SS; below it, SS:CC. The old format was
        // seconds % 100, which wrapped: a 100-second training round opened
        // reading "00:00" and a 320-second one read "20".
        // Hundredths only earn their place when time is nearly out.
        if (lbl_timer != null)
        {
            int whole = Mathf.Max(0, Mathf.FloorToInt(timer));

            if (timer >= 60f)
            {
                int mins = whole / 60;
                int secs = whole % 60;
                lbl_timer.text = mins + ":" + (secs < 10 ? "0" + secs : secs.ToString());
            }
            else
            {
                int hundredths = Mathf.Clamp((int)((timer - whole) * 100f), 0, 99);
                lbl_timer.text = (whole < 10 ? "0" + whole : whole.ToString())
                               + ":" + (hundredths < 10 ? "0" + hundredths : hundredths.ToString());
            }
        }

        // Max 0 means "no limit" — 1v1 rounds are a race with no clock running
        // out, so the bar goes away and the readout stays a neutral colour
        // rather than turning red as if the player were about to lose.
        bool unlimited = timerMaxTime <= 0f;

        if (lbl_timeLabel != null)
        {
            string caption = unlimited ? "TIME" : "TIME REMAINING";
            if (lbl_timeLabel.text != caption) lbl_timeLabel.text = caption;
        }

        float ratio = unlimited ? 1f : Mathf.Clamp01(timer / timerMaxTime);
        Color tint = unlimited ? ColWarm
                   : ratio > 0.33f ? ColWarm
                   : ratio > 0.11f ? ColYellow : ColRed;

        if (timerBarFill != null)
        {
            if (timerBarFill.gameObject.activeSelf == unlimited)
                timerBarFill.gameObject.SetActive(!unlimited);

            timerBarFill.fillAmount = ratio;
            timerBarFill.color = tint;
        }

        if (lbl_timer != null) lbl_timer.color = tint;
    }

    // ── Fade-to-opaque finished ────────────────────────────────────────────
    void OnEndFadeToOpaque(string id)
    {
        if (id == "fadeToOpaqueAfterGameWon" || id == "fadeToOpaqueAfterGameLost")
        {
            pnl_main.SetActive(false);
            pnl_continue.SetActive(true);

            int streak = Messenger.BroadcastReceiver<int>(ReceiveMessage.ReceiveGameProgress);
            lbl_gameProgress.text = streak.ToString();

            string best = lbl_highscore != null ? lbl_highscore.text : "0";

            if (id == "fadeToOpaqueAfterGameWon")
            {
                if (lbl_inARow)    lbl_inARow.text    = streak + " IN A ROW!";
                if (lbl_bestScore) lbl_bestScore.text  = "PERSONAL BEST: " + best;
                if (lbl_correctAnswer) lbl_correctAnswer.gameObject.SetActive(false);
                if (lbl_btnText)   lbl_btnText.text    = "CONTINUE";

                Messenger.Broadcast<float, string>(Message.OnStartFadeToTransparent,
                    0.2f, "fadeToTransparentAfterGameWon");
            }
            else
            {
                if (lbl_inARow)    lbl_inARow.text    = "STREAK LOST";
                if (lbl_bestScore) lbl_bestScore.text  = "PERSONAL BEST: " + best;
                if (lbl_correctAnswer)
                {
                    lbl_correctAnswer.gameObject.SetActive(true);
                    lbl_correctAnswer.text = GameManager.Instance.correctSolution;
                }
                if (lbl_btnText)   lbl_btnText.text    = "TRY AGAIN";

                Messenger.Broadcast(Message.OnResetProgress);
                Messenger.Broadcast<float, string>(Message.OnStartFadeToTransparent,
                    0.2f, "fadeToTransparentAfterGameLost");
            }
        }
        else if (id == "fadeToOpaqueBeforeGameStarts")
        {
            Messenger.Broadcast(Message.StartNewGame);
            pnl_continue.SetActive(false);
            pnl_main.SetActive(false);
            Messenger.Broadcast<float, string>(Message.OnStartFadeToTransparent,
                0.2f, "fadeToTransparentBeforeGameStarts");
        }
    }

    // ── Fade-to-transparent finished ──────────────────────────────────────
    void OnEndFadeToTransparent(string id)
    {
        Debug.Log("OnEndFadeToTransparent: " + id);
        if (id == "fadeToTransparentBeforeGameStarts")
        {
            Debug.Log("Activating pnl_main");
            pnl_main.SetActive(true);
            ApplyBgColor();
        }
    }

    // ── Win / Lose ─────────────────────────────────────────────────────────
    void GameWon()
    {
        if (lbl_result != null) lbl_result.text = "WELL DONE!";
        Color c = Messenger.BroadcastReceiver<Color>(ReceiveMessage.ReceiveWinGUIColor);
        pnl_continue.GetComponent<Image>().color = c;
        Messenger.Broadcast<float, string>(Message.OnStartFadeToOpaque, 0.2f, "fadeToOpaqueAfterGameWon");
    }

    void OnGameLost()
    {
        if (lbl_result != null) lbl_result.text = "TIME'S UP!";
        Color c = Messenger.BroadcastReceiver<Color>(ReceiveMessage.ReceiveLoseGUIColor);
        pnl_continue.GetComponent<Image>().color = c;
        Messenger.Broadcast<float, string>(Message.OnStartFadeToOpaque, 0.2f, "fadeToOpaqueAfterGameLost");
    }

    // ── Auto-continue coroutine (win only) ─────────────────────────────────
    IEnumerator AutoContinue(float delay)
    {
        float t = delay;
        while (t > 0f)
        {
            int sec = Mathf.CeilToInt(t);
            if (lbl_btnText) lbl_btnText.text = "CONTINUE (" + sec + ")";
            t -= Time.deltaTime;
            yield return null;
        }
        autoContinueCo = null;
        StartNewGame();
    }

    // ── Login / Register ───────────────────────────────────────────────────
    void HideAllPanels()
    {
        if (pnl_login) pnl_login.SetActive(false);
        if (pnl_register) pnl_register.SetActive(false);
        if (pnl_start) pnl_start.SetActive(false);
        if (pnl_modeSelect) pnl_modeSelect.SetActive(false);
        if (pnl_main) pnl_main.SetActive(false);
        if (pnl_continue) pnl_continue.SetActive(false);
        if (pnl_tutorial) pnl_tutorial.SetActive(false);
        if (pnl_forgotPassword) pnl_forgotPassword.SetActive(false);
        if (pnl_settings) pnl_settings.SetActive(false);
        ArcadeGUIManager.Instance?.HideAllPanels();
        ProgressionGUIManager.Instance?.HideAll();
    }

    /// <summary>HideAllPanels for other managers that open their own screens.</summary>
    public void HideAllPanelsPublic() => HideAllPanels();

    public void ShowLoginScreen()
    {
        HideAllPanels();
        Show(pnl_login, true);
        if (lbl_login_error) lbl_login_error.text = "";
    }

    bool progressionLoaded = false;

    public void ShowStartScreen()
    {
        HideAllPanels();
        Show(pnl_start, true);

        // First time we reach the menu we know Firebase and the user are ready
        if (!progressionLoaded && AuthManager.Instance != null && AuthManager.Instance.IsLoggedIn)
        {
            progressionLoaded = true;
            PlayerStatsManager.Instance?.Load();
            DailyManager.Instance?.Load();
        }

        ProgressionGUIManager.Instance?.RefreshMenuBadges();
    }

    // ── Progression screens ───────────────────────────────────────────────
    public void OnProfilePressed()     { ProgressionGUIManager.Instance?.ShowProfile(); }
    public void OnLeaderboardPressed() { ProgressionGUIManager.Instance?.ShowLeaderboard(); }
    public void OnDailyPressed()       { ProgressionGUIManager.Instance?.ShowDaily(); }

    static readonly Color FieldNormal = new Color(0.08f, 0.10f, 0.16f, 1f);
    static readonly Color FieldError  = new Color(0.35f, 0.05f, 0.05f, 1f);

    void HighlightField(InputField field, bool error)
    {
        if (field == null) return;
        var img = field.GetComponent<Image>();
        if (img) img.color = error ? FieldError : FieldNormal;
    }

    void ResetFieldColors()
    {
        HighlightField(inp_email, false);
        HighlightField(inp_password, false);
    }

    public void OnLoginPressed()
    {
        if (lbl_login_error) lbl_login_error.text = "";
        ResetFieldColors();

        string email = inp_email?.text ?? "";
        string pass  = inp_password?.text ?? "";

        bool hasError = false;
        if (string.IsNullOrEmpty(email)) {
            HighlightField(inp_email, true);
            hasError = true;
        }
        if (string.IsNullOrEmpty(pass)) {
            HighlightField(inp_password, true);
            hasError = true;
        }
        if (hasError) {
            if (lbl_login_error) lbl_login_error.text = "Please fill in all fields";
            return;
        }
        if (!email.Contains("@")) {
            HighlightField(inp_email, true);
            if (lbl_login_error) lbl_login_error.text = "Invalid email format";
            return;
        }

        AuthManager.Instance.OnLoginSuccess += OnLoginSuccess;
        AuthManager.Instance.OnLoginFailed  += OnLoginError;
        AuthManager.Instance.LoginEmail(email, pass);
    }

    void OnLoginSuccess()
    {
        AuthManager.Instance.OnLoginSuccess -= OnLoginSuccess;
        AuthManager.Instance.OnLoginFailed  -= OnLoginError;
        UnityMainThreadDispatcher.Enqueue(ShowStartScreen);
    }

    void OnLoginError(string msg)
    {
        AuthManager.Instance.OnLoginSuccess -= OnLoginSuccess;
        AuthManager.Instance.OnLoginFailed  -= OnLoginError;
        UnityMainThreadDispatcher.Enqueue(() => {
            string errorMsg = msg;
            // Firebase error messages
            if (msg.Contains("user-not-found") || msg.Contains("invalid-email"))
                errorMsg = "Account doesn't exist. Please sign up.";
            else if (msg.Contains("wrong-password"))
                errorMsg = "Incorrect password";
            else if (msg.Contains("too-many-requests"))
                errorMsg = "Too many login attempts. Try again later.";

            if (lbl_login_error) lbl_login_error.text = errorMsg;
        });
    }

    public void OnShowForgotPasswordPanel()
    {
        HideAllPanels();
        if (pnl_forgotPassword) pnl_forgotPassword.SetActive(true);
        if (lbl_forgot_status) lbl_forgot_status.text = "";
        // Pre-fill email if user already typed it
        if (inp_forgot_email != null && inp_email != null && !string.IsNullOrEmpty(inp_email.text))
            inp_forgot_email.text = inp_email.text;
    }

    public void OnSendResetEmailPressed()
    {
        if (lbl_forgot_status) lbl_forgot_status.text = "";
        string email = inp_forgot_email ? inp_forgot_email.text : "";

        if (string.IsNullOrEmpty(email)) {
            if (lbl_forgot_status) lbl_forgot_status.text = "Enter your email";
            return;
        }
        if (!email.Contains("@")) {
            if (lbl_forgot_status) lbl_forgot_status.text = "Invalid email format";
            return;
        }

        if (lbl_forgot_status) lbl_forgot_status.text = "SENDING...";
        AuthManager.Instance.OnLoginFailed += OnForgotPasswordResult;
        AuthManager.Instance.ResetPassword(email);
    }

    void OnForgotPasswordResult(string msg)
    {
        AuthManager.Instance.OnLoginFailed -= OnForgotPasswordResult;
        UnityMainThreadDispatcher.Enqueue(() => {
            if (lbl_forgot_status)
            {
                string lower = msg.ToLower();
                if (lower.Contains("sent") || lower.Contains("check") || lower.Contains("success"))
                {
                    lbl_forgot_status.color = new Color(0.46f, 1f, 0.01f);
                    lbl_forgot_status.text = "Reset link sent! Check your email.";
                }
                else if (lower.Contains("no user") || lower.Contains("not found") || lower.Contains("no account") || lower.Contains("user-not-found") || lower.Contains("doesn't exist") || lower.Contains("does not exist") || lower.Contains("sign up"))
                {
                    lbl_forgot_status.color = new Color(1f, 0.4f, 0.4f);
                    lbl_forgot_status.text = "No account found for this email.\nPlease create an account.";
                }
                else
                {
                    lbl_forgot_status.color = new Color(1f, 0.4f, 0.4f);
                    lbl_forgot_status.text = msg;
                }
            }
        });
    }

    public void OnShowRegisterPressed()
    {
        HideAllPanels();
        Show(pnl_register, true);
        if (lbl_login_error) lbl_login_error.text = "";
    }

    public void OnRegisterPressed()
    {
        if (lbl_login_error) lbl_login_error.text = "";
        string name  = inp_reg_name?.text ?? "";
        string email = inp_reg_email?.text ?? "";
        string pass  = inp_reg_pass?.text ?? "";
        string confPass = inp_reg_confirm_pass?.text ?? "";

        if (string.IsNullOrEmpty(name)) {
            if (lbl_login_error) lbl_login_error.text = "Username is required";
            return;
        }
        if (string.IsNullOrEmpty(email)) {
            if (lbl_login_error) lbl_login_error.text = "Email is required";
            return;
        }
        if (string.IsNullOrEmpty(pass)) {
            if (lbl_login_error) lbl_login_error.text = "Password is required";
            return;
        }
        if (string.IsNullOrEmpty(confPass)) {
            if (lbl_login_error) lbl_login_error.text = "Confirm password is required";
            return;
        }
        if (!email.Contains("@")) {
            if (lbl_login_error) lbl_login_error.text = "Invalid email format";
            return;
        }
        if (pass.Length < 6) {
            if (lbl_login_error) lbl_login_error.text = "Password must be at least 6 characters";
            return;
        }
        if (pass != confPass) {
            if (lbl_login_error) lbl_login_error.text = "Passwords don't match";
            return;
        }

        AuthManager.Instance.OnLoginSuccess += OnLoginSuccess;
        AuthManager.Instance.OnLoginFailed  += OnLoginError;
        AuthManager.Instance.RegisterEmail(email, pass, name);
    }

    public void OnBackToLoginPressed()
    {
        ShowLoginScreen();
    }

    // ── Social login ──────────────────────────────────────────────────────
    public void OnGoogleLoginPressed()
    {
        if (lbl_login_error) { lbl_login_error.color = new Color(1f, 0.7f, 0.2f); lbl_login_error.text = ""; }
        AuthManager.Instance.OnLoginSuccess += OnLoginSuccess;
        AuthManager.Instance.OnLoginFailed  += OnSocialLoginError;
        AuthManager.Instance.LoginWithGoogle();
    }

    public void OnFacebookLoginPressed()
    {
        if (lbl_login_error) { lbl_login_error.color = new Color(1f, 0.7f, 0.2f); lbl_login_error.text = ""; }
        AuthManager.Instance.OnLoginSuccess += OnLoginSuccess;
        AuthManager.Instance.OnLoginFailed  += OnSocialLoginError;
        AuthManager.Instance.LoginWithFacebook();
    }

    void OnSocialLoginError(string msg)
    {
        AuthManager.Instance.OnLoginSuccess -= OnLoginSuccess;
        AuthManager.Instance.OnLoginFailed  -= OnSocialLoginError;
        UnityMainThreadDispatcher.Enqueue(() => {
            if (lbl_login_error)
            {
                lbl_login_error.color = new Color(1f, 0.7f, 0.2f);
                lbl_login_error.text = msg;
            }
        });
    }

    // ── Guest Login ────────────────────────────────────────────────────
    public void OnGuestLoginPressed()
    {
        if (lbl_login_error) { lbl_login_error.color = new Color(0.5f, 1f, 0.5f); lbl_login_error.text = "Connecting..."; }
        AuthManager.Instance.OnLoginSuccess += OnLoginSuccess;
        AuthManager.Instance.OnLoginFailed  += OnSocialLoginError;
        AuthManager.Instance.LoginAsGuest();
    }

    // ── Settings ────────────────────────────────────────────────────────
    public GameObject pnl_settings = null;

    public void OnSettingsPressed()
    {
        HideAllPanels();
        if (pnl_settings) pnl_settings.SetActive(true);
    }

    public void OnSettingsBackPressed()
    {
        HideAllPanels();
        Show(pnl_start, true);
    }

    public Image gameBG = null;

    public void ApplyBgColor()
    {
        if (gameBG && GameSettings.Instance != null)
            gameBG.color = GameSettings.Instance.SelectedBgColor;
    }

    // Audio is owned by VolumeControl now — it manages the corner key, the
    // level readout and the stepped popover. These remain so any older wiring
    // or serialized UnityEvent reference still resolves.
    public void OnVolumePressed()
    {
        VolumeControl.Instance?.Toggle();
    }

    public void UpdateVolumeIcon()
    {
        VolumeControl.Instance?.Redraw();
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
