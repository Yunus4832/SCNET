namespace Game;

public sealed class RunningSetting
{
    public RunModeType RunMode { get; set; } = RunModeType.Gui;

    public LogType LogLevel { get; set; } = LogType.Information;

    /// <summary>窗口模式（仅 GUI 模式生效；headless 无窗口）。</summary>
    public WindowMode WindowMode { get; set; } = WindowMode.Resizable;

    /// <summary>窗口宽度，0 表示使用默认（屏幕 3/4）。</summary>
    public int WindowWidth { get; set; }

    /// <summary>窗口高度，0 表示使用默认（屏幕 3/4）。</summary>
    public int WindowHeight { get; set; }

    public string DefaultSessionId { get; set; } = string.Empty;

    public string PendingSessionId { get; set; } = string.Empty;

    public string[] RemainingArgs { get; set; } = [];

    public string ActiveSessionId { get; set; } = string.Empty;

    public bool HasExplicitSessionRequest { get; set; }

    public string RequestedSessionName { get; set; } = string.Empty;

    public string? SessionWorldOverride { get; set; }

    public string? SessionSeedOverride { get; set; }

    public string? SessionConnectHostOverride { get; set; }

    public int? SessionConnectPortOverride { get; set; }

    public string? PlayerOverride { get; set; }

    public int? SessionServerPortOverride { get; set; }

    public int? SessionBroadcastPortOverride { get; set; }

    public bool SaveRequested { get; set; }
}
