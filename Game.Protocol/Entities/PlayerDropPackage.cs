using Game.Protocol.Players;

namespace Game.Protocol.Entities;

public sealed record PlayerDropPackage : ComponentPlayerPackage
{
    public override PlayerAction Type => PlayerAction.Drop;
}
