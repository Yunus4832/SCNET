using Engine.Core;

namespace Game.Protocol.Entities;

public readonly record struct PickableSnapshot(ushort Id, int Count, int Value, Vector3 Position, Vector3 Velocity, Matrix? StuckMatrix);
