namespace Game.Protocol.World;

public sealed record SubsystemTimePackage(double Time, double TimeOfDayOffset) : IProtocolMessage
{
    public byte ID => (byte)PackageType.SubsystemTime;
}
