using System;

/// <summary>
/// How long a match runs. Everything on the wire speaks firstTo — rounds a
/// player must WIN — while the mode-select panel offers a total round count,
/// because "5" reading as a nine-round match was the confusing part.
/// </summary>
public static class MatchLength
{
    /// <summary>Lengths offered, in rounds played. Odd, so a match cannot end level.</summary>
    public static readonly int[] OPTIONS = { 1, 3, 5 };

    public const int DEFAULT_ROUNDS = 3;

    /// <summary>Rounds needed to take the match: 1 of 1, 2 of 3, 3 of 5.</summary>
    public static int WinsFor(int rounds) => rounds / 2 + 1;

    /// <summary>Most rounds a match needing this many wins can run.</summary>
    public static int RoundsFor(int wins) => wins * 2 - 1;

    /// <summary>Falls back here when a match or invite arrives without the field.</summary>
    public static int DefaultFirstTo => WinsFor(DEFAULT_ROUNDS);

    /// <summary>
    /// Composed from a count and a noun rather than translated as a sentence.
    /// "BEST OF 3" cannot be assembled that way — Chinese renders the idea as
    /// 五局三胜制, which carries its own numbers, so pasting a 3 in front of a
    /// translation of "BEST OF" produces nonsense in several languages.
    /// </summary>
    public static string Label(int wins) =>
        wins <= 1 ? Loc.T("SINGLE ROUND")
                  : RoundsFor(wins) + " " + Loc.T("ROUNDS");
}

[Serializable]
public class OnlineUser
{
    public string uid;
    public string displayName;
    public bool isFriend;
    public bool isOnline;
}

[Serializable]
public class InviteData
{
    public string inviteId;
    public string fromUid;
    public string fromName;
    public GameMode mode;
    public int firstTo;
    public string status; // "pending", "accepted", "declined"
}

[Serializable]
public class MatchSettings
{
    public GameMode mode;
    public int firstTo;
}

[Serializable]
public class MatchData
{
    public string matchId;
    public string hostUid;
    public string opponentUid;
    public string opponentName;
    public MatchSettings settings;
    public string state; // "waiting", "playing", "roundEnd", "matchEnd", "abandoned"
}
