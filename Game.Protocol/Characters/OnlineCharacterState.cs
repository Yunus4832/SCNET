using Engine.Core.Mathematics;

using MessagePack;

namespace Game.Protocol.Characters;

[MessagePackObject]
public readonly record struct OnlineCharacterState(
    [property: Key(0)] Guid CharacterId,
    [property: Key(1)] Vector3 Position,
    [property: Key(2)] float Health,
    [property: Key(3)] bool IsSleeping
);
