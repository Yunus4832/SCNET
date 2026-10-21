namespace Game.Protocol.Players;

public sealed record PlayerExperiencePackage : ComponentPlayerPackage
{
    public override PlayerAction Type => PlayerAction.AddExperience;

    public int Count { get; init; }

    public bool PlaySound { get; init; }

    public float Level { get; init; }
}
