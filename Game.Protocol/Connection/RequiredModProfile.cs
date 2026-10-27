using MessagePack;

using MaxLengthAttribute = System.ComponentModel.DataAnnotations.MaxLengthAttribute;

namespace Game.Protocol.Connection;

[MessagePackObject]
public sealed record RequiredModProfile(
    [property: Key(0), MaxLength(512)] string Id,
    [property: Key(1), MaxLength(256)] IReadOnlyList<RequiredModPackage> Packages
);
