using System.Collections.ObjectModel;

using Engine.Core.Mathematics;

using MessagePack;

namespace Game.Protocol.World;

[MessagePackObject]
public sealed record ExplosionCellsPackage : ExplosionsPackage
{
    [IgnoreMember] public override EventType Type => EventType.Cell;

    [Key(0)]
    public IReadOnlyDictionary<Point2, IReadOnlyList<ExplosionCell>> Cells { get; init; } =
        ReadOnlyDictionary<Point2, IReadOnlyList<ExplosionCell>>.Empty;
}
