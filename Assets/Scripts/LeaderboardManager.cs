using UnityEngine;
using Firebase.Database;
using System;
using System.Collections;
using System.Collections.Generic;

[Serializable]
public class LeaderboardEntry
{
    public int    rank;
    public string uid;
    public string name;
    public int    elo;
    public int    wins;
    public string country;
    public bool   isMe;
}

public enum LeaderboardScope { Global, Country, Friends }

/// <summary>
/// Monthly leaderboards, kept deliberately small to stay inside Firebase's
/// free tier: entries are denormalised copies of the handful of fields a row
/// needs, queried with LimitToLast so a client never downloads the full table.
///
/// Boards are keyed by month (yyyy-MM) so they reset on their own — a player
/// who starts today is never staring at an all-time top ten they cannot reach.
///
/// Requires these Realtime Database rules so the sort happens server-side:
///   "leaderboard": {
///     "global":  { "$month":            { ".indexOn": "elo" } },
///     "country": { "$cc": { "$month":   { ".indexOn": "elo" } } }
///   }
/// </summary>
public class LeaderboardManager : MonoBehaviour
{
    public static LeaderboardManager Instance { get; private set; }

    public const int PAGE_SIZE = 100;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(this); return; }
    }

    string MonthKey => FirebaseDBManager.Instance != null
        ? FirebaseDBManager.Instance.ServerMonthKey
        : DateTime.UtcNow.ToString("yyyy-MM");

    // ═══════════════════════════════════════════════════════════════════
    //  Publishing
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>Mirrors the local player's row into this month's boards.</summary>
    public void PublishMyEntry(string name, int elo, int wins, string country)
    {
        var db = FirebaseDBManager.Instance;
        string uid = AuthManager.Instance?.CurrentUser?.UserId;
        if (db == null || !db.IsInitialized || uid == null) return;

        var row = new Dictionary<string, object>
        {
            ["name"]    = name,
            ["elo"]     = elo,
            ["wins"]    = wins,
            ["country"] = country
        };

        string month = MonthKey;

        db.GetRef("leaderboard").Child("global").Child(month).Child(uid).SetValueAsync(row);

        if (!string.IsNullOrEmpty(country) && country != "XX")
            db.GetRef("leaderboard").Child("country").Child(country).Child(month).Child(uid)
              .SetValueAsync(row);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Fetching
    // ═══════════════════════════════════════════════════════════════════

    public void Fetch(LeaderboardScope scope, Action<List<LeaderboardEntry>> onDone)
    {
        switch (scope)
        {
            case LeaderboardScope.Country: FetchCountry(onDone); break;
            case LeaderboardScope.Friends: FetchFriends(onDone); break;
            default:                       FetchGlobal(onDone);  break;
        }
    }

    public void FetchGlobal(Action<List<LeaderboardEntry>> onDone)
    {
        var db = FirebaseDBManager.Instance;
        if (db == null || !db.IsInitialized) { onDone?.Invoke(new List<LeaderboardEntry>()); return; }

        var q = db.GetRef("leaderboard").Child("global").Child(MonthKey)
                  .OrderByChild("elo").LimitToLast(PAGE_SIZE);

        RunQuery(q, onDone);
    }

    public void FetchCountry(Action<List<LeaderboardEntry>> onDone)
    {
        var db = FirebaseDBManager.Instance;
        string cc = PlayerStatsManager.Instance?.Country ?? "XX";

        if (db == null || !db.IsInitialized || cc == "XX")
        {
            onDone?.Invoke(new List<LeaderboardEntry>());
            return;
        }

        var q = db.GetRef("leaderboard").Child("country").Child(cc).Child(MonthKey)
                  .OrderByChild("elo").LimitToLast(PAGE_SIZE);

        RunQuery(q, onDone);
    }

    void RunQuery(Query q, Action<List<LeaderboardEntry>> onDone)
    {
        string myUid = AuthManager.Instance?.CurrentUser?.UserId;

        q.GetValueAsync().ContinueWith(t =>
        {
            UnityMainThreadDispatcher.Enqueue(() =>
            {
                var list = new List<LeaderboardEntry>();

                if (!t.IsFaulted && t.Result != null && t.Result.Exists)
                {
                    foreach (var child in t.Result.Children)
                        list.Add(ParseRow(child, myUid));
                }

                // LimitToLast returns ascending — the board reads top-down
                list.Reverse();
                for (int i = 0; i < list.Count; i++) list[i].rank = i + 1;

                onDone?.Invoke(list);
            });
        });
    }

    static LeaderboardEntry ParseRow(DataSnapshot child, string myUid)
    {
        int elo = 0, wins = 0;
        if (child.Child("elo").Value != null)  int.TryParse(child.Child("elo").Value.ToString(), out elo);
        if (child.Child("wins").Value != null) int.TryParse(child.Child("wins").Value.ToString(), out wins);

        // Board rows use "name"; stats/{uid} rows use "displayName"
        string name = child.Child("name").Value?.ToString()
                   ?? child.Child("displayName").Value?.ToString()
                   ?? "Player";

        return new LeaderboardEntry
        {
            uid     = child.Key,
            name    = name,
            elo     = elo,
            wins    = wins,
            country = child.Child("country").Value?.ToString() ?? "XX",
            isMe    = child.Key == myUid
        };
    }

    /// <summary>
    /// Friends have no board of their own — their rows come straight from
    /// stats/{uid} and are ranked locally. The list is small, so this is cheap.
    /// </summary>
    public void FetchFriends(Action<List<LeaderboardEntry>> onDone)
    {
        StartCoroutine(FetchFriendsCo(onDone));
    }

    IEnumerator FetchFriendsCo(Action<List<LeaderboardEntry>> onDone)
    {
        var db = FirebaseDBManager.Instance;
        var list = new List<LeaderboardEntry>();
        string myUid = AuthManager.Instance?.CurrentUser?.UserId;

        if (db == null || !db.IsInitialized || myUid == null)
        {
            onDone?.Invoke(list);
            yield break;
        }

        var friendsTask = db.GetRef("friends").Child(myUid).GetValueAsync();
        yield return new WaitUntil(() => friendsTask.IsCompleted);

        var uids = new List<string> { myUid };
        if (!friendsTask.IsFaulted && friendsTask.Result != null && friendsTask.Result.Exists)
        {
            foreach (var child in friendsTask.Result.Children)
                uids.Add(child.Key);
        }

        foreach (var uid in uids)
        {
            var task = db.GetRef("stats").Child(uid).GetValueAsync();
            yield return new WaitUntil(() => task.IsCompleted);

            if (task.IsFaulted || task.Result == null || !task.Result.Exists) continue;

            list.Add(ParseRow(task.Result, myUid));
        }

        list.Sort((a, b) => b.elo.CompareTo(a.elo));
        for (int i = 0; i < list.Count; i++) list[i].rank = i + 1;

        onDone?.Invoke(list);
    }
}
