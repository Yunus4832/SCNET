using Game.Protocol.Players;

namespace Game.Protocol.Connection;

public sealed record PlayerReadyPackage : ComponentPlayerPackage
{
    public override PlayerAction Type => PlayerAction.IntoPlaying;
}
