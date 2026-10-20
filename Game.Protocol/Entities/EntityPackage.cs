using Game.Protocol.Players;

namespace Game.Protocol.Entities;

public sealed record EntityPackage : IProtocolMessage
{
    public enum EventType : byte { Remove, LoadList, LoadOne, RequestSync }
    public EventType Type { get; init; }
    public byte[] EntityData { get; init; } = [];
    public IReadOnlyList<PlayerSnapshot> Players { get; init; } = [];
    public IReadOnlyList<PlayerEntitySnapshot> PlayerEntities { get; init; } = [];
    public int EntityId { get; init; }
    public IReadOnlyList<int> EntityIdList { get; init; } = [];
    public byte ID => (byte)PackageType.Entity;

    public EntityPackage() { }
    public EntityPackage(int id) { Type = EventType.Remove; EntityId = id; }
    public EntityPackage(IReadOnlyList<int> ids) { Type = EventType.RequestSync; EntityIdList = ids; }
}
