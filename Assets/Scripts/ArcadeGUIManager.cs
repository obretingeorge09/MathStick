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
    public Text lbl_roundsRule = null;

    // Index 0..3 = Easy / Medium / Hard / Random, so the chosen one can be lit
    public SegButtonSelect[] sel_mode = new SegButtonSelect[4];

    // Index 0..2 = the entries of MatchLength.OPTIONS
    public SegButtonSelect[] sel_rounds = new SegButtonSelect[3];

    GameMode selectedMode = GameMode.Easy;
    int selectedRounds = MatchLength.DEFAULT_ROUNDS;

    /// <summary>What the rest of the game speaks: rounds needed to win.</summary>
    int selectedFirstTo => MatchLength.WinsFor(selectedRounds);

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
    int      CurFirstTo  => inBotMatch ? (BotMatchManager.Instance?.FirstTo ?? MatchLength.DefaultFirstTo)
                                       : (ArcadeMatchManager.Instance?.FirstTo ?? MatchLength.DefaultFirstTo);
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
        selectedRounds = MatchLength.DEFAULT_ROUNDS;
        UpdateModeSelectUI();
    }

    public void OnSelectEasy()    { selectedMode = GameMode.Easy;   UpdateModeSelectUI(); }
    public void OnSelectMedium()  { selectedMode = GameMode.Medium; UpdateModeSelectUI(); }
    public void OnSelectHard()    { selectedMode = GameMode.Hard;   UpdateModeSelectUI(); }
    public void OnSelectRandom()  { selectedMode = GameMode.Random; UpdateModeSelectUI(); }

    // Wired to the three length keys. The names are positional, not values —
    // the values live in MatchLength.OPTIONS so label and key cannot drift.
    public void OnSelectRounds1() { SetRounds(0); }
    public void OnSelectRounds2() { SetRounds(1); }
    public void OnSelectRounds3() { SetRounds(2); }

    void SetRounds(int index)
    {
        selectedRounds = MatchLength.OPTIONS[Mathf.Clamp(index, 0, MatchLength.OPTIONS.Length - 1)];
        UpdateModeSelectUI();
    }

    void UpdateModeSelectUI()
    {
        if (lbl_modeSelectTitle != null)
            lbl_modeSelectTitle.text = selectedMode.ToString().ToUpper() + "  ·  " +
                                       MatchLength.Label(selectedFirstTo);

        if (lbl_roundsRule != null)
            lbl_roundsRule.text = selectedRounds <= 1
                ? "ONE ROUND  ·  WINNER TAKES THE MATCH"
                : selectedRounds + " ROUNDS  ·  FIRST TO " + selectedFirstTo + " WINS";

        // Without this the panel never showed which key was chosen, so the
        // ones built dim looked broken however often they were tapped.
        for (int i = 0; i < sel_mode.Length; i++)
            if (sel_mode[i] != null) sel_mode[i].SetSelected(i == (int)selectedMode);

        int keys = Mathf.Min(sel_rounds.Length, MatchLength.OPTIONS.Length);
        for (int i = 0; i < keys; i++)
            if (sel_rounds[i] != null)
                sel_rounds[i].SetSelected(MatchLength.OPTIONS[i] == selectedRounds);
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
        if (lbl_inviteMode != null)
            lbl_inviteMode.text = invite.mode.ToString().ToUpper() + "  ·  " + MatchLength.Label(invite.firstTo);
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
        // "ROUND 2 OF 3" rather than "ROUND 2" — how long the match runs is the
        // thing the length keys were failing to communicate.
        if (lbl_roundInfo != null)
            lbl_roundInfo.text = CurFirstTo <= 1
                ? "ONE ROUND"
                : "ROUND " + CurRound + " OF " + MatchLength.RoundsFor(CurFirstTo);
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
