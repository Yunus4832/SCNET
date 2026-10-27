using MessagePack;

using MaxLengthAttribute = System.ComponentModel.DataAnnotations.MaxLengthAttribute;

namespace Game.Protocol.Connection;

[MessagePackObject]
public sealed record RequiredModPackage(
    [property: Key(0), MaxLength(512)] string ModId,
    [property: Key(1), MaxLength(256)] string Version,
    [property: Key(2), MaxLength(64)] string PackageHash
);
