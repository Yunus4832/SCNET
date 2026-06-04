namespace Game;

public sealed class RunningSetting
{
    public RunModeType RunMode { get; set; } = RunModeType.Gui;

    public string World { get; set; } = "World";

    public string Seed { get; set; } = string.Empty;

    public string[] RemainingArgs { get; set; } = [];
}
