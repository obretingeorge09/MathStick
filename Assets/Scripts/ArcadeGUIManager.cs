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
    public Transform userListContent = null;  // ScrollView content
    public GameObject userRowPrefab = null;    // set by SceneBuilder
    public Text lbl_lobbyStatus = null;

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

    // ── Invite Popup UI ─────────────────────────────────────────────────
    public Text lbl_inviteFrom = null;
    public Text lbl_inviteMode = null;

    // ── Round overlay ───────────────────────────────────────────────────
    public GameObject pnl_roundOverlay = null;
    public Text lbl_roundResult = null;

    InviteData currentInvite;

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
            lobby.OnInviteReceived += ShowInvitePopup;
            lobby.OnMatchFound += OnMatchCreated;
            lobby.OnInviteAccepted += OnMatchCreated;
            lobby.OnInviteDeclined += OnInviteDeclined;
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
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Mode Select Panel
    // ═══════════════════════════════════════════════════════════════════

    public void ShowModeSelect()
    {
        HideAllArcadePanels();
        pnl_arcadeModeSelect?.SetActive(true);
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
        HideAllArcadePanels();
        pnl_arcadeWaiting?.SetActive(true);
        if (lbl_waitingStatus != null) lbl_waitingStatus.text = "SEARCHING...";

        LobbyManager.Instance?.EnterRandomQueue(selectedMode, selectedFirstTo);
    }

    public void OnCancelSearchPressed()
    {
        LobbyManager.Instance?.LeaveRandomQueue();
        ShowModeSelect();
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Lobby (Online Users + Invite)
    // ═══════════════════════════════════════════════════════════════════

    public void OnShowLobbyPressed()
    {
        HideAllArcadePanels();
        pnl_lobby?.SetActive(true);

        LobbyManager.Instance?.StartListeningOnlineUsers();
        LobbyManager.Instance?.StartListeningInvites();
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

            // Set name label
            var nameLabel = row.GetComponentInChildren<Text>();
            if (nameLabel != null) nameLabel.text = user.displayName;

            // Set invite button
            var btn = row.GetComponentInChildren<Button>();
            if (btn != null)
            {
                string uid = user.uid;
                btn.onClick.AddListener(() => SendInviteToUser(uid));
            }
        }
    }

    void SendInviteToUser(string uid)
    {
        LobbyManager.Instance?.SendInvite(uid, selectedMode, selectedFirstTo);

        HideAllArcadePanels();
        pnl_arcadeWaiting?.SetActive(true);
        if (lbl_waitingStatus != null) lbl_waitingStatus.text = "WAITING FOR RESPONSE...";
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Invite Popup (received invite)
    // ═══════════════════════════════════════════════════════════════════

    void ShowInvitePopup(InviteData invite)
    {
        currentInvite = invite;
        pnl_invitePopup?.SetActive(true);

        if (lbl_inviteFrom != null) lbl_inviteFrom.text = invite.fromName;
        if (lbl_inviteMode != null) lbl_inviteMode.text = invite.mode.ToString().ToUpper() + " - FIRST TO " + invite.firstTo;
    }

    public void OnAcceptInvitePressed()
    {
        if (currentInvite == null) return;
        pnl_invitePopup?.SetActive(false);

        LobbyManager.Instance?.AcceptInvite(currentInvite);
        currentInvite = null;
    }

    public void OnDeclineInvitePressed()
    {
        if (currentInvite == null) return;
        pnl_invitePopup?.SetActive(false);

        LobbyManager.Instance?.DeclineInvite(currentInvite);
        currentInvite = null;
    }

    void OnInviteDeclined()
    {
        if (lbl_waitingStatus != null) lbl_waitingStatus.text = "INVITE DECLINED";
        StartCoroutine(ReturnToModeSelectAfterDelay(2f));
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Match Flow
    // ═══════════════════════════════════════════════════════════════════

    void OnMatchCreated(string matchId)
    {
        HideAllArcadePanels();
        pnl_arcadeWaiting?.SetActive(true);
        if (lbl_waitingStatus != null) lbl_waitingStatus.text = "JOINING MATCH...";

        // Stop lobby listeners
        LobbyManager.Instance?.StopListeningOnlineUsers();
        LobbyManager.Instance?.LeaveRandomQueue();

        ArcadeMatchManager.Instance?.JoinMatch(matchId);
    }

    void OnOpponentJoined(string name)
    {
        if (lbl_waitingStatus != null) lbl_waitingStatus.text = name + " JOINED!";
    }

    void OnEquationReady()
    {
        HideAllArcadePanels();
        pnl_arcadeHUD?.SetActive(true);

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
        var match = ArcadeMatchManager.Instance;
        if (match == null) return;

        if (lbl_myScore != null) lbl_myScore.text = match.MyScore.ToString();
        if (lbl_oppScore != null) lbl_oppScore.text = match.OpponentScore.ToString();
        if (lbl_roundInfo != null) lbl_roundInfo.text = "ROUND " + match.CurrentRound;
        if (lbl_oppName != null) lbl_oppName.text = match.OpponentName?.ToUpper() ?? "OPPONENT";
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

        UpdateHUD();
        StartCoroutine(HideRoundOverlayAfterDelay(1.5f));
    }

    IEnumerator HideRoundOverlayAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        pnl_roundOverlay?.SetActive(false);
    }

    void ShowMatchResult(bool iWon)
    {
        HideAllArcadePanels();
        pnl_arcadeResult?.SetActive(true);

        // Hide main game panel
        var gui = FindObjectOfType<GUIManager>();
        if (gui != null && gui.pnl_main != null)
            gui.pnl_main.SetActive(false);

        var match = ArcadeMatchManager.Instance;

        if (lbl_resultTitle != null) lbl_resultTitle.text = iWon ? "YOU WIN!" : "YOU LOSE";
        if (lbl_resultScore != null && match != null)
            lbl_resultScore.text = match.MyScore + " - " + match.OpponentScore;
        if (lbl_resultDetail != null && match != null)
            lbl_resultDetail.text = "VS " + (match.OpponentName?.ToUpper() ?? "OPPONENT");
    }

    void OnOpponentDisconnected()
    {
        ShowMatchResult(true);
        if (lbl_resultDetail != null) lbl_resultDetail.text = "OPPONENT DISCONNECTED";
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Result Panel Buttons
    // ═══════════════════════════════════════════════════════════════════

    public void OnRematchPressed()
    {
        ArcadeMatchManager.Instance?.Cleanup();
        ShowModeSelect();
        // TODO: implement actual rematch (re-invite same opponent)
    }

    public void OnReturnToLobbyPressed()
    {
        ArcadeMatchManager.Instance?.Cleanup();
        ShowModeSelect();
    }

    public void OnArcadeBackToMenu()
    {
        ArcadeMatchManager.Instance?.Cleanup();
        LobbyManager.Instance?.StopListeningOnlineUsers();
        LobbyManager.Instance?.StopListeningInvites();
        LobbyManager.Instance?.LeaveRandomQueue();
        HideAllArcadePanels();

        // Return to start screen
        var gui = FindObjectOfType<GUIManager>();
        if (gui != null) gui.ShowStartScreen();
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Helpers
    // ═══════════════════════════════════════════════════════════════════

    void HideAllArcadePanels()
    {
        pnl_arcadeModeSelect?.SetActive(false);
        pnl_lobby?.SetActive(false);
        pnl_arcadeWaiting?.SetActive(false);
        pnl_arcadeHUD?.SetActive(false);
        pnl_arcadeResult?.SetActive(false);
        pnl_invitePopup?.SetActive(false);
        pnl_roundOverlay?.SetActive(false);
    }

    public void HideAllPanels()
    {
        HideAllArcadePanels();
    }

    void ShowError(string msg)
    {
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
            lobby.OnInviteReceived -= ShowInvitePopup;
            lobby.OnMatchFound -= OnMatchCreated;
            lobby.OnInviteAccepted -= OnMatchCreated;
            lobby.OnInviteDeclined -= OnInviteDeclined;
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
        }
    }
}
