using Engine.Core;

namespace Game.Protocol.Players;

public sealed record PlayerPositionPackage : ComponentPlayerPackage
{
    public override PlayerAction Type => PlayerAction.PositionSet;

    public Vector3 Position { get; init; }

    public Vector3 Velocity { get; init; }
}
