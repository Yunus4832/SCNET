namespace Game.Protocol.Players;

public sealed record PlayerRestartPackage : ComponentPlayerPackage
{
    public override PlayerAction Type => PlayerAction.Restart;
}
