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

    private byte _count;

    private Vector3? _flyToPosition;

    private byte? _getPlayer;

    private ushort _id;

    private readonly List<Pickable> _pickables = [];

    private Vector3 _position;

    private PickType _type;

    private int _value;

    private Vector3 _velocity;

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
        _type = type;
        _pickables.AddRange(pickables);
    }

    public PickablePackage(Pickable pickable, PickType pickType)
    {
        _id = pickable.Id;
        _value = pickable.Value;
        _count = (byte)pickable.Count;
        _position = pickable.Position;
        _velocity = pickable.Velocity;
        _getPlayer = pickable.GetPickPlayer;
        StuckMatrix = pickable.StuckMatrix;
        _flyToPosition = pickable.FlyToPosition;
        _type = pickType;
        PlaySound = pickable.PlaySound;
    }

    public PickablePackage(ushort pickId)
    {
        _id = pickId;
        _type = PickType.Delete;
    }

    public void WriteData(PackageStreamWriter writer)
    {
        if (GameManager.Project is null)
        {
            return;
        }

        var subsystemPickables = GameManager.Project.FindSubsystem<SubsystemPickables>(true)!;
        writer.WriteEnum(_type);
        switch (_type)
        {
            case PickType.Create:
                writer.Write(_id);
                writer.Write(_count);
                writer.Write(_value);
                writer.Write(_position);
                writer.Write(_velocity);
                writer.Write(StuckMatrix);
                break;
            case PickType.Update:
                writer.Write(_pickables.Count);
                foreach (var t in _pickables)
                {
                    writer.Write(t.Id);
                    writer.Write(t.Position);
                }

                break;
            case PickType.SetFlyToPosition:
                writer.Write(_id);
                if (_flyToPosition != null)
                {
                    writer.Write(_flyToPosition.Value);
                }

                break;
            case PickType.Delete:
                writer.Write(_id);
                writer.Write(PlaySound);
                break;
            case PickType.CreateList:
                writer.Write(_pickables.Count);
                foreach (var pickable in _pickables)
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
                writer.Write(_pickables.Count);
                foreach (var pickable in _pickables)
                {
                    writer.Write(pickable.Id);
                }

                break;
            case PickType.RequestSync:
                writer.Write(_id);
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
        _type = reader.ReadEnum<PickType>();
        switch (_type)
        {
            case PickType.Create:
                _id = reader.ReadUInt16();
                _count = reader.ReadByte();
                _value = reader.ReadInt32();
                _position = reader.ReadVector3();
                _velocity = reader.ReadVector3();
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
                    _pickables.Add(pickable);
                }
            }
                break;
            case PickType.SetFlyToPosition:
                _id = reader.ReadUInt16();
                _flyToPosition = reader.ReadVector3();
                break;
            case PickType.Delete:
                _id = reader.ReadUInt16();
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
                    _pickables.Add(pickable);
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
                    _pickables.Add(pickable);
                }
            }
                break;
            case PickType.RequestSync:
                _id = reader.ReadUInt16();
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
                    _pickables.Add(pickable);
                }
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
        var subsystemPickable = project.FindSubsystem<SubsystemPickables>(true)!;
        switch (_type)
        {
            case PickType.Create:
                var tmp = subsystemPickable.Pickables.Find(p => p.Id == _id);
                if (tmp != null)
                {
                    tmp.Value = _value;
                    tmp.Count = _count;
                    tmp.Velocity = _velocity;
                    tmp.StuckMatrix = StuckMatrix;
                }
                else
                {
                    subsystemPickable.CreatePickable(_id, _value, _count, _position, _velocity, StuckMatrix);
                }

                break;
            case PickType.Update:
                foreach (var c in _pickables)
                {
                    subsystemPickable.PickableAction(c.Id, pick => { pick.Position = c.Position; });
                }

                foreach (var c in subsystemPickable.Pickables)
                {
                    if (_pickables.Find(x => x.Id == c.Id) == null)
                    {
                        subsystemPickable.PickablesToRemove.Add(c);
                    }
                }

                break;
            case PickType.Delete:
                subsystemPickable.PickableAction(
                    _id,
                    pick =>
                    {
                        if (PlaySound)
                        {
                            subsystemPickable.PlayPickableCollectedSound(pick);
                        }

                        subsystemPickable.RemovePickable(pick);
                    },
                    false
                );
                break;
            case PickType.RequestSync:
                var flag = subsystemPickable.PickableAction(
                    _id,
                    pick => { netNode.QueuePackage(new PickablePackage(pick, PickType.Create) { To = From }); }
                );
                if (!flag)
                {
                    netNode.QueuePackage(new PickablePackage(_id) { To = From });
                }

                break;
            case PickType.SetFlyToPosition:
                subsystemPickable.PickableAction(_id, pick => { pick.FlyToPosition = _flyToPosition; });
                break;
            case PickType.SyncList:
            case PickType.CreateList:
                if (isServer)
                {
                    break;
                }

                foreach (var pickable in _pickables)
                {
                    subsystemPickable.CreatePickable(pickable.Id, pickable.Value, pickable.Count, pickable.Position,
                        pickable.Velocity, pickable.StuckMatrix);
                }

                break;
            case PickType.DeleteList:
                if (isServer)
                {
                    break;
                }

                foreach (var pickable in _pickables)
                {
                    subsystemPickable.PickableAction(pickable.Id,
                        pick => { subsystemPickable.RemovePickable(pick); }, false);
                }

                break;
        }
    }
}
