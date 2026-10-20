namespace Game.Protocol.Connection;

public sealed record RequiredModPackage(string ModId, string Version, string PackageHash);
