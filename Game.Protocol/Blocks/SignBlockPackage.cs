using Engine.Core.Mathematics;

using MessagePack;

using MaxLengthAttribute = System.ComponentModel.DataAnnotations.MaxLengthAttribute;

namespace Game.Protocol.Blocks;

[MessagePackObject]
public sealed record SignBlockPackage() : IProtocolMessage
{
    [Key(0)] public Point3 Point { get; init; }

    [Key(1), MaxLength(255)] public IReadOnlyList<string> Lines { get; init; } = [];

    [Key(2), MaxLength(255)] public IReadOnlyList<Color> Colors { get; init; } = [];

    [Key(3)] public string Url { get; init; } = string.Empty;

    public SignBlockPackage(Point3 point, string[] lines, Color[] colors, string url) : this()
    {
        Point = point;
        Lines = lines.ToArray();
        Colors = colors.ToArray();
        Url = url;
    }
}
