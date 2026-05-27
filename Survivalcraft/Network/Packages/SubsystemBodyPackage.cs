using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

public class SubsystemBodyPackage : IPackage
{
    [Flags]
    public enum ChangeFlag : byte
    {
        None = 0,
        LookAnglesChange = 1,
        FlyOrderChange = 2,
        PositionChange = 4,
        RotationChange = 8,
        VelocityChange = 16
    }

    public enum EventType
    {
        BodyUpdate,
        ApplyImpulse,
        HandleAxisCollision
    }

    private readonly List<BodyItem> _bodyList = [];

    private int _creatureId;

    public ComponentModel? CreatureModel;

    private EventType _eventType;

    private Vector3 _impulse;

    private int _targetCreatureId;

    public byte ID => (byte)PackageType.SubsystemBody;

    public Client? To { get; set; }

    public Client? Except { get; set; }

    public Client? From { get; set; }

    public ClientState MinNeedState => ClientState.Playing;

    public SubsystemBodyPackage()
    {
    }

    public SubsystemBodyPackage(List<ComponentBody> bodies)
    {
        _eventType = EventType.BodyUpdate;
        foreach (var b in bodies)
        {
            AddItem(b);
        }
    }

    public SubsystemBodyPackage(ComponentBody body, Vector3 vector)
    {
        _creatureId = body.Entity.EntityId;
        _eventType = EventType.ApplyImpulse;
        _impulse = vector;
    }

    public SubsystemBodyPackage(ComponentBody from, ComponentBody target, Vector3 velocity)
    {
        _eventType = EventType.HandleAxisCollision;
        _creatureId = from.Entity.EntityId;
        _targetCreatureId = target.Entity.EntityId;
        _impulse = velocity;
    }

    public void ReadData(PackageStreamReader reader)
    {
        _eventType = reader.ReadEnum<EventType>();
        switch (_eventType)
        {
            case EventType.BodyUpdate:
                var cnt = reader.ReadUInt16();
                for (ushort i = 0; i < cnt; i++)
                {
                    _bodyList.Add(ReadItem(reader));
                }

                break;
            case EventType.HandleAxisCollision:
                _creatureId = reader.ReadInt32();
                _targetCreatureId = reader.ReadInt32();
                _impulse = reader.ReadVector3();
                break;
            case EventType.ApplyImpulse:
                _creatureId = reader.ReadInt32();
                _impulse = reader.ReadVector3();
                break;
        }
    }

