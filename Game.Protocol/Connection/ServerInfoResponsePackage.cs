using MessagePack;

using MaxLengthAttribute = System.ComponentModel.DataAnnotations.MaxLengthAttribute;

namespace Game.Protocol.Connection;

[MessagePackObject]
public sealed record ServerInfoResponsePackage : ServerInfoPackage
{
    [IgnoreMember] public override bool RequestInfo => false;

    [Key(0)] public int ProtocolVersion { get; init; }

    [Key(1)] public ushort ClientCount { get; init; }

    [Key(2)] public ushort MaxPlayerCount { get; init; }

    [Key(3)] public byte GameMode { get; init; }

    [Key(4)] public float TimeOfDay { get; init; }

    [Key(5)] public byte Season { get; init; }

    [Key(6)] public float TimeOfSeason { get; init; }

    [Key(7)] public RequiredModProfile? RequiredModProfile { get; init; }

    [Key(8), MaxLength(16)] public IReadOnlyList<ContentRepositoryDescriptor> TemporaryRepositories { get; init; } = [];
}
