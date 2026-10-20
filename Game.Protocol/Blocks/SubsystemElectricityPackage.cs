namespace Game.Protocol.Blocks;

public sealed record SubsystemElectricityPackage : IProtocolMessage
{
    public ElectricitySnapshot Snapshot { get; init; }
    public byte ID => (byte)PackageType.SubsystemElectricity;
    public SubsystemElectricityPackage() => Snapshot = new ElectricitySnapshot();
    public SubsystemElectricityPackage(ElectricitySnapshot snapshot) => Snapshot = snapshot;
}
