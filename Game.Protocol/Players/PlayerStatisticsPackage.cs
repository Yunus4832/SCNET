namespace Game.Protocol.Players;

public sealed record PlayerStatisticsPackage : ComponentPlayerPackage
{
    public override PlayerAction Type => PlayerAction.SyncStat;
    public byte[] Stat { get; init; } = [];
}
