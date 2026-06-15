namespace Game;

public enum SessionKind
{
    Gui,
    Singleplayer,
    LocalServer,
    RemoteClient,
    HeadlessServer
}

public enum SessionRestoreAction
{
    OpenMainMenu,
    OpenWorldList,
    LoadSingleplayerWorld,
    LoadLocalServerWorld,
    OpenServerBrowser,
    ConnectRemoteServer,
    StartHeadlessServer
}

public sealed class SessionInfo
{
    public string SessionId { get; set; } = "default";

    public SessionKind Kind { get; set; } = SessionKind.Gui;

    public SessionRestoreAction Action { get; set; } = SessionRestoreAction.OpenMainMenu;

    public string World { get; set; } = "World";

    public string Seed { get; set; } = string.Empty;

    public string ServerHost { get; set; } = string.Empty;

    public int ServerPort { get; set; }

    public string Password { get; set; } = string.Empty;
}
