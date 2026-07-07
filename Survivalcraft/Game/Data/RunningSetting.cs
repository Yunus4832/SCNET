namespace Game;

public sealed class RunningSetting
{
    public RunModeType RunMode { get; set; } = RunModeType.Gui;

    public LogType LogLevel { get; set; } = LogType.Information;

    public string DefaultSessionId { get; set; } = string.Empty;

    public string PendingSessionId { get; set; } = string.Empty;

    public string[] RemainingArgs { get; set; } = [];

    public string ActiveSessionId { get; set; } = string.Empty;

    public bool HasExplicitSessionRequest { get; set; }

    public string RequestedSessionName { get; set; } = string.Empty;

    public string? SessionWorldOverride { get; set; }

    public string? SessionSeedOverride { get; set; }
}
