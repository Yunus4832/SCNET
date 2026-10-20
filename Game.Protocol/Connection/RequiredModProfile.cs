namespace Game.Protocol.Connection;

public sealed record RequiredModProfile(string Id, IReadOnlyList<RequiredModPackage> Packages);
