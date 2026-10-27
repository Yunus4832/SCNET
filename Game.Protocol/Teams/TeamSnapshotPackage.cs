using MessagePack;

namespace Game.Protocol.Teams;

[MessagePackObject]
public sealed record TeamSnapshotPackage : TeamManagePackage
{
    [IgnoreMember] public override CommandType Command => CommandType.SyncTeams;

    [Key(0)] public IReadOnlyList<TeamState> Teams { get; init; } = [];
}
