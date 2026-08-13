using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

public sealed class ConnectionPhaseAckPackage : IPackage
{
    public Guid Epoch;
    public ConnectionPhase Phase;

    public byte ID => (byte)PackageType.ConnectionPhaseAck;
    public Client? To { get; set; }
    public Client? Except { get; set; }
    public Client? From { get; set; }
    public ClientState MinNeedState => ClientState.NotConnected;

    public ConnectionPhaseAckPackage()
    {
    }

    public ConnectionPhaseAckPackage(Guid epoch, ConnectionPhase phase)
    {
        Epoch = epoch;
        Phase = phase;
    }

    public void WriteData(PackageStreamWriter writer)
    {
        writer.Write(Epoch);
        writer.WriteEnum(Phase);
    }

    public void ReadData(PackageStreamReader reader)
    {
        Epoch = reader.ReadGuid();
        Phase = reader.ReadEnum<ConnectionPhase>();
    }
}
