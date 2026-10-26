namespace Game.Diagnostics;

public struct AnalyticsParameter(string name, string value)
{
    public string Name = name;

    public string Value = value;
}
