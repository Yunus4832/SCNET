using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

public class SubsystemPlayersPackage : IPackage
{
    private readonly List<ComponentPlayerPackage> _componentPlayerPackageList = [];

    public byte ID => (byte)PackageType.SubsystemPlayers;

    public Client? To { get; set; }

    public Client? Except { get; set; }

    public Client? From { get; set; }

    public ClientState MinNeedState => ClientState.Playing;

    public void ReadData(PackageStreamReader reader)
    {
        lock (_componentPlayerPackageList)
        {
            _componentPlayerPackageList.Clear();
            var count = reader.ReadInt32();
            for (var index = 0; index < count; index++)
            {
                var componentPlayerPackage = new ComponentPlayerPackage();
                componentPlayerPackage.ReadData(reader);
                componentPlayerPackage.NeedHandleMainPlayer = false;
                _componentPlayerPackageList.Add(componentPlayerPackage);
            }
        }
    }

    public void WriteData(PackageStreamWriter writer)
    {
        lock (_componentPlayerPackageList)
        {
            writer.Write(_componentPlayerPackageList.Count);
            foreach (var package in _componentPlayerPackageList)
            {
                package.WriteData(writer);
            }
        }
    }

    public void Handle(NetNode netNode, bool isServer)
    {
        lock (_componentPlayerPackageList)
        {
            foreach (var package in _componentPlayerPackageList)
            {
                package.Handle(netNode, isServer);
            }
        }
    }

    public void AddPackage(ComponentPlayerPackage componentPlayerPackage)
    {
        lock (_componentPlayerPackageList)
        {
            _componentPlayerPackageList.Add(componentPlayerPackage);
        }
    }
}
