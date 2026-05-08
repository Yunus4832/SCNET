using EntitySystem.TemplatesDatabase;

namespace Game.NetWork.Packages;

public class MovingBlockPackage : IPackage
{
    [Flags]
    public enum EventType
    {
        Add = 1,
        Remove = 2,
        HagTag = 4,
        Stopped = 8
    }

    public ValuesDictionary? AddData;

    private Point3 _position;

    private EventType _type;

    private string _movingBlockId = string.Empty;

    public byte ID => (byte)PackageType.MovingBlockSet;

    public Client? To { get; set; }

    public Client? Except { get; set; }

    public Client? From { get; set; }

    public ClientState MinNeedState => ClientState.ProjectLoaded;

    public MovingBlockPackage()
    {
    }

    public MovingBlockPackage(SubsystemMovingBlocks.MovingBlockSet movingSet)
    {
        _type = EventType.Add;
        AddData = SubsystemMovingBlocks.SaveMovingItem(movingSet);
    }

    public MovingBlockPackage(IMovingBlockSet movingSet, bool stop)
    {
        _type = EventType.Remove;
        _movingBlockId = movingSet.Id;
        if (stop)
        {
            _type |= EventType.Stopped;
        }

        _type |= EventType.HagTag;
        _position = movingSet.Tag as Point3? ?? new Point3();
    }


    public void WriteData(PackageStreamWriter writer)
    {
        writer.WriteEnum(_type);
        switch (_type)
        {
            case EventType.Add:
                if (AddData != null)
                {
                    writer.Write(AddData);
                }

                break;
            default:
                writer.Write(_movingBlockId);
                if (_type.HasFlag(EventType.HagTag))
                {
                    writer.Write(_position);
                }

                break;
        }
    }

    public void ReadData(PackageStreamReader reader)
    {
        _type = reader.ReadEnum<EventType>();
        switch (_type)
        {
            case EventType.Add:
                AddData = reader.ReadValuesDictionary();
                break;
            default:
                _movingBlockId = reader.ReadString();
                if (_type.HasFlag(EventType.HagTag))
                {
                    _position = reader.ReadPoint3();
                }

                break;
        }
    }

    public void Handle(ProjectNet projectNet, NetNode netNode, bool isServer)
    {
        var obj = projectNet.FindSubsystem<SubsystemMovingBlocks>(true)!;
        switch (_type)
        {
            case EventType.Add:
                if (AddData != null)
                {
                    var m = obj.LoadAndAddMovingItem(AddData) as SubsystemMovingBlocks.MovingBlockSet;
                    var subsystemAudio = projectNet.FindSubsystem<SubsystemAudio>(true)!;
                    if (m != null)
                    {
                        obj.MovingBlockSets.Add(m);
                        if (m.Id == SubsystemPistonBlockBehavior.IdString)
                        {
                            subsystemAudio.PlaySound("Audio/Piston", 1f, 0f, m.Position, 2f, true);
                        }
                    }
                }

                break;
            default:
                var mm = _type.HasFlag(EventType.HagTag)
                    ? obj.FindMovingBlocks(_movingBlockId, _position)
                    : obj.FindMovingBlocks(_movingBlockId, null);
                if (mm != null)
                {
                    if (_type.HasFlag(EventType.Stopped) && mm is SubsystemMovingBlocks.MovingBlockSet blockSet)
                    {
                        obj.DoStop(blockSet);
                    }
                    else
                    {
                        obj.RemoveMovingBlockSetLogic(mm);
                    }
                }

                break;
        }
    }
}
