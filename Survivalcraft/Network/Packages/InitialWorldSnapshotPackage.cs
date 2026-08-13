using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

public sealed class InitialWorldSnapshotPackage : IPackage
{
    public Guid Epoch;
    public ClientPackage ClientList = new();
    public readonly List<ValuesDictionary> Players = [];
    public byte[] EntityData = [];

    public byte ID => (byte)PackageType.InitialWorldSnapshot;
    public Client? To { get; set; }
    public Client? Except { get; set; }
    public Client? From { get; set; }
    public ClientState MinNeedState => ClientState.NotConnected;

    public InitialWorldSnapshotPackage()
    {
    }

    public InitialWorldSnapshotPackage(Guid epoch, Project project, IEnumerable<Client> clients,
        IEnumerable<PlayerData> players, IEnumerable<Entity> entities)
    {
        Epoch = epoch;
        ClientList = new ClientPackage(clients);
        foreach (var player in players)
        {
            var values = new ValuesDictionary();
            player.Save(values);
            Players.Add(values);
        }

        EntityData = SerializeEntities(project, entities);
    }

    public void WriteData(PackageStreamWriter writer)
    {
        writer.Write(Epoch);
        ClientList.WriteData(writer);
        writer.Write((ushort)Players.Count);
        foreach (var player in Players)
        {
            writer.Write(player);
        }

        writer.WriteBuff(EntityData);
    }

    public void ReadData(PackageStreamReader reader)
    {
        Epoch = reader.ReadGuid();
        ClientList = new ClientPackage();
        ClientList.ReadData(reader);
        var count = reader.ReadUInt16();
        for (var i = 0; i < count; i++)
        {
            Players.Add(reader.ReadValuesDictionary());
        }

        EntityData = reader.ReadBuff();
    }

    internal static byte[] SerializeEntities(Project project, IEnumerable<Entity> entities)
    {
        var values = new ValuesDictionary();
        project.SaveEntitiesAll(entities).Save(values);
        return values.ToMessagePack();
    }

    internal static List<Entity> DeserializeEntities(Project project, byte[] data)
    {
        var values = new ValuesDictionary();
        values.ApplyOverridesUseMessagePack(data);
        return project.LoadEntitiesAll(new EntityDataList(project.GameDatabase, values, false));
    }
}
