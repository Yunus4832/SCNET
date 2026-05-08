namespace Game.NetWork.Packages;

public class SubsystemElectricityPackage : IPackage
{
    private SubsystemElectricity? _subsystem;

    private readonly List<SubsystemElectricity.NetSimulate> _netSimulates = [];

    public byte ID => (byte)PackageType.SubsystemElectricity;

    public Client? To { get; set; }

    public Client? Except { get; set; }

    public Client? From { get; set; }

    public ClientState MinNeedState => ClientState.Playing;

    public SubsystemElectricityPackage()
    {
    }

    public SubsystemElectricityPackage(List<SubsystemElectricity.NetSimulate> netSimulates)
    {
        _netSimulates.AddRange(netSimulates);
    }

    public void WriteData(PackageStreamWriter writer)
    {
        writer.Write((byte)_netSimulates.Count);
        foreach (var netSimulate in _netSimulates)
        {
            writer.Write(netSimulate.StartStep);
            writer.Write((ushort)netSimulate.SaveData.Count);
            foreach (var item in netSimulate.SaveData)
            {
                writer.WriteBlockPoint(item.Key);
                writer.Write(item.Value);
            }
        }
    }

    public void ReadData(PackageStreamReader reader)
    {
        var count = reader.ReadByte();
        for (byte i = 0; i < count; i++)
        {
            var netSimulate = new SubsystemElectricity.NetSimulate
            {
                StartStep = reader.ReadInt32()
            };
            var electricsCount = reader.ReadUInt16();
            for (var p = 0; p < electricsCount; p++)
            {
                netSimulate.SaveData.Add(reader.ReadBlockPoint(), reader.ReadSingle());
            }

            _netSimulates.Add(netSimulate);
        }
    }

    public void Handle(ProjectNet projectNet, NetNode netNode, bool isServer)
    {
        _subsystem = projectNet.FindSubsystem<SubsystemElectricity>(true)!;
        _subsystem.List.AddRange(_netSimulates);
    }
}
