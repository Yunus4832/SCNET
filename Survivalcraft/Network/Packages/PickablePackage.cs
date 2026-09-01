using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

public class PickablePackage : IPackage
{
    public enum PickType
    {
        Create,
        Update,
        Delete,
        RequestSync,
        SetFlyToPosition,
        CreateList,
        DeleteList,
        SyncList
    }

    public byte Count;

    public Vector3? FlyToPosition;

    public byte? GetPlayer;

    public ushort Id;

    public readonly List<Pickable> Pickables = [];

    public Vector3 Position;

    public PickType Type;

    public int Value;

    public Vector3 Velocity;

    public bool PlaySound;

    public Matrix? StuckMatrix;

    public byte ID => (byte)PackageType.Pickable;

    public Client? To { get; set; }

    public Client? Except { get; set; }

    public Client? From { get; set; }

    public ClientState MinNeedState => ClientState.ProjectLoaded;

    public PickablePackage()
    {
    }

    public PickablePackage(List<Pickable> pickables, PickType type = PickType.Update)
    {
        Type = type;
        Pickables.AddRange(pickables);
    }

    public PickablePackage(Pickable pickable, PickType pickType)
    {
        Id = pickable.Id;
        Value = pickable.Value;
        Count = (byte)pickable.Count;
        Position = pickable.Position;
        Velocity = pickable.Velocity;
        GetPlayer = pickable.GetPickPlayer;
        StuckMatrix = pickable.StuckMatrix;
        FlyToPosition = pickable.FlyToPosition;
        Type = pickType;
        PlaySound = pickable.PlaySound;
    }

    public PickablePackage(ushort pickId)
    {
        Id = pickId;
        Type = PickType.Delete;
    }

    public void WriteData(PackageStreamWriter writer)
    {
        if (GameManager.Project is null)
        {
            return;
        }

        var subsystemPickables = GameManager.Project.FindSubsystem<SubsystemPickables>(true)!;
        writer.WriteEnum(Type);
        switch (Type)
        {
            case PickType.Create:
                writer.Write(Id);
                writer.Write(Count);
                writer.Write(Value);
                writer.Write(Position);
                writer.Write(Velocity);
                writer.Write(StuckMatrix);
                break;
            case PickType.Update:
                writer.Write(Pickables.Count);
                foreach (var t in Pickables)
                {
                    writer.Write(t.Id);
                    writer.Write(t.Position);
                }

                break;
            case PickType.SetFlyToPosition:
                writer.Write(Id);
                if (FlyToPosition != null)
                {
                    writer.Write(FlyToPosition.Value);
                }

                break;
            case PickType.Delete:
                writer.Write(Id);
                writer.Write(PlaySound);
                break;
            case PickType.CreateList:
                writer.Write(Pickables.Count);
                foreach (var pickable in Pickables)
                {
                    writer.Write(pickable.Id);
                    writer.Write(pickable.Count);
                    writer.Write(pickable.Value);
                    writer.Write(pickable.Position);
                    writer.Write(pickable.Velocity);
                    writer.Write(pickable.StuckMatrix);
                }

                break;
            case PickType.DeleteList:
                writer.Write(Pickables.Count);
                foreach (var pickable in Pickables)
                {
                    writer.Write(pickable.Id);
                }

                break;
            case PickType.RequestSync:
                writer.Write(Id);
                break;
            case PickType.SyncList:
            {
                var pickables = subsystemPickables.Pickables;
                writer.Write(pickables.Count);
                foreach (var pickable in pickables)
                {
                    writer.Write(pickable.Id);
                    writer.Write(pickable.Count);
                    writer.Write(pickable.Value);
                    writer.Write(pickable.Position);
                    writer.Write(pickable.Velocity);
                    writer.Write(pickable.StuckMatrix);
                }
            }
                break;
        }
    }

    public void ReadData(PackageStreamReader reader)
    {
        Type = reader.ReadEnum<PickType>();
        switch (Type)
        {
            case PickType.Create:
                Id = reader.ReadUInt16();
                Count = reader.ReadByte();
                Value = reader.ReadInt32();
                Position = reader.ReadVector3();
                Velocity = reader.ReadVector3();
                StuckMatrix = reader.ReadMatrixNullable();
                break;
            case PickType.Update:
            {
                var count = reader.ReadInt32();
                for (var i = 0; i < count; i++)
                {
                    var pickable = new Pickable
                    {
                        Id = reader.ReadUInt16(),
                        Position = reader.ReadVector3()
                    };
                    Pickables.Add(pickable);
                }
            }
                break;
            case PickType.SetFlyToPosition:
                Id = reader.ReadUInt16();
                FlyToPosition = reader.ReadVector3();
                break;
            case PickType.Delete:
                Id = reader.ReadUInt16();
                PlaySound = reader.ReadBoolean();
                break;
            case PickType.CreateList:
            {
                var count = reader.ReadInt32();
                for (var i = 0; i < count; i++)
                {
                    var pickable = new Pickable
                    {
                        Id = reader.ReadUInt16(),
                        Count = reader.ReadInt32(),
                        Value = reader.ReadInt32(),
                        Position = reader.ReadVector3(),
                        Velocity = reader.ReadVector3(),
                        StuckMatrix = reader.ReadMatrixNullable()
                    };
                    Pickables.Add(pickable);
                }
            }
                break;
            case PickType.DeleteList:
            {
                var count = reader.ReadInt32();
                for (var i = 0; i < count; i++)
                {
                    var pickable = new Pickable
                    {
                        Id = reader.ReadUInt16()
                    };
                    Pickables.Add(pickable);
                }
            }
                break;
            case PickType.RequestSync:
                Id = reader.ReadUInt16();
                break;
            case PickType.SyncList:
            {
                var count = reader.ReadInt32();
                for (var i = 0; i < count; i++)
                {
                    var pickable = new Pickable
                    {
                        Id = reader.ReadUInt16(),
                        Count = reader.ReadInt32(),
                        Value = reader.ReadInt32(),
                        Position = reader.ReadVector3(),
                        Velocity = reader.ReadVector3(),
                        StuckMatrix = reader.ReadMatrixNullable()
                    };
                    Pickables.Add(pickable);
                }
            }
                break;
        }
    }
}
