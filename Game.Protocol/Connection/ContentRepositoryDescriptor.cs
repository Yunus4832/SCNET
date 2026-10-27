using MessagePack;

using MaxLengthAttribute = System.ComponentModel.DataAnnotations.MaxLengthAttribute;

namespace Game.Protocol.Connection;

[MessagePackObject]
public sealed record ContentRepositoryDescriptor(
    [property: Key(0)] Guid Id,
    [property: Key(1), MaxLength(8192)] string BaseUrl,
    [property: Key(2)] int Priority
);
