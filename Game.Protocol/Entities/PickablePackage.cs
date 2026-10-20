using Engine.Core;

namespace Game.Protocol.Entities;

public sealed record PickablePackage : IProtocolMessage
{
    public const int MaxPositionsPerSnapshot = 24;
    public enum PickType { Create, Update, Delete, RequestSync, CreateList, DeleteList }
    public byte Count { get; init; }
    public ushort Id { get; init; }
    public Vector3 Position { get; init; }
    public PickType Type { get; init; }
    public uint StateTick { get; init; }
    public int Value { get; init; }
    public Vector3 Velocity { get; init; }
    public bool PlaySound { get; init; }
    public Matrix? StuckMatrix { get; init; }
    public IReadOnlyList<PickableSnapshot> Pickables { get; init; } = [];
    public IReadOnlyList<PickablePosition> Positions { get; init; } = [];
    public IReadOnlyList<ushort> RemovedIds { get; init; } = [];
    public byte ID => (byte)PackageType.Pickable;
    public PickablePackage() { }
    public PickablePackage(ushort id) { Id = id; Type = PickType.Delete; }
}
