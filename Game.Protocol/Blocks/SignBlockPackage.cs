using Engine.Core.Mathematics;

namespace Game.Protocol.Blocks;

public sealed record SignBlockPackage : IProtocolMessage
{
    public Point3 Point { get; init; }

    public IReadOnlyList<string> Lines { get; init; } = [];

    public IReadOnlyList<Color> Colors { get; init; } = [];

    public string Url { get; init; } = string.Empty;

    public byte ID => (byte)PackageType.SignBlock;

    public SignBlockPackage()
    {
    }

    public SignBlockPackage(Point3 point, string[] lines, Color[] colors, string url)
    {
        Point = point;
        Lines = lines.ToArray();
        Colors = colors.ToArray();
        Url = url;
    }
}
