using System;

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
