using Game.Modding.Blocks;
using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

public sealed class BootstrapPackage : IPackage
{
    public Guid Epoch;
    public BlockIndexMapping BlockMapping = BlockIndexMapping.Empty;
    public ClientPackage ClientList = new();
    public byte[] ProjectData = [];
    public byte[] TextureData = [];

    public byte ID => (byte)PackageType.Bootstrap;
    public ClientState MinNeedState => ClientState.NotConnected;

    public BootstrapPackage()
    {
    }

    public BootstrapPackage(Guid epoch, IEnumerable<Client> clients, byte[]? textureData, byte[] projectData,
        BlockIndexMapping blockMapping)
    {
        Epoch = epoch;
        BlockMapping = blockMapping;
        ClientList = new ClientPackage(clients);
        TextureData = textureData ?? [];
        ProjectData = projectData;
    }

    public void WriteData(PackageStreamWriter writer)
    {
        writer.Write(Epoch);
        ClientList.WriteData(writer);
        writer.Write((ushort)BlockMapping.Entries.Count);
        foreach (var (id, index) in BlockMapping.Entries.OrderBy(pair => pair.Key.ToString(), StringComparer.Ordinal))
        {
            writer.Write(id.ToString());
            writer.Write((ushort)index);
        }
        writer.WriteBuff(TextureData);
        writer.WriteBuff(ProjectData);
    }

    public void ReadData(PackageStreamReader reader)
    {
        Epoch = reader.ReadGuid();
        ClientList = new ClientPackage();
        ClientList.ReadData(reader);
        var count = reader.ReadUInt16();
        if (count > BlockRuntimeCatalog.Capacity)
        {
            throw new InvalidDataException("Too many block mappings.");
        }

        var entries = new List<KeyValuePair<ResourceId, int>>(count);
        for (var i = 0; i < count; i++)
        {
            var id = reader.ReadString();
            if (id.Length > BlockIndexMapping.MaximumResourceIdLength)
            {
                throw new InvalidDataException("Block resource id is too long.");
            }

            entries.Add(new KeyValuePair<ResourceId, int>(ResourceId.Parse(id), reader.ReadUInt16()));
        }

        BlockMapping = new BlockIndexMapping(entries);
        TextureData = reader.ReadBuff();
        ProjectData = reader.ReadBuff();
    }
}
