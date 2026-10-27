using Engine.Core.Mathematics;

using MessagePack;

namespace Game.Protocol.Entities;

[MessagePackObject]
public readonly record struct PickablePosition([property: Key(0)] ushort Id, [property: Key(1)] Vector3 Position);
