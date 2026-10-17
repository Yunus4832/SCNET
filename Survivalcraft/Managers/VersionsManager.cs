namespace Game.Managers;

public static class VersionsManager
{
    public const string GameName = "SCNET";

    public static string BuildVersion { get; } = typeof(VersionsManager).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? throw new InvalidOperationException("The game assembly has no informational version.");

    public static string Version { get; } = BuildVersion.Split('+', 2)[0];

    public static string Title { get; } = $"{GameName}-{Version}";

    public static string ProtocolVersion { get; } = typeof(VersionsManager).Assembly
        .GetCustomAttributes<AssemblyMetadataAttribute>()
        .Single(attribute => attribute.Key == "GameProtocolVersion").Value
        ?? throw new InvalidOperationException("The game assembly has no protocol version.");
}
