namespace Game;

public sealed class RunningSetting
{
    public RunModeType RunMode { get; set; } = RunModeType.Gui;

    public LogType LogLevel { get; set; } = LogType.Information;

    public string SessionId { get; set; } = "default";

    public bool Restore { get; set; }

    public string[] RemainingArgs { get; set; } = [];
}
