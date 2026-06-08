using LiteNetLib;

namespace Game.Network;

public enum NetworkChannel : byte
{
    Control = 0,
    ReliableEvent = 1,
    Snapshot = 2,
    Effect = 3,
    Bulk = 4
}

public readonly record struct PackageTransport(
    NetworkChannel Channel,
    DeliveryMethod DeliveryMethod,
    double FlushInterval,
    bool Coalesce = false
)
{
    public byte ChannelNumber => (byte)Channel;
}
