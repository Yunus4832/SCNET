using EntitySystem.Core;

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

    private List<Entity> _entities = [];

    private int _entityId;

    private List<int> _entityIdList = [];

    private EventType _type;

    public byte ID => (byte)PackageType.Entity;

    public Client? To { get; set; }

    public Client? Except { get; set; }

    public Client? From { get; set; }

    public ClientState MinNeedState => ClientState.ProjectLoaded;


    public EntityPackage()
    {
    }

    public EntityPackage(int id)
    {
        _type = EventType.Remove;
        _entityId = id;
    }

    public EntityPackage(List<int> idList)
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
                _entityId = reader.ReadInt32();
                break;
            case EventType.RequestSync:
                var m = reader.ReadUInt16();
                _entityIdList = [];
                for (var i = 0; i < m; i++)
                {
                    _entityIdList.Add(reader.ReadInt32());
                }

                break;
        }
    }

    public void Handle(NetNode netNode, bool isServer)
    {
        if (GameManager.Project is null)
        {
            return;
        }

        var project = GameManager.Project;
        var el = new List<Entity>();
        switch (_type)
        {
            case EventType.LoadOne:
            case EventType.LoadList:
                foreach (var e in _entities)
                    //如果本地ID没有重复的添加，重复了进行替换
                {
                    if (!project.FindEntityById(e.EntityId, e2 =>
                        {
                            project.RemoveEntity(e2, true);
                            project.AddEntity(e);
                        }))
                    {
                        el.Add(e);
                    }
                }

                project.AddEntities(el);
                break;
            case EventType.Remove:
                project.FindEntityById(_entityId, entity => { project.RemoveEntity(entity, true); });
                break;
            case EventType.RequestSync:
                foreach (var e in _entityIdList)
                {
                    project.FindEntityById(e, el.Add);
                }

                netNode.QueuePackage(new EntityPackage(el) { To = From });
                break;
        }
    }
}
