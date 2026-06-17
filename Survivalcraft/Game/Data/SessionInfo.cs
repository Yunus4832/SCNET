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

    public string ServerHost { get; set; } = string.Empty;

    public int ServerPort { get; set; }

    public string Password { get; set; } = string.Empty;
}
