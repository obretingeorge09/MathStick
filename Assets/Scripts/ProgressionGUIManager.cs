using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// Drives the three retention screens: PROFILE (rating + record),
/// LEADERBOARD (monthly, by country, and among friends) and DAILY
/// (login streak + rotating challenges).
///
/// Kept out of GUIManager so the menu flow stays readable; GUIManager only
/// opens these panels and hides them again.
/// </summary>
public class ProgressionGUIManager : MonoBehaviour
{
    public static ProgressionGUIManager Instance { get; private set; }

    // ── Panels ──────────────────────────────────────────────────────────
    public GameObject pnl_profile = null;
    public GameObject pnl_leaderboard = null;
    public GameObject pnl_daily = null;

    // ── Profile ─────────────────────────────────────────────────────────
    public Text  lbl_prof_name    = null;
    public Text  lbl_prof_rank    = null;
    public Text  lbl_prof_elo     = null;
    public Text  lbl_prof_next    = null;   // "112 TO GOLD"
    public Image img_prof_rankbar = null;   // filled image
    public Text  lbl_prof_record  = null;   // "24W - 11L"
    public Text  lbl_prof_winrate = null;
    public Text  lbl_prof_streak  = null;
    public Text  lbl_prof_best    = null;
    public Text  lbl_prof_rounds  = null;
    public Text  lbl_prof_coins   = null;

    // ── Leaderboard ─────────────────────────────────────────────────────
    public Transform  lbContent    = null;
    public GameObject lbRowPrefab  = null;
    public Text       lbl_lb_status = null;
    public Text       lbl_lb_month  = null;
    public Text       lbl_tab_global  = null;
    public Text       lbl_tab_country = null;
    public Text       lbl_tab_friends = null;

    // ── Daily ───────────────────────────────────────────────────────────
    public Text       lbl_daily_streak    = null;   // big number
    public Text       lbl_daily_sub       = null;   // "DAY STREAK"
    public Text       lbl_daily_best      = null;
    public Text       lbl_daily_coins     = null;
    public GameObject btn_claim_streak    = null;
    public Text       lbl_claim_streak    = null;
    public Transform  challengeRow0       = null;
    public Transform  challengeRow1       = null;
    public Transform  challengeRow2       = null;

    // ── Start-screen badges ─────────────────────────────────────────────
    public Text       lbl_menu_rank  = null;
    public Text       lbl_menu_coins = null;
    public GameObject badge_daily    = null;   // dot shown when rewards are waiting

    LeaderboardScope currentScope = LeaderboardScope.Global;

