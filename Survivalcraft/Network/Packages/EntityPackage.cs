using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

public class EntityPackage : IPackage
{
    public enum EventType
    {
        Remove,
        LoadList,
        LoadOne,
        RequestSync
    }

    public List<Entity> Entities = [];

    public byte[] EntityData = [];

    public readonly List<ClientPackage> Clients = [];

    public readonly List<ValuesDictionary> Players = [];

    public int EntityId;

    public List<int> EntityIdList = [];

    public EventType Type;

    public byte ID => (byte)PackageType.Entity;




    public ClientState MinNeedState => ClientState.ProjectLoaded;


    public EntityPackage()
    {
    }

    public EntityPackage(int id)
    {
        Type = EventType.Remove;
        EntityId = id;
    }

    public EntityPackage(List<int> idList)
    {
        Type = EventType.RequestSync;
        EntityIdList.AddRange(idList);
    }

    public EntityPackage(Entity entity)
    {
        Type = EventType.LoadOne;
        Entities = [entity];
    }

    public EntityPackage(List<Entity> entities)
    {
        Type = EventType.LoadList;
        Entities.AddRange(entities);
    }

    public void WriteData(PackageStreamWriter writer)
    {
        writer.WriteEnum(Type);
        switch (Type)
        {
            case EventType.LoadOne:
            case EventType.LoadList:
                var players = Entities.Select(entity => entity.FindComponent<ComponentPlayer>()?.PlayerData)
                    .OfType<PlayerData>().Distinct().ToArray();
                writer.Write(players.Length);
                foreach (var player in players)
                {
                    var client = player.Client;
                    writer.Write(client is not null);
                    if (client is not null)
                    {
                        new ClientPackage(client.ID, client.TokenId, client.GUID).WriteData(writer);
                    }

                    var values = new ValuesDictionary();
                    player.Save(values);
                    writer.Write(values);
                }

                writer.WriteEntityLoadList(Entities);
                break;
            case EventType.Remove:
                writer.Write(EntityId);
                break;
            case EventType.RequestSync:
                writer.Write((ushort)EntityIdList.Count);
                foreach (var e in EntityIdList)
                {
                    writer.Write(e);
                }

                break;
        }
    }

    public void ReadData(PackageStreamReader reader)
    {
        Type = reader.ReadEnum<EventType>();
        switch (Type)
        {
            case EventType.LoadOne:
            case EventType.LoadList:
                var count = reader.ReadInt32();
                for (var i = 0; i < count; i++)
                {
                    if (reader.ReadBoolean())
                    {
                        var client = new ClientPackage();
                        client.ReadData(reader);
                        Clients.Add(client);
                    }

                    Players.Add(reader.ReadValuesDictionary());
                }

                EntityData = reader.ReadBuff();
                break;
            case EventType.Remove:
                EntityId = reader.ReadInt32();
                break;
            case EventType.RequestSync:
                var m = reader.ReadUInt16();
                EntityIdList = [];
                for (var i = 0; i < m; i++)
                {
                    EntityIdList.Add(reader.ReadInt32());
                }

                break;
        }
    }


    public static bool ShouldSendEntityToClients(Entity entity)
    {
        if (RunMode.Value is RunModeType.Gui)
        {
            return true;
        }

        var componentPlayer = entity.FindComponent<ComponentPlayer>();
        return componentPlayer is null || componentPlayer.PlayerData.Client is not null;
    }
}