    public void WriteData(PackageStreamWriter writer)
    {
        writer.WriteEnum(_eventType);
        switch (_eventType)
        {
            case EventType.BodyUpdate:
                writer.Write((ushort)_bodyList.Count);
                foreach (var i in _bodyList)
                {
                    WriteItem(writer, i);
                }

                break;
            case EventType.HandleAxisCollision:
                writer.Write(_creatureId);
                writer.Write(_targetCreatureId);
                writer.Write(_impulse);
                break;
            case EventType.ApplyImpulse:
                writer.Write(_creatureId);
                writer.Write(_impulse);
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
        switch (_eventType)
        {
            case EventType.BodyUpdate:
                var bodies = project.FindSubsystem<SubsystemBodies>(true)!;
                var ml = new List<int>();
                var rl = new List<ComponentBody>();
                //服务器的动物列表
                foreach (var item in _bodyList)
                {
                    bodies.FindBodyByCreatureID(
                        item.CreatureId,
                        body =>
                        {
                            if (item.ChangeFlag.HasFlag(ChangeFlag.PositionChange))
                            {
                                body.NetPosition.SetNext(item.Position);
                            }

                            if (item.ChangeFlag.HasFlag(ChangeFlag.RotationChange))
                            {
                                body.NetRotation.SetNext(item.Rotation);
                            }

                            if (item.ChangeFlag.HasFlag(ChangeFlag.VelocityChange))
                            {
                                body.NetVelocity.SetNext(item.Velocity);
                            }

                            if (body.Locomotion == null)
                            {
                                return;
                            }

                            if (item.ChangeFlag.HasFlag(ChangeFlag.LookAnglesChange))
                            {
                                body.Locomotion.NetLookAngles.SetNext(item.LookAngles);
                            }

                            if (item.ChangeFlag.HasFlag(ChangeFlag.FlyOrderChange))
                            {
                                body.Locomotion.LastFlyOrder = item.FlyOrder;
                            }
                        },
                        () =>
                        {
                            //本地没有这个动物，向服务器请求
                            ml.Add(item.CreatureId);
                        }
                    );
                }

                foreach (var item2 in bodies.Bodies)
                {
                    BodyItem? m = _bodyList.Find(x => x.CreatureId == item2.Entity.EntityId);
                    if (!m.HasValue)
                    {
                        rl.Add(item2);
                    }
                }

                if (ml.Count > 0)
                {
                    netNode.QueuePackage(new EntityPackage(ml));
                }

                if (rl.Count > 0)
                {
                    foreach (var b in rl)
                    {
                        project.RemoveEntity(b.Entity, true);
                    }
                }

                break;
            case EventType.HandleAxisCollision:
                project.FindSubsystem<SubsystemBodies>(true)!.FindBodyByCreatureID(_creatureId, from =>
                {
                    project.FindSubsystem<SubsystemBodies>(true)!.FindBodyByCreatureID(_targetCreatureId, target =>
                    {
                        target.Velocity = _impulse;
                        target.NetVelocity.SetNext(_impulse);
                        from.CollidedWithBody?.Invoke(target);
                        target.CollidedWithBody?.Invoke(from);
                    });
                });
                break;
            case EventType.ApplyImpulse:
                project.FindSubsystem<SubsystemBodies>(true)!
                    .FindBodyByCreatureID(_creatureId, body => { body.ApplyImpulseNet(_impulse); });
                break;
        }
    }

    public void AddItem(ComponentBody body)
    {
        var bodyItem = new BodyItem
        {
            CreatureId = body.Entity.EntityId
        };
        if (body.SendPosition.HasValue)
        {
            bodyItem.ChangeFlag |= ChangeFlag.PositionChange;
            bodyItem.Position = body.SendPosition.Value;
        }

        if (body.SendRotation.HasValue)
        {
            bodyItem.ChangeFlag |= ChangeFlag.RotationChange;
            bodyItem.Rotation = body.SendRotation.Value;
        }

        if (body.SendVelocity.HasValue)
        {
            bodyItem.ChangeFlag |= ChangeFlag.VelocityChange;
            bodyItem.Velocity = body.SendVelocity.Value;
        }

        if (body.Locomotion != null)
        {
            if (body.Locomotion.SendLookAngles.HasValue)
            {
                bodyItem.ChangeFlag |= ChangeFlag.LookAnglesChange;
                bodyItem.LookAngles = body.Locomotion.SendLookAngles.Value;
            }

            if (body.Locomotion.FlyOrderChange)
            {
                bodyItem.ChangeFlag |= ChangeFlag.FlyOrderChange;
                bodyItem.FlyOrder = body.Locomotion.LastFlyOrder;
            }
        }

        _bodyList.Add(bodyItem);
    }

    private BodyItem ReadItem(PackageStreamReader reader)
    {
        var bodyItem = new BodyItem
        {
            ChangeFlag = reader.ReadEnum<ChangeFlag>(),
            CreatureId = reader.ReadInt32()
        };

        if (bodyItem.ChangeFlag.HasFlag(ChangeFlag.PositionChange))
        {
            bodyItem.Position = reader.ReadVector3();
        }

        if (bodyItem.ChangeFlag.HasFlag(ChangeFlag.RotationChange))
        {
            bodyItem.Rotation = reader.ReadQuaternion();
        }

        if (bodyItem.ChangeFlag.HasFlag(ChangeFlag.VelocityChange))
        {
            bodyItem.Velocity = reader.ReadVector3();
        }

        if (bodyItem.ChangeFlag.HasFlag(ChangeFlag.LookAnglesChange))
        {
            bodyItem.LookAngles = reader.ReadVector2();
        }

        if (bodyItem.ChangeFlag.HasFlag(ChangeFlag.FlyOrderChange))
        {
            bodyItem.FlyOrder = reader.ReadVector3Nullable();
        }

        return bodyItem;
    }

    private void WriteItem(PackageStreamWriter writer, BodyItem bodyItem)
    {
        writer.WriteEnum(bodyItem.ChangeFlag);
        writer.Write(bodyItem.CreatureId);
        if (bodyItem.ChangeFlag.HasFlag(ChangeFlag.PositionChange))
        {
            writer.Write(bodyItem.Position);
        }

        if (bodyItem.ChangeFlag.HasFlag(ChangeFlag.RotationChange))
        {
            writer.Write(bodyItem.Rotation);
        }

        if (bodyItem.ChangeFlag.HasFlag(ChangeFlag.VelocityChange))
        {
            writer.Write(bodyItem.Velocity);
        }

        if (bodyItem.ChangeFlag.HasFlag(ChangeFlag.LookAnglesChange))
        {
            writer.Write(bodyItem.LookAngles);
        }

        if (bodyItem.ChangeFlag.HasFlag(ChangeFlag.FlyOrderChange))
        {
            writer.Write(bodyItem.FlyOrder);
        }
    }

    private struct BodyItem
    {
        public int CreatureId;

        public Vector3 Position;

        public Vector3 Velocity;

        public Quaternion Rotation;

        public Vector2 LookAngles;

        public Vector3? FlyOrder;

        public ChangeFlag ChangeFlag;
    }
}
