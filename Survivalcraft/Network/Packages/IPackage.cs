using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

public interface IPackage
{
    byte ID { get; }

    Client? To { get; set; }

    Client? Except { get; set; }

    Client? From { get; set; }

    ClientState MinNeedState { get; }

    void WriteData(PackageStreamWriter writer);

    void ReadData(PackageStreamReader reader);
}
