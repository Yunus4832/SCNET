namespace Game.Platforms;

public static class ClientPlatform
{
    public static Platform Kind { get; private set; } = Platform.Desktop;

    public static void Configure(Platform platform) => Kind = platform;
}
