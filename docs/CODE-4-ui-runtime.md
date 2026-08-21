# MathStick — UI runtime — panels, audio, layout, tutorial

> **Generated file — do not edit.** Regenerated from the sources listed below.
> The code in `Assets/` is the only source of truth; this exists so the whole
> project can be handed to a tool that reads documents rather than a repo.

> Everything that drives the generated UI at runtime.

---

## `Assets/Scripts/GUIManager.cs`

```csharp
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
```

## `Assets/Scripts/ArcadeGUIManager.cs`

```csharp
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class ArcadeGUIManager : MonoBehaviour
{
    public static ArcadeGUIManager Instance { get; private set; }

    // ── Panels ──────────────────────────────────────────────────────────
    public GameObject pnl_arcadeModeSelect = null;
    public GameObject pnl_lobby = null;
    public GameObject pnl_arcadeWaiting = null;
    public GameObject pnl_arcadeHUD = null;
    public GameObject pnl_arcadeResult = null;
    public GameObject pnl_invitePopup = null;

    // ── Mode Select UI ──────────────────────────────────────────────────
    public Text lbl_modeSelectTitle = null;
    GameMode selectedMode = GameMode.Easy;
    int selectedFirstTo = 3;

    // ── Lobby UI ────────────────────────────────────────────────────────
    public InputField inp_search = null;
    public Transform userListContent = null;  // ScrollView content (online players)
    public Transform friendListContent = null; // ScrollView content (friends)
    public GameObject userRowPrefab = null;    // set by SceneBuilder
    public GameObject friendRowPrefab = null;  // set by SceneBuilder
    public Text lbl_lobbyStatus = null;
    public Text lbl_friendsStatus = null;

    // ── Waiting UI ──────────────────────────────────────────────────────
    public Text lbl_waitingStatus = null;

    // ── HUD UI (overlay on pnl_main) ────────────────────────────────────
    public Text lbl_myScore = null;
    public Text lbl_oppScore = null;
    public Text lbl_roundInfo = null;
    public Text lbl_oppName = null;

    // ── Result UI ───────────────────────────────────────────────────────
    public Text lbl_resultTitle = null;     // "YOU WIN!" / "YOU LOSE"
    public Text lbl_resultScore = null;     // "3 - 1"
    public Text lbl_resultDetail = null;
    public Text lbl_resultElo = null;       // "+18  →  1218 SILVER"
    public Text lbl_rematchStatus = null;   // rematch negotiation feedback
    public GameObject btn_rematch = null;   // hidden once a rematch is pending

    // ── Invite Popup UI ─────────────────────────────────────────────────
    public Text lbl_inviteFrom = null;
    public Text lbl_inviteMode = null;

    // ── Out of coins ────────────────────────────────────────────────────
    public GameObject pnl_noCoins = null;
    public Text lbl_noCoins_detail = null;
    public GameObject btn_watchAd = null;
    public Text lbl_watchAd = null;

    // ── Round overlay ───────────────────────────────────────────────────
    public GameObject pnl_roundOverlay = null;
    public Text lbl_roundResult = null;
    public Text lbl_roundAnswer = null;   // the equation that was being raced
    public Text lbl_resultAnswer = null;  // same, on the final-round screen

    InviteData currentInvite;

    // A match is either against a real player (ArcadeMatchManager) or a bot
    // (BotMatchManager). Both drive the same panels, so every read of score /
    // round / opponent goes through the accessors below.
    bool inBotMatch = false;
    bool resultRecorded = false;

    // Charged when the search starts, refunded if the player backs out before
    // a match is ever made. Nobody pays for a queue they left.
    int pendingFee = 0;

    int      CurMyScore  => inBotMatch ? (BotMatchManager.Instance?.MyScore ?? 0)
                                       : (ArcadeMatchManager.Instance?.MyScore ?? 0);
    int      CurOppScore => inBotMatch ? (BotMatchManager.Instance?.OpponentScore ?? 0)
                                       : (ArcadeMatchManager.Instance?.OpponentScore ?? 0);
    int      CurRound    => inBotMatch ? (BotMatchManager.Instance?.CurrentRound ?? 1)
                                       : (ArcadeMatchManager.Instance?.CurrentRound ?? 1);
    string   CurOppName  => inBotMatch ? BotMatchManager.Instance?.OpponentName
                                       : ArcadeMatchManager.Instance?.OpponentName;
    GameMode CurMode     => inBotMatch ? (BotMatchManager.Instance?.MatchMode ?? GameMode.Easy)
                                       : (ArcadeMatchManager.Instance?.MatchMode ?? GameMode.Easy);

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(this); return; }
    }

    void Start()
    {
        // Subscribe to search field changes
        if (inp_search != null)
            inp_search.onValueChanged.AddListener((_) => OnSearchChanged());

        // Subscribe to LobbyManager events
        var lobby = LobbyManager.Instance;
        if (lobby != null)
        {
            lobby.OnOnlineUsersUpdated += RefreshUserList;
            lobby.OnFriendsUpdated += RefreshFriendList;
            lobby.OnInviteReceived += ShowInvitePopup;
            lobby.OnMatchFound += OnMatchCreated;
            lobby.OnInviteAccepted += OnMatchCreated;
            lobby.OnInviteDeclined += OnInviteDeclined;
            lobby.OnBotFallback += OnBotFallback;
            lobby.OnError += ShowError;
        }

        // Subscribe to ArcadeMatchManager events
        var match = ArcadeMatchManager.Instance;
        if (match != null)
        {
            match.OnScoreUpdated += UpdateScoreDisplay;
            match.OnRoundResult += ShowRoundResult;
            match.OnMatchResult += ShowMatchResult;
            match.OnEquationReady += OnEquationReady;
            match.OnOpponentDisconnected += OnOpponentDisconnected;
            match.OnOpponentJoined += OnOpponentJoined;
            match.OnError += ShowError;

            match.OnRematchRequestedByOpponent += OnOpponentWantsRematch;
            match.OnRematchDeclined += OnRematchDeclined;
            match.OnRematchTimedOut += OnRematchTimedOut;
            match.OnRematchStarting += OnRematchStarting;
        }

        // Bot matches raise the same events, so they reuse the same handlers
        var bot = BotMatchManager.Instance;
        if (bot != null)
        {
            bot.OnScoreUpdated += UpdateScoreDisplay;
            bot.OnRoundResult += ShowRoundResult;
            bot.OnMatchResult += ShowMatchResult;
            bot.OnEquationReady += OnEquationReady;
            bot.OnOpponentJoined += OnOpponentJoined;
            bot.OnError += ShowError;
        }

        // The rating is fetched asynchronously, so the result screen fills in late
        if (PlayerStatsManager.Instance != null)
            PlayerStatsManager.Instance.OnStatsChanged += RefreshResultElo;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Mode Select Panel
    // ═══════════════════════════════════════════════════════════════════

    public void ShowModeSelect()
    {
        HideAllArcadePanels();
        Show(pnl_arcadeModeSelect, true);
        inBotMatch = false;
        selectedMode = GameMode.Easy;
        selectedFirstTo = 3;
        UpdateModeSelectUI();
    }

    public void OnSelectEasy()    { selectedMode = GameMode.Easy;   UpdateModeSelectUI(); }
    public void OnSelectMedium()  { selectedMode = GameMode.Medium; UpdateModeSelectUI(); }
    public void OnSelectHard()    { selectedMode = GameMode.Hard;   UpdateModeSelectUI(); }
    public void OnSelectRandom()  { selectedMode = GameMode.Random; UpdateModeSelectUI(); }

    public void OnSelectFirstTo3() { selectedFirstTo = 3; UpdateModeSelectUI(); }
    public void OnSelectFirstTo5() { selectedFirstTo = 5; UpdateModeSelectUI(); }
    public void OnSelectFirstTo7() { selectedFirstTo = 7; UpdateModeSelectUI(); }

    void UpdateModeSelectUI()
    {
        if (lbl_modeSelectTitle != null)
            lbl_modeSelectTitle.text = selectedMode.ToString().ToUpper() + " - FIRST TO " + selectedFirstTo;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Random Battle
    // ═══════════════════════════════════════════════════════════════════

    public void OnRandomBattlePressed()
    {
        if (!TryPayEntry()) return;

        HideAllArcadePanels();
        Show(pnl_arcadeWaiting, true);
        if (lbl_waitingStatus != null) lbl_waitingStatus.text = "SEARCHING...";

        LobbyManager.Instance?.EnterRandomQueue(selectedMode, selectedFirstTo);
    }

    public void OnCancelSearchPressed()
    {
        RefundEntry();
        LobbyManager.Instance?.LeaveRandomQueue();

        // The bot fallback may already have kicked in while this panel was up
        if (inBotMatch) BotMatchManager.Instance?.Cleanup();

        ShowModeSelect();
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Lobby (Online Users + Invite)
    // ═══════════════════════════════════════════════════════════════════

    public void OnShowLobbyPressed()
    {
        HideAllArcadePanels();
        Show(pnl_lobby, true);

        LobbyManager.Instance?.StartListeningOnlineUsers();
        LobbyManager.Instance?.StartListeningInvites();
        LobbyManager.Instance?.LoadFriends();
    }

    public void OnLobbyBackPressed()
    {
        LobbyManager.Instance?.StopListeningOnlineUsers();
        ShowModeSelect();
    }

    public void OnSearchChanged()
    {
        if (inp_search == null) return;
        string query = inp_search.text;
        var results = LobbyManager.Instance?.SearchUsers(query);
        if (results != null)
            PopulateUserList(results);
    }

    void RefreshUserList(List<OnlineUser> users)
    {
        PopulateUserList(users);
        if (lbl_lobbyStatus != null)
            lbl_lobbyStatus.text = users.Count + " ONLINE";
    }

    void PopulateUserList(List<OnlineUser> users)
    {
        if (userListContent == null) return;

        // Clear existing rows
        for (int i = userListContent.childCount - 1; i >= 0; i--)
            Destroy(userListContent.GetChild(i).gameObject);

        foreach (var user in users)
        {
            GameObject row;
            if (userRowPrefab != null)
                row = Instantiate(userRowPrefab, userListContent);
            else
            {
                row = new GameObject("UserRow", typeof(RectTransform));
                row.transform.SetParent(userListContent, false);
            }
            row.SetActive(true);

            // Set name label
            var nameLbl = row.transform.Find("lbl_name")?.GetComponent<Text>();
            if (nameLbl != null) nameLbl.text = user.displayName;

            // Invite button
            var inviteBtn = row.transform.Find("btn_invite/btn_face")?.GetComponent<Button>();
            if (inviteBtn != null)
            {
                string uid = user.uid;
                inviteBtn.onClick.AddListener(() => SendInviteToUser(uid));
            }

            // Add friend button
            var addBtn = row.transform.Find("btn_add_friend/btn_face")?.GetComponent<Button>();
            if (addBtn != null)
            {
                string uid = user.uid;
                string uname = user.displayName;
                bool alreadyFriend = user.isFriend;

                // Change label if already friend
                var addLbl = row.transform.Find("btn_add_friend/btn_face/lbl_btn")?.GetComponent<Text>();
                if (addLbl != null) addLbl.text = alreadyFriend ? "FRIEND" : "+";

                if (!alreadyFriend)
                    addBtn.onClick.AddListener(() => OnAddFriendPressed(uid, uname));
                else
                    addBtn.interactable = false;
            }
        }
    }

    void RefreshFriendList(List<OnlineUser> friends)
    {
        PopulateFriendList(friends);
        if (lbl_friendsStatus != null)
            lbl_friendsStatus.text = friends.Count + (friends.Count == 1 ? " FRIEND" : " FRIENDS");
    }

    void PopulateFriendList(List<OnlineUser> friends)
    {
        if (friendListContent == null) return;

        for (int i = friendListContent.childCount - 1; i >= 0; i--)
            Destroy(friendListContent.GetChild(i).gameObject);

        foreach (var friend in friends)
        {
            GameObject row;
            if (friendRowPrefab != null)
                row = Instantiate(friendRowPrefab, friendListContent);
            else
            {
                row = new GameObject("FriendRow", typeof(RectTransform));
                row.transform.SetParent(friendListContent, false);
            }
            row.SetActive(true);

            // Name
            var nameLbl = row.transform.Find("lbl_name")?.GetComponent<Text>();
            if (nameLbl != null) nameLbl.text = friend.displayName;

            // Online status indicator
            var statusImg = row.transform.Find("img_status")?.GetComponent<Image>();
            if (statusImg != null)
                statusImg.color = friend.isOnline ? new Color(0.3f, 0.87f, 0.37f) : new Color(0.4f, 0.4f, 0.4f);

            // Status text
            var statusLbl = row.transform.Find("lbl_status")?.GetComponent<Text>();
            if (statusLbl != null)
                statusLbl.text = friend.isOnline ? "ONLINE" : "OFFLINE";

            // Invite button (only if online)
            var inviteBtn = row.transform.Find("btn_invite/btn_face")?.GetComponent<Button>();
            if (inviteBtn != null)
            {
                if (friend.isOnline)
                {
                    string uid = friend.uid;
                    inviteBtn.onClick.AddListener(() => SendInviteToUser(uid));
                }
                else
                {
                    inviteBtn.interactable = false;
                    var invLbl = row.transform.Find("btn_invite/btn_face/lbl_btn")?.GetComponent<Text>();
                    if (invLbl != null) invLbl.color = new Color(0.3f, 0.3f, 0.3f);
                }
            }

            // Remove friend button
            var removeBtn = row.transform.Find("btn_remove/btn_face")?.GetComponent<Button>();
            if (removeBtn != null)
            {
                string uid = friend.uid;
                removeBtn.onClick.AddListener(() => OnRemoveFriendPressed(uid));
            }
        }
    }

    void OnAddFriendPressed(string uid, string name)
    {
        LobbyManager.Instance?.AddFriend(uid, name);
    }

    void OnRemoveFriendPressed(string uid)
    {
        LobbyManager.Instance?.RemoveFriend(uid);
    }

    void SendInviteToUser(string uid)
    {
        if (!TryPayEntry()) return;

        LobbyManager.Instance?.SendInvite(uid, selectedMode, selectedFirstTo);

        HideAllArcadePanels();
        Show(pnl_arcadeWaiting, true);
        if (lbl_waitingStatus != null) lbl_waitingStatus.text = "WAITING FOR RESPONSE...";
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Entry fee
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Takes the entry fee, or opens the top-up sheet and refuses. Random is
    /// charged at the Medium rate because the real difficulty is not decided
    /// until the match starts.
    /// </summary>
    bool TryPayEntry()
    {
        var stats = PlayerStatsManager.Instance;
        if (stats == null) return true;   // no economy loaded yet — never block

        GameMode feeMode = selectedMode == GameMode.Random ? GameMode.Medium : selectedMode;
        int fee = PlayerStatsManager.EntryFee(feeMode);

        // A newcomer must never be priced out before they know the game
        if (stats.HasNewPlayerShield) { pendingFee = 0; return true; }

        if (stats.TrySpendCoins(fee)) { pendingFee = fee; return true; }

        // Broke: the day's free entries are what make "you can always play"
        // true for ranked as well, not only for training
        if (stats.TryUseFreeEntry()) { pendingFee = 0; return true; }

        ShowNoCoins(fee, stats.Coins);
        return false;
    }

    /// <summary>Give the fee back — the player never reached a match.</summary>
    void RefundEntry()
    {
        if (pendingFee <= 0) return;

        PlayerStatsManager.Instance?.AddCoins(pendingFee);
        pendingFee = 0;
    }

    void ShowNoCoins(int needed, int have)
    {
        HideAllArcadePanels();
        Show(pnl_noCoins, true);

        if (lbl_noCoins_detail != null)
        {
            int free = PlayerStatsManager.Instance != null
                ? PlayerStatsManager.Instance.FreeEntriesRemaining : 0;

            lbl_noCoins_detail.text = free > 0
                ? "NEED " + needed + "  ·  YOU HAVE " + have + "  ·  " + free + " FREE LEFT"
                : "NEED " + needed + "  ·  YOU HAVE " + have;
        }

        // The ad is an accelerator, never the only door. TRAINING is free and
        // the daily bonus lands tomorrow regardless, so this sheet always has
        // a way out even when no ad can be served.
        bool adReady = AdManager.Instance != null && AdManager.Instance.IsRewardedReady;
        Show(btn_watchAd, adReady);

        if (lbl_watchAd != null && adReady)
            lbl_watchAd.text = "WATCH AD  +" + AD_REWARD;
    }

    /// <summary>
    /// Deliberately below the 50-coin day-1 login bonus. An ad that pays more
    /// than showing up teaches the player to skip the daily hook.
    /// </summary>
    public const int AD_REWARD = 30;

    public void OnWatchAdPressed()
    {
        var ads = AdManager.Instance;
        if (ads == null || !ads.IsRewardedReady) return;

        if (lbl_watchAd != null) lbl_watchAd.text = "LOADING...";

        ads.ShowRewarded(watched =>
        {
            // Only a completed view pays. Rewarding a dismissal is against every
            // ad network's policy and is how accounts get suspended.
            if (watched) PlayerStatsManager.Instance?.AddCoins(AD_REWARD);

            var stats = PlayerStatsManager.Instance;
            if (stats == null) return;

            GameMode feeMode = selectedMode == GameMode.Random ? GameMode.Medium : selectedMode;
            ShowNoCoins(PlayerStatsManager.EntryFee(feeMode), stats.Coins);
        });
    }

    /// <summary>Free mode — the guaranteed exit from the out-of-coins sheet.</summary>
    public void OnPlayTrainingPressed()
    {
        Show(pnl_noCoins, false);
        HideAllArcadePanels();

        var gui = FindObjectOfType<GUIManager>();
        if (gui != null) gui.OnPlayPressed();
    }

    public void OnNoCoinsBackPressed()
    {
        Show(pnl_noCoins, false);
        ShowModeSelect();
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Invite Popup (received invite)
    // ═══════════════════════════════════════════════════════════════════

    void ShowInvitePopup(InviteData invite)
    {
        currentInvite = invite;
        Show(pnl_invitePopup, true);

        if (lbl_inviteFrom != null) lbl_inviteFrom.text = invite.fromName;
        if (lbl_inviteMode != null) lbl_inviteMode.text = invite.mode.ToString().ToUpper() + " - FIRST TO " + invite.firstTo;
    }

    public void OnAcceptInvitePressed()
    {
        if (currentInvite == null) return;
        Show(pnl_invitePopup, false);

        LobbyManager.Instance?.AcceptInvite(currentInvite);
        currentInvite = null;
    }

    public void OnDeclineInvitePressed()
    {
        if (currentInvite == null) return;
        Show(pnl_invitePopup, false);

        LobbyManager.Instance?.DeclineInvite(currentInvite);
        currentInvite = null;
    }

    void OnInviteDeclined()
    {
        RefundEntry();
        if (lbl_waitingStatus != null) lbl_waitingStatus.text = "INVITE DECLINED";
        StartCoroutine(ReturnToModeSelectAfterDelay(2f));
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Match Flow
    // ═══════════════════════════════════════════════════════════════════

    void OnMatchCreated(string matchId)
    {
        inBotMatch = false;
        resultRecorded = false;
        pendingFee = 0;   // the match exists; the fee is now genuinely spent

        HideAllArcadePanels();
        Show(pnl_arcadeWaiting, true);
        if (lbl_waitingStatus != null) lbl_waitingStatus.text = "JOINING MATCH...";

        // Stop lobby listeners
        LobbyManager.Instance?.StopListeningOnlineUsers();
        LobbyManager.Instance?.LeaveRandomQueue();

        ArcadeMatchManager.Instance?.JoinMatch(matchId);
    }

    /// <summary>
    /// Nobody was in the queue. Rather than leave the player waiting on an
    /// empty lobby, hand them a bot opponent.
    /// </summary>
    void OnBotFallback(GameMode mode, int firstTo)
    {
        if (BotMatchManager.Instance == null)
        {
            ShowError("No opponents available");
            StartCoroutine(ReturnToModeSelectAfterDelay(2f));
            return;
        }

        inBotMatch = true;
        resultRecorded = false;
        pendingFee = 0;

        HideAllArcadePanels();
        Show(pnl_arcadeWaiting, true);
        if (lbl_waitingStatus != null) lbl_waitingStatus.text = "OPPONENT FOUND!";

        LobbyManager.Instance?.StopListeningOnlineUsers();

        BotMatchManager.Instance.StartBotMatch(mode, firstTo);
    }

    void OnOpponentJoined(string name)
    {
        if (lbl_waitingStatus != null) lbl_waitingStatus.text = name + " JOINED!";
    }

    void OnEquationReady()
    {
        HideAllArcadePanels();
        Show(pnl_arcadeHUD, true);

        // Show the main game panel (GUIManager's pnl_main)
        var gui = FindObjectOfType<GUIManager>();
        if (gui != null && gui.pnl_main != null)
            gui.pnl_main.SetActive(true);

        UpdateHUD();

        // Start arcade timer
        var timer = FindObjectOfType<TimerManager>();
        if (timer != null) timer.StartArcadeTimer();
    }

    void UpdateHUD()
    {
        if (lbl_myScore != null) lbl_myScore.text = CurMyScore.ToString();
        if (lbl_oppScore != null) lbl_oppScore.text = CurOppScore.ToString();
        if (lbl_roundInfo != null) lbl_roundInfo.text = "ROUND " + CurRound;
        if (lbl_oppName != null) lbl_oppName.text = CurOppName?.ToUpper() ?? "OPPONENT";
    }

    void UpdateScoreDisplay(int myScore, int oppScore)
    {
        if (lbl_myScore != null) lbl_myScore.text = myScore.ToString();
        if (lbl_oppScore != null) lbl_oppScore.text = oppScore.ToString();
    }

    void ShowRoundResult(int round, bool iWon)
    {
        // Hide main game panel
        var gui = FindObjectOfType<GUIManager>();
        if (gui != null && gui.pnl_main != null)
            gui.pnl_main.SetActive(false);

        // Show round overlay
        if (pnl_roundOverlay != null)
        {
            pnl_roundOverlay.SetActive(true);
            if (lbl_roundResult != null)
                lbl_roundResult.text = iWon ? "ROUND WON!" : "ROUND LOST";
        }

        // Whoever lost the race still gets to see what the answer was
        if (lbl_roundAnswer != null)
            lbl_roundAnswer.text = GameManager.Instance != null
                ? GameManager.Instance.correctSolution : "";

        PlayerStatsManager.Instance?.RecordRound(iWon);

        UpdateHUD();
        StartCoroutine(HideRoundOverlayAfterDelay(2.6f));
    }

    IEnumerator HideRoundOverlayAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        Show(pnl_roundOverlay, false);
    }

    void ShowMatchResult(bool iWon)
    {
        HideAllArcadePanels();
        Show(pnl_arcadeResult, true);

        // Hide main game panel
        var gui = FindObjectOfType<GUIManager>();
        if (gui != null && gui.pnl_main != null)
            gui.pnl_main.SetActive(false);

        // The deciding round skips the round overlay, so this is the only
        // place its answer is ever shown.
        if (lbl_resultAnswer != null)
            lbl_resultAnswer.text = GameManager.Instance != null
                ? GameManager.Instance.correctSolution : "";

        if (lbl_resultTitle != null) lbl_resultTitle.text = iWon ? "YOU WIN!" : "YOU LOSE";
        if (lbl_resultScore != null) lbl_resultScore.text = CurMyScore + " - " + CurOppScore;
        if (lbl_resultDetail != null)
            lbl_resultDetail.text = "VS " + (CurOppName?.ToUpper() ?? "OPPONENT");

        // Reset the rematch controls for this result screen
        Show(btn_rematch, true);
        if (lbl_rematchStatus != null) lbl_rematchStatus.text = "";

        // Guard against double-counting: OnOpponentDisconnected can arrive
        // alongside a regular match-end event.
        if (!resultRecorded)
        {
            resultRecorded = true;

            // Blank until the new rating lands — against a human the opponent's
            // rating is fetched asynchronously, so showing the old delta here
            // would flash the previous match's result.
            if (lbl_resultElo != null) lbl_resultElo.text = "";

            string oppUid = inBotMatch ? null : ArcadeMatchManager.Instance?.OpponentUid;
            PlayerStatsManager.Instance?.RecordMatchResult(iWon, oppUid, inBotMatch, CurMode);
        }
        else
        {
            RefreshResultElo();
        }
    }

    /// <summary>Fills in the rating line once the async ELO update lands.</summary>
    void RefreshResultElo()
    {
        if (lbl_resultElo == null) return;
        if (pnl_arcadeResult == null || !pnl_arcadeResult.activeSelf) return;

        var stats = PlayerStatsManager.Instance;
        if (stats == null) { lbl_resultElo.text = ""; return; }

        int delta = stats.LastEloDelta;
        string sign = delta >= 0 ? "+" : "";

        lbl_resultElo.text = sign + delta + "   ·   " + stats.Elo + " " + stats.RankName;
        lbl_resultElo.color = delta >= 0
            ? new Color(0.46f, 1f, 0.01f)
            : new Color(1f, 0.09f, 0.27f);
    }

    void OnOpponentDisconnected()
    {
        ShowMatchResult(true);
        if (lbl_resultDetail != null) lbl_resultDetail.text = "OPPONENT DISCONNECTED";

        // There is nobody left to rematch with
        Show(btn_rematch, false);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Rematch
    // ═══════════════════════════════════════════════════════════════════

    public void OnRematchPressed()
    {
        // A rematch is a new match and costs the same as any other. Without
        // this the fee was a one-off: bots always accept, so a single payment
        // bought an unlimited session.
        if (!TryPayEntry()) return;

        // Bots never decline, so a rematch starts straight away
        if (inBotMatch)
        {
            resultRecorded = false;
            HideAllArcadePanels();
            Show(pnl_arcadeWaiting, true);
            if (lbl_waitingStatus != null) lbl_waitingStatus.text = "REMATCH...";

            pendingFee = 0;   // a match starts immediately; the fee is spent
            BotMatchManager.Instance?.Rematch();
            return;
        }

        var match = ArcadeMatchManager.Instance;
        if (match == null || !match.MatchEnded)
        {
            ShowModeSelect();
            return;
        }

        Show(btn_rematch, false);
        if (lbl_rematchStatus != null)
            lbl_rematchStatus.text = "WAITING FOR OPPONENT...";

        match.RequestRematch();
    }

    void OnOpponentWantsRematch()
    {
        if (lbl_rematchStatus != null)
            lbl_rematchStatus.text = (CurOppName?.ToUpper() ?? "OPPONENT") + " WANTS A REMATCH!";
    }

    void OnRematchDeclined()
    {
        Show(btn_rematch, false);
        if (lbl_rematchStatus != null) lbl_rematchStatus.text = "OPPONENT LEFT";
    }

    void OnRematchTimedOut()
    {
        Show(btn_rematch, false);
        if (lbl_rematchStatus != null) lbl_rematchStatus.text = "NO RESPONSE";
    }

    void OnRematchStarting()
    {
        resultRecorded = false;

        HideAllArcadePanels();
        Show(pnl_arcadeWaiting, true);
        if (lbl_waitingStatus != null) lbl_waitingStatus.text = "REMATCH...";
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Result Panel Buttons
    // ═══════════════════════════════════════════════════════════════════

    public void OnReturnToLobbyPressed()
    {
        LeaveCurrentMatch();
        ShowModeSelect();
    }

    public void OnArcadeBackToMenu()
    {
        LeaveCurrentMatch();
        LobbyManager.Instance?.StopListeningOnlineUsers();
        LobbyManager.Instance?.StopListeningInvites();
        LobbyManager.Instance?.LeaveRandomQueue();
        HideAllArcadePanels();

        // Return to start screen
        var gui = FindObjectOfType<GUIManager>();
        if (gui != null) gui.ShowStartScreen();
    }

    void LeaveCurrentMatch()
    {
        if (inBotMatch)
        {
            BotMatchManager.Instance?.Cleanup();
            inBotMatch = false;
            return;
        }

        // Tell a waiting opponent we're not rematching before tearing down
        var match = ArcadeMatchManager.Instance;
        if (match != null && match.MatchEnded) match.DeclineRematch();
        match?.Cleanup();
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Helpers
    // ═══════════════════════════════════════════════════════════════════

    void HideAllArcadePanels()
    {
        Show(pnl_arcadeModeSelect, false);
        Show(pnl_lobby, false);
        Show(pnl_arcadeWaiting, false);
        Show(pnl_arcadeHUD, false);
        Show(pnl_arcadeResult, false);
        Show(pnl_invitePopup, false);
        Show(pnl_roundOverlay, false);
        Show(pnl_noCoins, false);
    }

    public void HideAllPanels()
    {
        HideAllArcadePanels();
    }

    void ShowError(string msg)
    {
        // Whatever went wrong, the player never reached a match — give it back
        RefundEntry();

        Debug.LogError("Arcade: " + msg);
        if (lbl_waitingStatus != null && pnl_arcadeWaiting.activeSelf)
            lbl_waitingStatus.text = "ERROR: " + msg;
    }

    IEnumerator ReturnToModeSelectAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        ShowModeSelect();
    }

    void OnDestroy()
    {
        var lobby = LobbyManager.Instance;
        if (lobby != null)
        {
            lobby.OnOnlineUsersUpdated -= RefreshUserList;
            lobby.OnFriendsUpdated -= RefreshFriendList;
            lobby.OnInviteReceived -= ShowInvitePopup;
            lobby.OnMatchFound -= OnMatchCreated;
            lobby.OnInviteAccepted -= OnMatchCreated;
            lobby.OnInviteDeclined -= OnInviteDeclined;
            lobby.OnBotFallback -= OnBotFallback;
            lobby.OnError -= ShowError;
        }

        var match = ArcadeMatchManager.Instance;
        if (match != null)
        {
            match.OnScoreUpdated -= UpdateScoreDisplay;
            match.OnRoundResult -= ShowRoundResult;
            match.OnMatchResult -= ShowMatchResult;
            match.OnEquationReady -= OnEquationReady;
            match.OnOpponentDisconnected -= OnOpponentDisconnected;
            match.OnOpponentJoined -= OnOpponentJoined;
            match.OnError -= ShowError;

            match.OnRematchRequestedByOpponent -= OnOpponentWantsRematch;
            match.OnRematchDeclined -= OnRematchDeclined;
            match.OnRematchTimedOut -= OnRematchTimedOut;
            match.OnRematchStarting -= OnRematchStarting;
        }

        var bot = BotMatchManager.Instance;
        if (bot != null)
        {
            bot.OnScoreUpdated -= UpdateScoreDisplay;
            bot.OnRoundResult -= ShowRoundResult;
            bot.OnMatchResult -= ShowMatchResult;
            bot.OnEquationReady -= OnEquationReady;
            bot.OnOpponentJoined -= OnOpponentJoined;
            bot.OnError -= ShowError;
        }

        if (PlayerStatsManager.Instance != null)
            PlayerStatsManager.Instance.OnStatsChanged -= RefreshResultElo;
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

## `Assets/Scripts/VolumeControl.cs`

```csharp
using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// The corner audio key and the stepped popover it opens.
///
/// The old control could only mute, so the player could turn the game off but
/// never down. This one drives the master level through six discrete states and
/// reports the current one on the key itself, as a column of lit cells — the
/// same visual language as the 7-segment digits the game is made of.
///
/// Discrete steps rather than a slider: the whole game is tapping, a drag in the
/// top corner of a tall phone is a bad one-handed reach, and nobody needs 37%.
/// </summary>
public class VolumeControl : MonoBehaviour
{
    public static VolumeControl Instance { get; private set; }

