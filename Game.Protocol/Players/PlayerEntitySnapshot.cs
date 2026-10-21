using Engine.Core.Mathematics;

namespace Game.Protocol.Players;

public readonly record struct PlayerEntitySnapshot(Guid PlayerGuid, int EntityId, Vector3 Position, int ParentBodyId);
