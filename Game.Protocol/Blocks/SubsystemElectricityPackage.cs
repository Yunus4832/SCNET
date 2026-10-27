using MessagePack;

namespace Game.Protocol.Blocks;

[MessagePackObject]
public sealed record SubsystemElectricityPackage([property: Key(0)] ElectricitySnapshot Snapshot) : IProtocolMessage
{
    public SubsystemElectricityPackage() : this(new ElectricitySnapshot())
    {
    }
}
