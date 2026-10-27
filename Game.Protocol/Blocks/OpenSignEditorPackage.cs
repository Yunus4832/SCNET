using Engine.Core.Mathematics;

using MessagePack;

namespace Game.Protocol.Blocks;

[MessagePackObject]
public sealed record OpenSignEditorPackage : BlockEditPackage
{
    [IgnoreMember] public override EventType Type => EventType.EditSign;

    [Key(0)] public Point3 Point3 { get; init; }
}
