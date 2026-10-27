using MessagePack;

namespace Game.Protocol.Characters;

[MessagePackObject]
public sealed record CharacterStatisticsPackage : ComponentCharacterPackage
{
    [IgnoreMember] public override CharacterAction Type => CharacterAction.SyncStat;

    [Key(1)] public byte[] Stat { get; init; } = [];
}
