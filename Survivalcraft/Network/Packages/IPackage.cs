using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

public interface IPackage
{
    byte ID { get; }

    ClientState MinNeedState { get; }

    void WriteData(PackageStreamWriter writer);

    void ReadData(PackageStreamReader reader);
}
