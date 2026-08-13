using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

public sealed class BootstrapPackage : IPackage
{
    public Guid Epoch;
    public ClientPackage ClientList = new();
    public byte[] ProjectData = [];
    public byte[] TextureData = [];

    public byte ID => (byte)PackageType.Bootstrap;
    public Client? To { get; set; }
    public Client? Except { get; set; }
    public Client? From { get; set; }
    public ClientState MinNeedState => ClientState.NotConnected;

    public BootstrapPackage()
    {
    }

    public BootstrapPackage(Guid epoch, IEnumerable<Client> clients, byte[]? textureData, byte[] projectData)
    {
        Epoch = epoch;
        ClientList = new ClientPackage(clients);
        TextureData = textureData ?? [];
        ProjectData = projectData;
    }

    public void WriteData(PackageStreamWriter writer)
    {
        writer.Write(Epoch);
        ClientList.WriteData(writer);
        writer.WriteBuff(TextureData);
        writer.WriteBuff(ProjectData);
    }

    public void ReadData(PackageStreamReader reader)
    {
        Epoch = reader.ReadGuid();
        ClientList = new ClientPackage();
        ClientList.ReadData(reader);
        TextureData = reader.ReadBuff();
        ProjectData = reader.ReadBuff();
    }
}
