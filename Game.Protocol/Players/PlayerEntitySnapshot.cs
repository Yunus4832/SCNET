using Engine.Core;

namespace Game.Protocol.Players;

public readonly record struct PlayerEntitySnapshot(Guid PlayerGuid, int EntityId, Vector3 Position, int ParentBodyId);