    static readonly Color TAB_ON  = new Color(0.46f, 1f, 0.01f);
    static readonly Color TAB_OFF = new Color(0.22f, 0.41f, 0.12f);
    static readonly Color DIM     = new Color(0.4f, 0.4f, 0.4f);

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(this); return; }
    }

    void Start()
    {
        if (PlayerStatsManager.Instance != null)
            PlayerStatsManager.Instance.OnStatsChanged += OnProgressionChanged;

        if (DailyManager.Instance != null)
            DailyManager.Instance.OnDailyChanged += OnProgressionChanged;

        HideAll();
    }

    void OnProgressionChanged()
    {
        RefreshMenuBadges();

        if (pnl_profile != null && pnl_profile.activeSelf) RefreshProfile();
        if (pnl_daily != null && pnl_daily.activeSelf)     RefreshDaily();
    }

    public void HideAll()
    {
        Show(pnl_profile, false);
        Show(pnl_leaderboard, false);
        Show(pnl_daily, false);
    }

    void BackToMenu()
    {
        HideAll();
        var gui = FindObjectOfType<GUIManager>();
        if (gui != null) gui.ShowStartScreen();
    }

    public void OnBackPressed() => BackToMenu();

    // ═══════════════════════════════════════════════════════════════════
    //  Menu badges
    // ═══════════════════════════════════════════════════════════════════

    public void RefreshMenuBadges()
    {
        var stats = PlayerStatsManager.Instance;
        if (stats != null)
        {
            if (lbl_menu_rank != null)
            {
                lbl_menu_rank.text = stats.RankName + "  " + stats.Elo;
                lbl_menu_rank.color = stats.RankColor;
            }
            if (lbl_menu_coins != null)
                lbl_menu_coins.text = stats.Coins.ToString();
        }

        var daily = DailyManager.Instance;
        if (badge_daily != null)
            badge_daily.SetActive(daily != null && daily.PendingRewards > 0);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Profile
    // ═══════════════════════════════════════════════════════════════════

    public void ShowProfile()
    {
        var gui = FindObjectOfType<GUIManager>();
        gui?.HideAllPanelsPublic();

        HideAll();
        Show(pnl_profile, true);
        RefreshProfile();
    }

    void RefreshProfile()
    {
        var stats = PlayerStatsManager.Instance;
        if (stats == null) return;

        if (lbl_prof_name != null)
            lbl_prof_name.text = (AuthManager.Instance?.DisplayName ?? "PLAYER").ToUpper();

        if (lbl_prof_rank != null)
        {
            lbl_prof_rank.text  = stats.RankName;
            lbl_prof_rank.color = stats.RankColor;
        }

        if (lbl_prof_elo != null) lbl_prof_elo.text = stats.Elo.ToString();

        if (lbl_prof_next != null)
        {
            int toNext = stats.EloToNextRank;
            int idx = PlayerStatsManager.RankIndexFor(stats.Elo);

            lbl_prof_next.text = toNext > 0 && idx + 1 < PlayerStatsManager.RANK_NAMES.Length
                ? toNext + " TO " + PlayerStatsManager.RANK_NAMES[idx + 1]
                : "TOP RANK";
        }

        if (img_prof_rankbar != null)
        {
            img_prof_rankbar.fillAmount = stats.RankProgress;
            img_prof_rankbar.color = stats.RankColor;
        }

        if (lbl_prof_record != null)  lbl_prof_record.text  = stats.Wins + "W - " + stats.Losses + "L";
        if (lbl_prof_winrate != null) lbl_prof_winrate.text = stats.WinRate + "%";
        if (lbl_prof_streak != null)  lbl_prof_streak.text  = stats.CurrentStreak.ToString();
        if (lbl_prof_best != null)    lbl_prof_best.text    = stats.BestStreak.ToString();
        if (lbl_prof_rounds != null)  lbl_prof_rounds.text  = stats.RoundsWon + " / " + (stats.RoundsWon + stats.RoundsLost);
        if (lbl_prof_coins != null)   lbl_prof_coins.text   = stats.Coins.ToString();
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Leaderboard
    // ═══════════════════════════════════════════════════════════════════

    public void ShowLeaderboard()
    {
        var gui = FindObjectOfType<GUIManager>();
        gui?.HideAllPanelsPublic();

        HideAll();
        Show(pnl_leaderboard, true);

        currentScope = LeaderboardScope.Global;
        LoadBoard();
    }

    public void OnTabGlobal()  { currentScope = LeaderboardScope.Global;  LoadBoard(); }
    public void OnTabCountry() { currentScope = LeaderboardScope.Country; LoadBoard(); }
    public void OnTabFriends() { currentScope = LeaderboardScope.Friends; LoadBoard(); }

    void LoadBoard()
    {
        UpdateTabHighlight();
        ClearRows();

        if (lbl_lb_month != null)
        {
            string month = FirebaseDBManager.Instance != null
                ? FirebaseDBManager.Instance.ServerMonthKey
                : System.DateTime.UtcNow.ToString("yyyy-MM");
            lbl_lb_month.text = "SEASON " + month;
        }

        if (lbl_lb_status != null) lbl_lb_status.text = "LOADING...";

        var mgr = LeaderboardManager.Instance;
        if (mgr == null)
        {
            if (lbl_lb_status != null) lbl_lb_status.text = "UNAVAILABLE";
            return;
        }

        mgr.Fetch(currentScope, PopulateBoard);
    }

    void UpdateTabHighlight()
    {
        if (lbl_tab_global != null)
            lbl_tab_global.color = currentScope == LeaderboardScope.Global ? TAB_ON : TAB_OFF;
        if (lbl_tab_country != null)
            lbl_tab_country.color = currentScope == LeaderboardScope.Country ? TAB_ON : TAB_OFF;
        if (lbl_tab_friends != null)
            lbl_tab_friends.color = currentScope == LeaderboardScope.Friends ? TAB_ON : TAB_OFF;

        if (lbl_tab_country != null)
        {
            string cc = PlayerStatsManager.Instance?.Country ?? "XX";
            lbl_tab_country.text = cc == "XX" ? "COUNTRY" : cc;
        }
    }

    void ClearRows()
    {
        if (lbContent == null) return;

        for (int i = lbContent.childCount - 1; i >= 0; i--)
            Destroy(lbContent.GetChild(i).gameObject);
    }

    void PopulateBoard(List<LeaderboardEntry> entries)
    {
        ClearRows();

        if (lbl_lb_status != null)
        {
            if (entries.Count == 0)
                lbl_lb_status.text = currentScope == LeaderboardScope.Friends
                    ? "ADD FRIENDS TO COMPARE"
                    : "NO PLAYERS YET THIS MONTH";
            else
                lbl_lb_status.text = entries.Count + (entries.Count == 1 ? " PLAYER" : " PLAYERS");
        }

        if (lbContent == null || lbRowPrefab == null) return;

        foreach (var e in entries)
        {
            var row = Instantiate(lbRowPrefab, lbContent);
            row.SetActive(true);

            var lblRank = row.transform.Find("lbl_rank")?.GetComponent<Text>();
            if (lblRank != null)
            {
                lblRank.text = "#" + e.rank;
                // Podium colours make the top three read at a glance
                if (e.rank == 1)      lblRank.color = new Color(1f, 0.84f, 0f);
                else if (e.rank == 2) lblRank.color = new Color(0.75f, 0.75f, 0.75f);
                else if (e.rank == 3) lblRank.color = new Color(0.80f, 0.50f, 0.20f);
            }

            var lblName = row.transform.Find("lbl_name")?.GetComponent<Text>();
            if (lblName != null)
            {
                lblName.text = e.isMe ? e.name + "  (YOU)" : e.name;
                if (e.isMe) lblName.color = new Color(0.46f, 1f, 0.01f);
            }

            var lblElo = row.transform.Find("lbl_elo")?.GetComponent<Text>();
            if (lblElo != null)
            {
                lblElo.text = e.elo.ToString();
                lblElo.color = PlayerStatsManager.RankColorFor(e.elo);
            }

            var lblSub = row.transform.Find("lbl_sub")?.GetComponent<Text>();
            if (lblSub != null)
                lblSub.text = e.wins + "W" + (currentScope == LeaderboardScope.Global && e.country != "XX"
                    ? "  ·  " + e.country : "");

            // Tint the player's own row so it stands out while scrolling
            if (e.isMe)
            {
                var bg = row.GetComponent<Image>();
                if (bg != null) bg.color = new Color(0.10f, 0.20f, 0.06f);
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Daily
    // ═══════════════════════════════════════════════════════════════════

    public void ShowDaily()
    {
        var gui = FindObjectOfType<GUIManager>();
        gui?.HideAllPanelsPublic();

        HideAll();
        Show(pnl_daily, true);
        RefreshDaily();
    }

    void RefreshDaily()
    {
        var daily = DailyManager.Instance;
        var stats = PlayerStatsManager.Instance;

        if (lbl_daily_coins != null && stats != null)
            lbl_daily_coins.text = stats.Coins.ToString();

        if (daily == null) return;

        if (lbl_daily_streak != null) lbl_daily_streak.text = daily.Streak.ToString();
        if (lbl_daily_sub != null)
            lbl_daily_sub.text = daily.Streak == 1 ? "DAY STREAK" : "DAY STREAK";
        if (lbl_daily_best != null)
            lbl_daily_best.text = "BEST: " + daily.LongestStreak;

        // Streak claim button
        if (btn_claim_streak != null)
        {
            bool canClaim = daily.CanClaimStreak;
            btn_claim_streak.SetActive(true);

            var claimBtn = btn_claim_streak.transform.Find("btn_face")?.GetComponent<Button>();
            if (claimBtn != null) claimBtn.interactable = canClaim;

            if (lbl_claim_streak != null)
            {
                // Nothing has to be earned first any more — opening the app is
                // the whole requirement, so there is no "go play something" state.
                if (canClaim)
                {
                    lbl_claim_streak.text = "CLAIM +" + daily.StreakReward;
                    lbl_claim_streak.color = new Color(0.46f, 1f, 0.01f);
                }
                else
                {
                    lbl_claim_streak.text = "COME BACK TOMORROW";
                    lbl_claim_streak.color = DIM;
                }
            }
        }

        RefreshChallengeRow(challengeRow0, 0);
        RefreshChallengeRow(challengeRow1, 1);
        RefreshChallengeRow(challengeRow2, 2);
    }

    void RefreshChallengeRow(Transform row, int index)
    {
        if (row == null) return;

        var daily = DailyManager.Instance;
        if (daily == null || index >= daily.Challenges.Count)
        {
            row.gameObject.SetActive(false);
            return;
        }

        row.gameObject.SetActive(true);
        var c = daily.Challenges[index];

        var lblDesc = row.Find("lbl_desc")?.GetComponent<Text>();
        if (lblDesc != null) lblDesc.text = c.Description;

        var lblProg = row.Find("lbl_prog")?.GetComponent<Text>();
        if (lblProg != null)
            lblProg.text = Mathf.Min(c.progress, c.target) + " / " + c.target;

        var bar = row.Find("bar/bar_fill")?.GetComponent<Image>();
        if (bar != null)
            bar.fillAmount = c.target > 0 ? Mathf.Clamp01((float)c.progress / c.target) : 0f;

        var lblReward = row.Find("btn_claim/btn_face/lbl_btn")?.GetComponent<Text>();
        var btnClaim  = row.Find("btn_claim/btn_face")?.GetComponent<Button>();

        if (lblReward != null)
        {
            if (c.claimed)
            {
                lblReward.text = "DONE";
                lblReward.color = DIM;
            }
            else if (c.Completed)
            {
                lblReward.text = "CLAIM";
                lblReward.color = new Color(0.46f, 1f, 0.01f);
            }
            else
            {
                lblReward.text = "+" + c.reward;
                lblReward.color = new Color(0.4f, 0.73f, 0.42f);
            }
        }

        if (btnClaim != null)
            btnClaim.interactable = c.Completed && !c.claimed;
    }

    public void OnClaimStreakPressed()
    {
        if (DailyManager.Instance != null && DailyManager.Instance.ClaimStreak())
            RefreshDaily();
    }

    public void OnClaimChallenge0() => ClaimChallenge(0);
    public void OnClaimChallenge1() => ClaimChallenge(1);
    public void OnClaimChallenge2() => ClaimChallenge(2);

    void ClaimChallenge(int index)
    {
        if (DailyManager.Instance != null && DailyManager.Instance.ClaimChallenge(index))
            RefreshDaily();
    }

    void OnDestroy()
    {
        if (PlayerStatsManager.Instance != null)
            PlayerStatsManager.Instance.OnStatsChanged -= OnProgressionChanged;

        if (DailyManager.Instance != null)
            DailyManager.Instance.OnDailyChanged -= OnProgressionChanged;
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
