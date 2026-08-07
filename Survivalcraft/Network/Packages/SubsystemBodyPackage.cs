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

    public readonly List<BodyItem> BodyList = [];

    public int CreatureId;

    public ComponentModel? CreatureModel;

    public EventType PackageEventType;

    public Vector3 Impulse;

    public int TargetCreatureId;

    public byte ID => (byte)PackageType.SubsystemBody;

    public Client? To { get; set; }

    public Client? Except { get; set; }

    public Client? From { get; set; }

    public ClientState MinNeedState => ClientState.Playing;

    /// <summary>
    /// 状态流轮次序号：同一轮拆出的所有生物包共享同一个值，
    /// 客户端按实体比较此序号丢弃旧包，实现“最新优先”。
    /// </summary>
    public uint StateTick;

    public SubsystemBodyPackage()
    {
    }

    public SubsystemBodyPackage(List<ComponentBody> bodies)
    {
        PackageEventType = EventType.BodyUpdate;
        foreach (var b in bodies)
        {
            AddItem(b);
        }
    }

    public SubsystemBodyPackage(ComponentBody body, Vector3 vector)
    {
        CreatureId = body.Entity.EntityId;
        PackageEventType = EventType.ApplyImpulse;
        Impulse = vector;
    }

    public SubsystemBodyPackage(ComponentBody from, ComponentBody target, Vector3 velocity)
    {
        PackageEventType = EventType.HandleAxisCollision;
        CreatureId = from.Entity.EntityId;
        TargetCreatureId = target.Entity.EntityId;
        Impulse = velocity;
    }

    public void ReadData(PackageStreamReader reader)
    {
        PackageEventType = reader.ReadEnum<EventType>();
        switch (PackageEventType)
        {
            case EventType.BodyUpdate:
                StateTick = reader.ReadUInt32();
                var cnt = reader.ReadUInt16();
                for (ushort i = 0; i < cnt; i++)
                {
                    BodyList.Add(ReadItem(reader));
                }

                break;
            case EventType.HandleAxisCollision:
                CreatureId = reader.ReadInt32();
                TargetCreatureId = reader.ReadInt32();
                Impulse = reader.ReadVector3();
                break;
            case EventType.ApplyImpulse:
                CreatureId = reader.ReadInt32();
                Impulse = reader.ReadVector3();
                break;
        }
    }

    public void WriteData(PackageStreamWriter writer)
    {
        writer.WriteEnum(PackageEventType);
        switch (PackageEventType)
        {
            case EventType.BodyUpdate:
                writer.Write(StateTick);
                writer.Write((ushort)BodyList.Count);
                foreach (var i in BodyList)
                {
                    WriteItem(writer, i);
                }

                break;
            case EventType.HandleAxisCollision:
                writer.Write(CreatureId);
                writer.Write(TargetCreatureId);
                writer.Write(Impulse);
                break;
            case EventType.ApplyImpulse:
                writer.Write(CreatureId);
                writer.Write(Impulse);
                break;
        }
    }


    public void AddItem(ComponentBody body)
    {
        // 每轮发送完整当前状态（位置/旋转/速度/视线角），快照自包含：
        // 丢包后下一轮即恢复，不会因“停止移动前最后一包丢失”而长期停在旧位置。
        var bodyItem = new BodyItem
        {
            CreatureId = body.Entity.EntityId
        };
        bodyItem.ChangeFlag |= ChangeFlag.PositionChange;
        bodyItem.Position = body.Position;
        bodyItem.ChangeFlag |= ChangeFlag.RotationChange;
        bodyItem.Rotation = body.Rotation;
        bodyItem.ChangeFlag |= ChangeFlag.VelocityChange;
        bodyItem.Velocity = body.Velocity;

        if (body.Locomotion != null)
        {
            bodyItem.ChangeFlag |= ChangeFlag.LookAnglesChange;
            bodyItem.LookAngles = body.Locomotion.LookAngles;

            if (body.Locomotion.FlyOrderChange)
            {
                bodyItem.ChangeFlag |= ChangeFlag.FlyOrderChange;
                bodyItem.FlyOrder = body.Locomotion.LastFlyOrder;
            }
        }

        BodyList.Add(bodyItem);
    }

    public BodyItem ReadItem(PackageStreamReader reader)
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

    public void WriteItem(PackageStreamWriter writer, BodyItem bodyItem)
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

    public struct BodyItem
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
