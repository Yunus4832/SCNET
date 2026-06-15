namespace Game;

public enum GuiStartupBehavior
{
    MainMenu,
    EnterDefaultSession
}

public sealed class RunningSetting
{
    public RunModeType RunMode { get; set; } = RunModeType.Gui;

    public LogType LogLevel { get; set; } = LogType.Information;

    public string DefaultSessionId { get; set; } = "default";

    public string PendingSessionId { get; set; } = string.Empty;

    public GuiStartupBehavior DefaultGuiStartupBehavior { get; set; } = GuiStartupBehavior.MainMenu;

    public string[] RemainingArgs { get; set; } = [];

    public string ActiveSessionId { get; set; } = "default";

    public bool HasExplicitSessionRequest { get; set; }

    public bool SessionIsTransient { get; set; }

    public bool ShouldEnterSession { get; set; }

    public string? SessionWorldOverride { get; set; }

    public string? SessionSeedOverride { get; set; }
}
