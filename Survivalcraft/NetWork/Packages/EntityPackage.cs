using EntitySystem.Core;

namespace Game.NetWork.Packages;

public class EntityPackage : IPackage
{
    public enum EventType
    {
        Remove,
        LoadList,
        LoadOne,
        RequestSync
    }

    private List<Entity> _entities = [];

    private ushort _entityId;

    private List<ushort> _entityIdList = [];

    private EventType _type;

    public byte ID => (byte)PackageType.Entity;

    public Client? To { get; set; }

    public Client? Except { get; set; }

    public Client? From { get; set; }

    public ClientState MinNeedState => ClientState.ProjectLoaded;


    public EntityPackage()
    {
    }

    public EntityPackage(ushort id)
    {
        _type = EventType.Remove;
        _entityId = id;
    }

    public EntityPackage(List<ushort> idList)
    {
        _type = EventType.RequestSync;
        _entityIdList.AddRange(idList);
    }

    public EntityPackage(Entity entity)
    {
        _type = EventType.LoadOne;
        _entities = [entity];
    }

    public EntityPackage(List<Entity> entities)
    {
        _type = EventType.LoadList;
        _entities.AddRange(entities);
    }

    public void WriteData(PackageStreamWriter writer)
    {
        writer.WriteEnum(_type);
        switch (_type)
        {
            case EventType.LoadOne:
            case EventType.LoadList:
                writer.WriteEntityLoadList(_entities);
                break;
            case EventType.Remove:
                writer.Write(_entityId);
                break;
            case EventType.RequestSync:
                writer.Write((ushort)_entityIdList.Count);
                foreach (var e in _entityIdList)
                {
                    writer.Write(e);
                }

                break;
        }
    }

    public void ReadData(PackageStreamReader reader)
    {
        _type = reader.ReadEnum<EventType>();
        switch (_type)
        {
            case EventType.LoadOne:
            case EventType.LoadList:
                _entities = reader.ReadEntityLoadList();
                break;
            case EventType.Remove:
                _entityId = reader.ReadUInt16();
                break;
            case EventType.RequestSync:
                var m = reader.ReadUInt16();
                _entityIdList = [];
                for (var i = 0; i < m; i++)
                {
                    _entityIdList.Add(reader.ReadUInt16());
                }

                break;
        }
    }

    public void Handle(ProjectNet projectNet, NetNode netNode, bool isServer)
    {
        var el = new List<Entity>();
        switch (_type)
        {
            case EventType.LoadOne:
            case EventType.LoadList:
                foreach (var e in _entities)
                    //如果本地ID没有重复的添加，重复了进行替换
                {
                    if (!projectNet.FindEntityById(e.EntityId, e2 =>
                        {
                            projectNet.RemoveEntity(e2, true);
                            projectNet.AddEntity(e);
#if DEBUG
                            Log.Information($"生物替换[{e.EntityId}]");
#endif
                        }))
                    {
                        el.Add(e);
                    }
                }

                projectNet.AddEntities(el);
                break;
            case EventType.Remove:
                projectNet.FindEntityById(_entityId, entity => { projectNet.RemoveEntity(entity, true); });
                break;
            case EventType.RequestSync:
                foreach (var e in _entityIdList)
                {
                    projectNet.FindEntityById(e, entity => { el.Add(entity); });
                }

                netNode.QueuePackage(new EntityPackage(el) { To = From });
                break;
        }
    }
}
