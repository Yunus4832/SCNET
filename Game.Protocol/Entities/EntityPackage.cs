using Game.Protocol.Characters;

namespace Game.Protocol.Entities;

public sealed record EntityPackage : IProtocolMessage
{
    public enum EventType : byte { Remove = 0, LoadList = 1, LoadOne = 2, RequestSync = 3 }

    public EventType Type { get; init; }

    public byte[] EntityData { get; init; } = [];

    public IReadOnlyList<CharacterSnapshot> Characters { get; init; } = [];

    public IReadOnlyList<CharacterEntitySnapshot> CharacterEntities { get; init; } = [];

    public int EntityId { get; init; }

    public IReadOnlyList<int> EntityIdList { get; init; } = [];

    public byte ID => (byte)PackageType.Entity;

    public EntityPackage()
    {
    }

    public EntityPackage(int id)
    {
        Type = EventType.Remove;
        EntityId = id;
    }

    public EntityPackage(IReadOnlyList<int> ids)
    {
        Type = EventType.RequestSync;
        EntityIdList = ids;
    }
}
