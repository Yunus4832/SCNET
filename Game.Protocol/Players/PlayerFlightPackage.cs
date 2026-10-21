namespace Game.Protocol.Players;

public sealed record PlayerFlightPackage : ComponentPlayerPackage
{
    public override PlayerAction Type => PlayerAction.CreativeFlyChange;

    public bool IsCreativeFly { get; init; }
}
