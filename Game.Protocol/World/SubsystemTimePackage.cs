using MessagePack;

namespace Game.Protocol.World;

[MessagePackObject]
public sealed record SubsystemTimePackage(
    [property: Key(0)] double Time,
    [property: Key(1)] double TimeOfDayOffset
) : IProtocolMessage
{
}
