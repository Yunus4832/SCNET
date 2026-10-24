namespace Game.Protocol.Characters;

public sealed record CharacterStatisticsPackage : ComponentCharacterPackage
{
    public override CharacterAction Type => CharacterAction.SyncStat;

    public byte[] Stat { get; init; } = [];
}