    [Header("Key")]
    public Image   glyphCone;
    public Image   glyphSlash;
    public Image[] keyCells    = new Image[5];   // index 0 = level 1 (bottom)
    public Image[] keyOutlines = new Image[5];
    public CanvasGroup keyGroup;

    [Header("Popover")]
    public GameObject popover;
    public GameObject scrim;
    public CanvasGroup popGroup;
    public Image[] pips        = new Image[5];   // index 0 = level 5 (top)
    public Image[] pipOutlines = new Image[5];
    public Button  btnPlus;
    public Button  btnMinus;

    [Header("Context")]
    public GameObject gameplayPanel;             // pnl_main — key dims over play

    [Header("Look")]
    public Color lit   = new Color(0.463f, 1f, 0.012f);
    public Color unlit = new Color(0.102f, 0.180f, 0.063f);
    public Color dim   = new Color(0.200f, 0.412f, 0.118f);

    [Header("Timing, seconds")]
    public float autoHide  = 2.5f;
    public float openTime  = 0.14f;
    public float closeTime = 0.10f;

    Coroutine hideCo, animCo;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(this); return; }
    }

    void OnEnable()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.OnAudioChanged += Redraw;

        // Draw directly rather than waiting for an event that may already have fired
        Redraw();
    }

    void OnDisable()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.OnAudioChanged -= Redraw;
    }

    void Start()
    {
        if (popover != null) popover.SetActive(false);
        if (scrim != null) scrim.SetActive(false);
        Redraw();
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Open / close
    // ═══════════════════════════════════════════════════════════════════

    public void Toggle()
    {
        if (popover != null && popover.activeSelf) Close();
        else Open();
    }

    public void Open()
    {
        // Without a popover the key degrades to a plain mute toggle
        if (popover == null)
        {
            if (AudioManager.Instance != null) AudioManager.Instance.ToggleMute();
            Redraw();
            return;
        }

        popover.SetActive(true);
        if (scrim != null) scrim.SetActive(true);
        Redraw();

        if (animCo != null) StopCoroutine(animCo);
        animCo = StartCoroutine(OpenAnim());

        RestartAutoHide();
    }

    public void Close()
    {
        if (popover == null || !popover.activeSelf) return;

        if (hideCo != null) { StopCoroutine(hideCo); hideCo = null; }
        if (animCo != null) StopCoroutine(animCo);
        animCo = StartCoroutine(CloseAnim());
    }

    IEnumerator OpenAnim()
    {
        var rt = popover.GetComponent<RectTransform>();

        for (float t = 0; t < openTime; t += Time.unscaledDeltaTime)
        {
            float n = t / openTime;
            if (popGroup != null) popGroup.alpha = n;

            // Small overshoot so it springs out of the key rather than inflating
            float sc = n < 0.75f
                ? Mathf.Lerp(0.88f, 1.06f, n / 0.75f)
                : Mathf.Lerp(1.06f, 1f, (n - 0.75f) / 0.25f);

            if (rt != null) rt.localScale = Vector3.one * sc;
            yield return null;
        }

        if (popGroup != null) popGroup.alpha = 1f;
        if (rt != null) rt.localScale = Vector3.one;
        animCo = null;
    }

    IEnumerator CloseAnim()
    {
        var rt = popover.GetComponent<RectTransform>();

        for (float t = 0; t < closeTime; t += Time.unscaledDeltaTime)
        {
            float n = t / closeTime;
            if (popGroup != null) popGroup.alpha = 1f - n;
            if (rt != null) rt.localScale = Vector3.one * Mathf.Lerp(1f, 0.92f, n);
            yield return null;
        }

        if (rt != null) rt.localScale = Vector3.one;
        popover.SetActive(false);
        if (scrim != null) scrim.SetActive(false);
        animCo = null;
    }

    void RestartAutoHide()
    {
        if (hideCo != null) StopCoroutine(hideCo);
        hideCo = StartCoroutine(AutoHide());
    }

    IEnumerator AutoHide()
    {
        float t = 0f;
        while (t < autoHide) { t += Time.unscaledDeltaTime; yield return null; }

        hideCo = null;
        Close();
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Stepping
    // ═══════════════════════════════════════════════════════════════════

    public void StepUp()   { Step(+1); }
    public void StepDown() { Step(-1); }

    void Step(int delta)
    {
        var am = AudioManager.Instance;
        if (am == null) return;

        am.StepMaster(delta);
        RestartAutoHide();
        Redraw();

        // The player has to HEAR the level they just set. At step 0 PlaySFX
        // early-returns, and that silence is itself the right feedback.
        am.PlaySFX(TickClip());
    }

    static AudioClip s_tick;

    static AudioClip TickClip()
    {
        if (s_tick != null) return s_tick;

        const int rate = 44100;
        const float dur = 0.06f;
        int n = (int)(rate * dur);

        s_tick = AudioClip.Create("volTick", n, 1, rate, false);
        var data = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            data[i] = Mathf.Sin(2f * Mathf.PI * 1100f * t) * (1f - t / dur) * 0.5f;
        }
        s_tick.SetData(data, 0);
        return s_tick;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Drawing
    // ═══════════════════════════════════════════════════════════════════

    public void Redraw()
    {
        var am = AudioManager.Instance;
        if (am == null) return;

        // Gate on IsMuted, not MasterStep: a ToggleMute from anywhere else
        // leaves the step at 5 while the game is silent, and the icon would lie.
        int step = am.IsMuted ? 0 : am.MasterStep;
        bool off = step == 0;

        if (glyphCone != null)  glyphCone.color = off ? dim : lit;
        if (glyphSlash != null) glyphSlash.gameObject.SetActive(off);

        // Key column runs bottom-up: cell i is level i+1
        Paint(keyCells, keyOutlines, step, true);

        // Popover ladder runs top-down: pip i is level STEPS - i
        Paint(pips, pipOutlines, step, false);

        if (btnPlus  != null) btnPlus.interactable  = step < AudioManager.STEPS;
        if (btnMinus != null) btnMinus.interactable = step > 0;

        // Quiet game: the chrome steps back while you are actually playing
        if (keyGroup != null)
            keyGroup.alpha = gameplayPanel != null && gameplayPanel.activeSelf ? 0.7f : 1f;
    }

    void Paint(Image[] cells, Image[] outlines, int step, bool bottomUp)
    {
        if (cells == null) return;

        for (int i = 0; i < cells.Length; i++)
        {
            int level = bottomUp ? i + 1 : AudioManager.STEPS - i;

            if (cells[i] != null)
                cells[i].color = level <= step ? lit : unlit;

            // Outlines appear only at zero, so an all-dark ladder still reads
            // as "switched off" rather than "broken".
            if (outlines != null && i < outlines.Length && outlines[i] != null)
                outlines[i].gameObject.SetActive(step == 0);
        }
    }
}
```

## `Assets/Scripts/AudioManager.cs`

```csharp
using UnityEngine;
using System;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    public AudioSource musicSource;
    public AudioSource sfxSource;

    /// <summary>Raised whenever a level changes, so icons and sliders can redraw.</summary>
    public event Action OnAudioChanged;

    public const int STEPS = 5;

    // Perceptual ladder rather than even 0.2 increments: linear steps sound
    // like four loud settings and one silent one, because loudness is not
    // proportional to amplitude.
    static readonly float[] GAIN = { 0f, 0.06f, 0.20f, 0.40f, 0.66f, 1.00f };

    // Master is the situational control, so it is stepped and the STEP is the
    // stored truth. Music and SFX are a set-once balance, so they stay smooth.
    int   masterStep = STEPS;
    float music = 0.7f;
    float sfx   = 1f;
    bool  muted = false;

    public int   MasterStep  => masterStep;
    public float Master      => GAIN[Mathf.Clamp(masterStep, 0, STEPS)];
    public float MusicVolume => music;
    public float SFXVolume   => sfx;
    public bool  IsMuted     => muted || masterStep == 0;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        music = PlayerPrefs.GetFloat("MusicVolume", 0.7f);
        sfx   = PlayerPrefs.GetFloat("SFXVolume", 1f);
        muted = PlayerPrefs.GetInt("IsMuted", 0) == 1;

        if (PlayerPrefs.HasKey("MasterStep"))
        {
            masterStep = Mathf.Clamp(PlayerPrefs.GetInt("MasterStep"), 0, STEPS);
        }
        else if (PlayerPrefs.HasKey("MasterVolume"))
        {
            // One-time migration from the old continuous master
            masterStep = NearestStep(PlayerPrefs.GetFloat("MasterVolume"));
        }
    }

    void Start()
    {
        Apply();

        // UI built in the same frame subscribes after Awake, so without this
        // the first draw would show stale defaults.
        OnAudioChanged?.Invoke();
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Levels
    // ═══════════════════════════════════════════════════════════════════

    static int NearestStep(float gain)
    {
        int best = 0;
        for (int i = 1; i <= STEPS; i++)
            if (Mathf.Abs(GAIN[i] - gain) < Mathf.Abs(GAIN[best] - gain)) best = i;
        return best;
    }

    public void SetMasterStep(int step)
    {
        masterStep = Mathf.Clamp(step, 0, STEPS);

        // Stepping up off silence is the player asking to hear again
        if (masterStep > 0) muted = false;

        Persist();
    }

    public void StepMaster(int delta) => SetMasterStep(masterStep + delta);

    /// <summary>Float entry point kept for older call sites; snaps to the ladder.</summary>
    public void SetMaster(float value) => SetMasterStep(NearestStep(Mathf.Clamp01(value)));

    public void SetMusicVolume(float value)
    {
        music = Mathf.Clamp01(value);
        Persist();
    }

    public void SetSFXVolume(float value)
    {
        sfx = Mathf.Clamp01(value);
        Persist();
    }

    public void ToggleMute()
    {
        muted = !muted;

        // Un-muting at step 0 would still be silent, so give it something audible
        if (!muted && masterStep == 0) masterStep = 3;

        Persist();
    }

    void Persist()
    {
        Apply();

        PlayerPrefs.SetInt("MasterStep", masterStep);
        PlayerPrefs.SetFloat("MusicVolume", music);
        PlayerPrefs.SetFloat("SFXVolume", sfx);
        PlayerPrefs.SetInt("IsMuted", muted ? 1 : 0);
        PlayerPrefs.Save();

        OnAudioChanged?.Invoke();
    }

    void Apply()
    {
        float g = IsMuted ? 0f : Master;
        if (musicSource) musicSource.volume = music * g;
        if (sfxSource)   sfxSource.volume   = sfx * g;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Playback
    // ═══════════════════════════════════════════════════════════════════

    public void PlaySFX(AudioClip clip)
    {
        if (IsMuted || sfxSource == null || clip == null) return;

        // volumeScale multiplies AudioSource.volume, which Apply() already set
        // to sfx * master. Passing the gain again squared it — at master 0.4
        // effects played at 0.16 of the level the player asked for.
        sfxSource.PlayOneShot(clip, 1f);
    }

    public void PlayMusic(AudioClip clip)
    {
        if (musicSource == null || clip == null) return;
        if (musicSource.clip == clip && musicSource.isPlaying) return;

        musicSource.clip = clip;
        musicSource.loop = true;
        musicSource.volume = IsMuted ? 0f : music * Master;
        musicSource.Play();
    }

    public void StopMusic()
    {
        if (musicSource != null) musicSource.Stop();
    }
}
```

## `Assets/Scripts/AudioSliderBinder.cs`

```csharp
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Binds one Settings slider to an AudioManager channel.
///
/// Kept as a component rather than a persistent UnityEvent because the scene is
/// generated from an editor script, and float-argument listeners do not survive
/// that round trip cleanly.
/// </summary>
[RequireComponent(typeof(Slider))]
public class AudioSliderBinder : MonoBehaviour
{
    public enum Channel { Music, SFX }

    public Channel channel = Channel.Music;
    public Text valueLabel;

    Slider slider;
    bool suppress;

    void Awake()
    {
        slider = GetComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
    }

    void OnEnable()
    {
        slider.onValueChanged.AddListener(OnChanged);

        if (AudioManager.Instance != null)
            AudioManager.Instance.OnAudioChanged += Pull;

        Pull();
    }

    void OnDisable()
    {
        slider.onValueChanged.RemoveListener(OnChanged);

        if (AudioManager.Instance != null)
            AudioManager.Instance.OnAudioChanged -= Pull;
    }

    void OnChanged(float v)
    {
        if (suppress) return;

        var am = AudioManager.Instance;
        if (am == null) return;

        if (channel == Channel.Music) am.SetMusicVolume(v);
        else                          am.SetSFXVolume(v);

        UpdateLabel(v);
    }

    /// <summary>Refresh from the model without re-entering OnChanged.</summary>
    void Pull()
    {
        var am = AudioManager.Instance;
        if (am == null || slider == null) return;

        float v = channel == Channel.Music ? am.MusicVolume : am.SFXVolume;

        suppress = true;
        slider.value = v;
        suppress = false;

        UpdateLabel(v);
    }

    void UpdateLabel(float v)
    {
        if (valueLabel != null)
            valueLabel.text = Mathf.RoundToInt(v * 100f) + "%";
    }
}
```

## `Assets/Scripts/TapIndicator.cs`

```csharp
using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// The tap marker used by the tutorial: a solid core dot with expanding rings
/// behind it, the convention mobile games use to say "press here".
///
/// It replaces the old pointing hand, which covered the very segment the player
/// was meant to watch light up. The rings expand outward from the target and
/// fade, so the target itself is never hidden.
///
/// Build it with TapIndicator.Create(...) or wire the fields from a scene builder.
/// </summary>
public class TapIndicator : MonoBehaviour
{
    [Header("Layers (core drawn on top of rings)")]
    public Image core;
    public Image[] rings;

    [Header("Timing, seconds")]
    public float ringDuration = 0.85f;   // one ring's full expand-and-fade
    public float ringStagger  = 0.28f;   // delay between successive rings
    public float pressDuration = 0.13f;  // core dip on contact
    public float fadeDuration  = 0.16f;  // show / hide

    [Header("Geometry, multiples of the core size")]
    public float ringStartScale = 0.55f;
    public float ringEndScale   = 2.6f;
    public float corePressScale = 0.72f;

    [Header("Look")]
    public Color tint = new Color(1f, 1f, 1f, 1f);
    [Range(0f, 1f)] public float coreAlpha = 0.9f;
    [Range(0f, 1f)] public float ringAlpha = 0.55f;

    Coroutine loop;
    RectTransform rt;

    void Awake()
    {
        rt = GetComponent<RectTransform>();

        // Only clear the layers here. Deactivating the root in Awake would fight
        // ShowAt, which activates the object immediately before starting the
        // coroutine — Awake runs in between and would switch it back off.
        ClearLayers();
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Public API
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>Place the indicator over a target and start rippling.</summary>
    public void ShowAt(RectTransform target)
    {
        if (target == null) return;
        if (rt == null) rt = GetComponent<RectTransform>();

        rt.position = target.position;
        gameObject.SetActive(true);

        if (loop != null) StopCoroutine(loop);
        loop = StartCoroutine(RippleLoop());
    }

    public void Hide()
    {
        if (loop != null) { StopCoroutine(loop); loop = null; }
        ClearLayers();
        gameObject.SetActive(false);
    }

    /// <summary>Plays the contact beat — call it the moment the tap registers.</summary>
    public IEnumerator PlayPress()
    {
        if (core == null) yield break;

        var t0 = core.rectTransform.localScale;
        float half = pressDuration * 0.5f;

        for (float t = 0; t < half; t += Time.deltaTime)
        {
            float n = t / half;
            core.rectTransform.localScale = Vector3.one * Mathf.Lerp(1f, corePressScale, n);
            yield return null;
        }
        for (float t = 0; t < half; t += Time.deltaTime)
        {
            float n = t / half;
            // Slight overshoot on release reads as a real button press
            core.rectTransform.localScale = Vector3.one * Mathf.Lerp(corePressScale, 1.08f, n);
            yield return null;
        }

        core.rectTransform.localScale = Vector3.one;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Animation
    // ═══════════════════════════════════════════════════════════════════

    IEnumerator RippleLoop()
    {
        ClearLayers();

        // Fade the core in rather than popping it
        if (core != null)
        {
            for (float t = 0; t < fadeDuration; t += Time.deltaTime)
            {
                SetAlpha(core, Mathf.Lerp(0f, coreAlpha, t / fadeDuration));
                yield return null;
            }
            SetAlpha(core, coreAlpha);
        }

        if (rings != null)
            for (int i = 0; i < rings.Length; i++)
                if (rings[i] != null)
                    StartCoroutine(RingWave(rings[i], i * ringStagger));

        yield return null;
    }

    IEnumerator RingWave(Image ring, float delay)
    {
        yield return new WaitForSeconds(delay);

        var rrt = ring.rectTransform;

        while (true)
        {
            for (float t = 0; t < ringDuration; t += Time.deltaTime)
            {
                float n = t / ringDuration;

                // Fast out, slow in: the wave leaps away then coasts, which is
                // what makes it read as energy leaving the contact point.
                float e = 1f - Mathf.Pow(1f - n, 3f);

                rrt.localScale = Vector3.one * Mathf.Lerp(ringStartScale, ringEndScale, e);
                SetAlpha(ring, Mathf.Lerp(ringAlpha, 0f, n * n));
                yield return null;
            }
        }
    }

    /// <summary>Reset every layer to fully transparent without touching the root.</summary>
    void ClearLayers()
    {
        if (core != null)
        {
            core.gameObject.SetActive(true);
            core.rectTransform.localScale = Vector3.one;
            SetAlpha(core, 0f);
        }

        if (rings != null)
            foreach (var r in rings)
                if (r != null)
                {
                    r.gameObject.SetActive(true);
                    r.rectTransform.localScale = Vector3.one * ringStartScale;
                    SetAlpha(r, 0f);
                }
    }

    void SetAlpha(Image img, float a)
    {
        if (img == null) return;
        img.color = new Color(tint.r, tint.g, tint.b, a);
    }
}
```

## `Assets/Scripts/TutorialAnimator.cs`

```csharp
using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class TutorialAnimator : MonoBehaviour
{
    public Line[] segsToTap;        // real Line segments to tap (in order)
    public Line[] allSegs;          // all segments (to reset)
    public TapIndicator tap;        // ripple marker shown over the segment to press
    public Text lblHint;            // popup hint text
    public Image hintBg;            // hint background
    public Text lblCongrats;        // congrats text

    string[] hintMessages = {
        "Tap segments to\nbuild the first number!",
        "Tap the operator key\nto switch + and -",
        "Build the\nsecond number!",
        "Complete the\nanswer below the line!",
        "CORRECT!"
    };

    void OnEnable()
    {
        StartCoroutine(RunDemo());
    }

    void OnDisable()
    {
        StopAllCoroutines();
    }

    IEnumerator RunDemo()
    {
        while (true)
        {
            // ALL segments visible: tappable = Active (amber), non-tappable = Inactive (dark amber)
            // First set ALL to Inactive (dark but visible housing)
            if (allSegs != null)
                foreach (var s in allSegs)
                    if (s) { s.SetInactive(); s.transform.localScale = Vector3.one; }

            // Then set tappable ones to Active (brighter amber)
            if (segsToTap != null)
                foreach (var s in segsToTap)
                    if (s) { s.SetActive(); s.transform.localScale = Vector3.one; }

            // Operators own their own visual state, so clear them explicitly
            foreach (var pm in GetComponentsInChildren<PlusMinus>(true))
                pm.ResetToggle();

            if (tap) tap.Hide();
            if (lblCongrats) lblCongrats.gameObject.SetActive(false);
            ShowHint("Watch how to solve!", true);

            yield return new WaitForSeconds(2f);

            // Tap each segment with animation
            int phase = -1;
            for (int i = 0; i < segsToTap.Length; i++)
            {
                if (segsToTap[i] == null) continue;

                // Determine phase for hint
                int newPhase = GetPhase(i);
                if (newPhase != phase)
                {
                    phase = newPhase;
                    ShowHint(hintMessages[Mathf.Min(phase, hintMessages.Length - 1)], true);
                    yield return new WaitForSeconds(0.5f);
                }

                var target = segsToTap[i].GetComponent<RectTransform>();

                // Ripple over the target long enough for the eye to land on it
                if (tap) tap.ShowAt(target);
                yield return new WaitForSeconds(0.5f);

                // Contact beat, then the segment lights under it
                if (tap) yield return tap.PlayPress();

                var pmOwner = segsToTap[i].GetComponentInParent<PlusMinus>();
                if (pmOwner != null) pmOwner.SetPlus();   // the key sets both bars
                else                 segsToTap[i].SetSelected();

                PlayClickSound();

                // Let the lit segment be seen before the marker leaves
                yield return new WaitForSeconds(0.12f);
                if (tap) tap.Hide();

                yield return new WaitForSeconds(0.14f);
            }

            // Success!
            if (tap) tap.Hide();
            ShowHint("", false);

            if (lblCongrats)
            {
                lblCongrats.gameObject.SetActive(true);
                lblCongrats.text = "CORRECT!";
            }

            // Flash all active segments
            for (int f = 0; f < 4; f++)
            {
                foreach (var s in segsToTap)
                    if (s) s.SetSelected();
                yield return new WaitForSeconds(0.2f);
                foreach (var s in segsToTap)
                    if (s) s.SetActive();
                yield return new WaitForSeconds(0.2f);
            }

            yield return new WaitForSeconds(2f);
            if (lblCongrats) lblCongrats.gameObject.SetActive(false);
            yield return new WaitForSeconds(1f);
        }
    }

    // Which phase based on segment index (customize per equation)
    int GetPhase(int idx)
    {
        if (idx < 7)  return 0;     // first number segments
        if (idx < 8)  return 1;     // operator — a single key since the rewrite
        if (idx < 18) return 2;     // second number
        return 3;                   // answer
    }

    void ShowHint(string text, bool show)
    {
        if (lblHint) { lblHint.text = text; lblHint.gameObject.SetActive(show); }
        if (hintBg) hintBg.gameObject.SetActive(show);
    }

    void PlayClickSound()
    {
        if (AudioManager.Instance == null || AudioManager.Instance.IsMuted) return;
        const int sampleRate = 44100;
        const float duration = 0.1f;
        int samples = (int)(sampleRate * duration);
        var clip = AudioClip.Create("tutClick", samples, 1, sampleRate, false);
        float[] data = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)sampleRate;
            float freq = 800f * Mathf.Exp(-t * 10f);
            data[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * (1f - t / duration);
        }
        clip.SetData(data, 0);
        AudioManager.Instance.PlaySFX(clip);
    }

}
```

## `Assets/Scripts/SafeAreaFitter.cs`

```csharp
using UnityEngine;

/// <summary>
/// Keeps its RectTransform inside the device's safe area — the region not
/// covered by a notch, camera cutout, rounded corner or gesture bar.
///
/// Put this on a full-screen container and parent the interactive UI to it.
/// Backgrounds should stay outside it so artwork still bleeds to the edges.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class SafeAreaFitter : MonoBehaviour
{
    RectTransform rt;

    Rect lastSafeArea;
    int  lastWidth;
    int  lastHeight;

    void Awake()
    {
        rt = GetComponent<RectTransform>();
        Apply();
    }

    void Update()
    {
        // Rotation and resolution changes both move the safe area, and neither
        // raises an event we can hook, so it is polled.
        if (Screen.safeArea == lastSafeArea
            && Screen.width == lastWidth
            && Screen.height == lastHeight) return;

        Apply();
    }

    void Apply()
    {
        if (rt == null) rt = GetComponent<RectTransform>();
        if (Screen.width <= 0 || Screen.height <= 0) return;

        Rect safe = Screen.safeArea;
        lastSafeArea = safe;
        lastWidth = Screen.width;
        lastHeight = Screen.height;

        Vector2 min = safe.position;
        Vector2 max = safe.position + safe.size;

        min.x /= Screen.width;
        min.y /= Screen.height;
        max.x /= Screen.width;
        max.y /= Screen.height;

        // A malformed safe area (some emulators report one) would collapse the
        // whole UI, so fall back to full screen instead.
        if (min.x < 0f || min.y < 0f || max.x > 1f || max.y > 1f
            || max.x - min.x < 0.5f || max.y - min.y < 0.5f)
        {
            min = Vector2.zero;
            max = Vector2.one;
        }

        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
```

## `Assets/Scripts/ScrollablePanel.cs`

```csharp
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Makes a full-screen panel scrollable when content doesn't fit (landscape).
/// Attach to the panel GameObject. Content height is set to referenceHeight (portrait design height).
/// In portrait, no scrolling (content fits). In landscape, vertical scrolling enabled.
/// </summary>
public class ScrollablePanel : MonoBehaviour
{
    [Tooltip("Height of the content in reference pixels (portrait design)")]
    public float referenceHeight = 1920f;

    ScrollRect scrollRect;
    RectTransform contentRect;
    bool setup;

    void OnEnable()
    {
        if (!setup) Setup();

        ResizeContent();

        // Reset scroll position
        if (scrollRect != null && scrollRect.content != null)
            scrollRect.content.anchoredPosition = Vector2.zero;
    }

    void OnRectTransformDimensionsChange()
    {
        if (setup) ResizeContent();
    }

    /// <summary>
    /// The content box must never be shorter than the panel itself. A fixed
    /// 1920 box on a 2120-tall screen left a dead strip along the bottom and
    /// pulled every bottom-anchored child up off the screen edge.
    /// </summary>
    void ResizeContent()
    {
        if (contentRect == null) return;

        var panelRt = GetComponent<RectTransform>();
        float panelHeight = panelRt.rect.height;
        float h = Mathf.Max(referenceHeight, panelHeight);

        if (!Mathf.Approximately(contentRect.sizeDelta.y, h))
            contentRect.sizeDelta = new Vector2(0, h);

        // Only scroll when the design actually overflows the screen
        if (scrollRect != null)
            scrollRect.vertical = h > panelHeight + 1f;
    }

    void Setup()
    {
        setup = true;
        var panelRt = GetComponent<RectTransform>();

        // Create a viewport (this panel becomes the viewport area)
        // We need to reparent all children into a content container

        // Create content holder
        var contentGO = new GameObject("_ScrollContent");
        contentGO.transform.SetParent(transform, false);
        contentRect = contentGO.AddComponent<RectTransform>();
        // Anchor to top, stretch width
        contentRect.anchorMin = new Vector2(0, 1);
        contentRect.anchorMax = new Vector2(1, 1);
        contentRect.pivot = new Vector2(0.5f, 1);
        contentRect.sizeDelta = new Vector2(0, referenceHeight);
        contentRect.anchoredPosition = Vector2.zero;

        // Move all children (except _ScrollContent) into content holder
        var children = new Transform[transform.childCount];
        for (int i = 0; i < transform.childCount; i++)
            children[i] = transform.GetChild(i);

        foreach (var child in children)
        {
            if (child != contentGO.transform)
                child.SetParent(contentGO.transform, false);
        }

        // Add mask to clip content
        var maskImg = GetComponent<Image>();
        if (maskImg == null)
        {
            maskImg = gameObject.AddComponent<Image>();
            maskImg.color = new Color(0, 0, 0, 0);
            maskImg.raycastTarget = true;
        }
        gameObject.AddComponent<RectMask2D>();

        // Add ScrollRect
        scrollRect = gameObject.AddComponent<ScrollRect>();
        scrollRect.content = contentRect;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Elastic;
        scrollRect.elasticity = 0.1f;
        scrollRect.scrollSensitivity = 40f;
        scrollRect.inertia = true;
        scrollRect.decelerationRate = 0.135f;
    }
}
```

## `Assets/Scripts/CanvasOrientationAdapter.cs`

```csharp
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Adjusts CanvasScaler.matchWidthOrHeight based on screen orientation.
/// Portrait: match=0.5 (balanced)
/// Landscape: match=0 (match width → height shrinks → content scrollable)
/// Uses Screen dimensions (not canvas rect) to avoid feedback loop.
/// </summary>
[RequireComponent(typeof(CanvasScaler))]
public class CanvasOrientationAdapter : MonoBehaviour
{
    CanvasScaler scaler;

    void Awake()
    {
        scaler = GetComponent<CanvasScaler>();
    }

    void Update()
    {
        if (scaler == null) return;
        bool landscape = Screen.width > Screen.height;
        scaler.matchWidthOrHeight = landscape ? 0f : 0.5f;
    }
}
```

## `Assets/Scripts/OrientationManager.cs`

```csharp
using UnityEngine;
using System;

/// <summary>
/// Global orientation detection using Canvas dimensions.
/// Other scripts subscribe to OnOrientationChanged.
/// </summary>
public class OrientationManager : MonoBehaviour
{
    public static OrientationManager Instance { get; private set; }

    public event Action<bool> OnOrientationChanged; // true = landscape
    public bool IsLandscape { get; private set; }

    RectTransform canvasRect;
    bool initialized;

    void Awake()
    {
        if (Instance == null) { Instance = this; }
        else { Destroy(this); return; }
    }

    void Update()
    {
        // Find canvas lazily (may not exist at Awake time)
        if (canvasRect == null)
        {
            var canvas = FindFirstObjectByType<Canvas>();
            if (canvas != null)
                canvasRect = canvas.rootCanvas.GetComponent<RectTransform>();
            if (canvasRect == null) return;
        }

        var r = canvasRect.rect;
        bool landscape = r.width > r.height;

        if (!initialized)
        {
            IsLandscape = landscape;
            initialized = true;
            return;
        }

        if (landscape != IsLandscape)
        {
            IsLandscape = landscape;
            Debug.Log($"OrientationManager: {(landscape ? "LANDSCAPE" : "PORTRAIT")} (canvas {r.width}x{r.height})");
            OnOrientationChanged?.Invoke(landscape);
        }
    }
}
```

## `Assets/Scripts/CameraResizer.cs`

```csharp
﻿using UnityEngine;
using System.Collections;

public class CameraResizer : MonoBehaviour
{
    public float resolution = 1.6f;
    public float cameraSize = 4.4f;

	// Use this for initialization
	void Start ()
    {
        float fw = cameraSize * 200f * resolution;
        float ar = (float)Screen.width / (float)Screen.height;
        Camera.main.orthographicSize = fw / (200f * ar);
	}
	
	// Update is called once per frame
	void Update ()
    {
	
	}
}
```

## `Assets/Scripts/ColorManager.cs`

```csharp
using UnityEngine;
using System.Collections;

public class ColorManager : MonoBehaviour
{
    public Color ActiveNumberColor = Color.white;
    public Color PossibleNumberColor = Color.white;
    public Color ImpossibleNumberColor = Color.white;
    public Color ActiveInnerColor = new Color(1f, 1f, 1f, 0.5f);
    public Color PossibleImpossibleInnerColor = new Color(1f, 1f, 1f, 0.2f);
    public Color BackgroundColor = Color.white;
    public Color WinGUIColor = Color.white;
    public Color LoseGUIColor = Color.white;

    void Awake()
    {
        Messenger.AddReceiverListener<Color>(ReceiveMessage.ReceiveActiveNumberColor, () => { return ActiveNumberColor; });
        Messenger.AddReceiverListener<Color>(ReceiveMessage.ReceivePossibleNumberColor, () => { return PossibleNumberColor; });
        Messenger.AddReceiverListener<Color>(ReceiveMessage.ReceiveImpossibleNumberColor, () => { return ImpossibleNumberColor; });
        Messenger.AddReceiverListener<Color>(ReceiveMessage.ReceiveActiveInnerColor, () => { return ActiveInnerColor; });
        Messenger.AddReceiverListener<Color>(ReceiveMessage.ReceivePossibleImpossibleInnerColor, () => { return PossibleImpossibleInnerColor; });
        Messenger.AddReceiverListener<Color>(ReceiveMessage.ReceiveWinGUIColor, () => { return WinGUIColor; });
        Messenger.AddReceiverListener<Color>(ReceiveMessage.ReceiveLoseGUIColor, () => { return LoseGUIColor; });
    }

    void Start()
    {
        Camera.main.backgroundColor = BackgroundColor;
    }
}
```

## `Assets/Scripts/GameSettings.cs`

```csharp
using UnityEngine;

public class GameSettings : MonoBehaviour
{
    public static GameSettings Instance { get; private set; }

    // Segment color options
    public static readonly Color[] SegmentColors = {
        new Color(0.98f, 0.88f, 0.10f),  // 0 Yellow (default)
        new Color(1.00f, 0.27f, 0.14f),  // 1 Red
        new Color(0.26f, 0.52f, 0.96f),  // 2 Blue
        new Color(0.30f, 0.87f, 0.37f),  // 3 Green
        new Color(1.00f, 0.41f, 0.71f),  // 4 Pink
        new Color(0.73f, 0.33f, 0.83f),  // 5 Purple
        new Color(0.55f, 0.36f, 0.96f),  // 6 Violet
        new Color(1.00f, 0.60f, 0.00f),  // 7 Orange
    };

    public static readonly string[] SegmentColorNames = {
        "YELLOW", "RED", "BLUE", "GREEN", "PINK", "PURPLE", "VIOLET", "ORANGE"
    };

    // Background color options
    public static readonly Color[] BackgroundColors = {
        new Color(0.03f, 0.05f, 0.10f),  // 0 Dark Navy (default)
        new Color(0.00f, 0.00f, 0.00f),  // 1 Black
        new Color(0.10f, 0.05f, 0.05f),  // 2 Dark Red
        new Color(0.05f, 0.10f, 0.05f),  // 3 Dark Green
        new Color(0.05f, 0.05f, 0.12f),  // 4 Dark Blue
        new Color(0.10f, 0.05f, 0.10f),  // 5 Dark Purple
        new Color(0.08f, 0.08f, 0.08f),  // 6 Dark Gray
        new Color(0.06f, 0.04f, 0.02f),  // 7 Dark Brown
    };

    public static readonly string[] BackgroundColorNames = {
        "NAVY", "BLACK", "DARK RED", "DARK GREEN", "DARK BLUE", "DARK PURPLE", "DARK GRAY", "DARK BROWN"
    };

    int segColorIndex;
    int bgColorIndex;

    public int SegColorIndex => segColorIndex;
    public int BgColorIndex  => bgColorIndex;

    public Color SelectedSegColor => SegmentColors[segColorIndex];
    public Color ActiveSegColor   => SelectedSegColor * 0.5f;
    public Color InactiveSegColor => SelectedSegColor * 0.12f;
    public Color GlowColor        => new Color(SelectedSegColor.r, SelectedSegColor.g, SelectedSegColor.b, 0.45f);
    public Color SelectedBgColor  => BackgroundColors[bgColorIndex];

    void Awake()
    {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else { Destroy(gameObject); return; }

        segColorIndex = PlayerPrefs.GetInt("SegColor", 0);
        bgColorIndex  = PlayerPrefs.GetInt("BgColor", 0);
    }

    public void SetSegColor(int index)
    {
        segColorIndex = Mathf.Clamp(index, 0, SegmentColors.Length - 1);
        PlayerPrefs.SetInt("SegColor", segColorIndex);
        PlayerPrefs.Save();
        Messenger.Broadcast(Message.StartNewGame); // refresh visuals
    }

    public void SetBgColor(int index)
    {
        bgColorIndex = Mathf.Clamp(index, 0, BackgroundColors.Length - 1);
        PlayerPrefs.SetInt("BgColor", bgColorIndex);
        PlayerPrefs.Save();
    }
}
```

## `Assets/Scripts/SettingsColorPicker.cs`

```csharp
using UnityEngine;
using UnityEngine.UI;

public class SettingsColorPicker : MonoBehaviour
{
    public int colorType;  // 0 = segment, 1 = background
    public int colorIndex;

    void Start()
    {
        var btn = GetComponent<Button>();
        if (btn != null)
            btn.onClick.AddListener(OnClick);
    }

    void OnClick()
    {
        if (GameSettings.Instance == null) return;

        if (colorType == 0)
            GameSettings.Instance.SetSegColor(colorIndex);
        else
            GameSettings.Instance.SetBgColor(colorIndex);
    }
}
```

## `Assets/Scripts/Fader.cs`

```csharp
﻿using UnityEngine;
using System.Collections;
using UnityEngine.UI;

public class Fader : MonoBehaviour
{
    public Image image = null;
    public GameObject panel = null;
    string currentFadeRequestId = null;
    float secInFade = 0f;

    // Awake instead of OnEnable so this works when placed on an always-active
    // manager object while the fader panel itself is toggled separately.
    void Awake()
    {
        Messenger.AddListener<float, string>(Message.OnStartFadeToTransparent, (nrSec, fadeId) => { secInFade = nrSec; currentFadeRequestId = fadeId; StartCoroutine(FadeCo(true)); });
        Messenger.AddListener<float, string>(Message.OnStartFadeToOpaque, (nrSec, fadeId) => { secInFade = nrSec; currentFadeRequestId = fadeId; StartCoroutine(FadeCo(false)); });
    }

    IEnumerator FadeCo(bool toTransparent)
    {
        if (toTransparent == false)
        {
            panel.SetActive(true);
        }

        float timeSoFar = 0f;
        Color transparentBlack = Color.black;
        transparentBlack.a = 0f;
        float ratio = 0f;

        while (true)
        {
            yield return null;
            timeSoFar += Time.unscaledDeltaTime;
            if (timeSoFar > secInFade)
            {
                break;
            }

            if (toTransparent == true)
            {
                ratio = 1f - timeSoFar / secInFade;
            }
            else
            {
                ratio = timeSoFar / secInFade;
            }
            image.color = Color.Lerp(transparentBlack, Color.black, ratio);
        }

        if (toTransparent == true)
        {
            image.color = transparentBlack;
        }
        else
        {
            image.color = Color.black;
        }

        if (toTransparent == true)
        {
            Messenger.Broadcast<string>(Message.OnEndFadeToTransparent, currentFadeRequestId);
            panel.SetActive(false);
        }
        else
        {
            Messenger.Broadcast<string>(Message.OnEndFadeToOpaque, currentFadeRequestId);
        }
    }
}
```
