namespace Game.Modding;

public static class ModPlatformInfo
{
    public const string ApiVersion = "1.42";

    public const string ScVersion = "2.4.40.6";

    public const int ApiV = 3;

    public static readonly bool IsAndroid = OperatingSystem.IsAndroid();
}
