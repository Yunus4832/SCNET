namespace Game;

public enum SessionTarget
{
    MainMenu,
    WorldList,
    World,
    ServerBrowser,
    RemoteServer
}

public sealed class SessionInfo
{
    public string SessionId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public SessionTarget Target { get; set; } = SessionTarget.MainMenu;

    public string World { get; set; } = "World";

    public string Seed { get; set; } = string.Empty;

    public GameMode? GameMode { get; set; }

    public string ServerHost { get; set; } = string.Empty;

    public int ServerPort { get; set; }

    public int BroadcastPort { get; set; }

    /// <summary>Optional HTTP command port override restored with this startup session.</summary>
    public int? HttpCommandPort { get; set; }

    /// <summary>Optional HTTP command access token restored with this startup session.</summary>
    public string? HttpCommandAccessToken { get; set; }

    public string Password { get; set; } = string.Empty;
}
