using System.Globalization;
using System.Reflection;

namespace Game.Protocol;

public static class GameProtocol
{
    public const string EmptyModDataHash = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";

    public const int ConnectionMagic = 9421523;

    public static int CurrentVersion { get; } = int.Parse(typeof(GameProtocol).Assembly
        .GetCustomAttributes<AssemblyMetadataAttribute>()
        .Single(attribute => attribute.Key == "GameProtocolVersion").Value
        ?? throw new InvalidOperationException("The game protocol assembly has no wire version."), CultureInfo.InvariantCulture);
}
