using MessagePack;

using MaxLengthAttribute = System.ComponentModel.DataAnnotations.MaxLengthAttribute;

namespace Game.Protocol.Blocks;

[MessagePackObject]
public sealed record EditableBlockDataPackage : EditableBlockPackage
{
    [Key(2)] public int? DefinitionId { get; init; }

    [Key(3), MaxLength(65535)] public required byte[] Data { get; init; }
}
