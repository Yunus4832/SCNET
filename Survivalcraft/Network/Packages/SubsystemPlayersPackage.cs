using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

public class SubsystemPlayersPackage : IPackage
{
    public readonly List<ComponentPlayerPackage> ComponentPlayerPackageList = [];

    public byte ID => (byte)PackageType.SubsystemPlayers;

    public Client? To { get; set; }

    public Client? Except { get; set; }

    public Client? From { get; set; }

    public ClientState MinNeedState => ClientState.Playing;

    public void ReadData(PackageStreamReader reader)
    {
        lock (ComponentPlayerPackageList)
        {
            ComponentPlayerPackageList.Clear();
            var count = reader.ReadInt32();
            for (var index = 0; index < count; index++)
            {
                var componentPlayerPackage = new ComponentPlayerPackage();
                componentPlayerPackage.ReadData(reader);
                componentPlayerPackage.NeedHandleMainPlayer = false;
                ComponentPlayerPackageList.Add(componentPlayerPackage);
            }
        }
    }

    public void WriteData(PackageStreamWriter writer)
    {
        lock (ComponentPlayerPackageList)
        {
            writer.Write(ComponentPlayerPackageList.Count);
            foreach (var package in ComponentPlayerPackageList)
            {
                package.WriteData(writer);
            }
        }
    }


    public void AddPackage(ComponentPlayerPackage componentPlayerPackage)
    {
        lock (ComponentPlayerPackageList)
        {
            ComponentPlayerPackageList.Add(componentPlayerPackage);
        }
    }
}
