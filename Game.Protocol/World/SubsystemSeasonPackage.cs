namespace Game.Protocol.World;

public sealed record SubsystemSeasonPackage(float TimeOfYear) : IProtocolMessage
{
    public byte ID => (byte)PackageType.SubsystemSeason;
}
