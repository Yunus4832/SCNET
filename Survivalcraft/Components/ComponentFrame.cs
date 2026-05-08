using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;
using Game.NetWork;

namespace Game.Components;

public class ComponentFrame : Component
{
    //简易内存修改检测
    private readonly SafeFloat _px = new();

    private readonly SafeFloat _py = new();

    private readonly SafeFloat _pz = new();

    private bool _cachedMatrixValid;

    public Vector3? SendPosition;

    public Quaternion? SendRotation;

    public virtual Vector3 Position
    {
        get
        {
            if (field.X.UncloseTo(_px.Get()) ||
                field.Y.UncloseTo(_py.Get()) ||
                field.Z.UncloseTo(_pz.Get()))
            {
                Program.RamDataChangeException(GetType().FullName!, "Position");
            }

            return field;
        }
        set
        {
            if (value == field)
            {
                return;
            }

            _cachedMatrixValid = false;
            field = value;
            _px.Set(value.X);
            _py.Set(value.Y);
            _pz.Set(value.Z);
            PositionChanged?.Invoke(this);
        }
    }

    public Quaternion Rotation
    {
        get;
        set
        {
            value = Quaternion.Normalize(value);
            if (value == field)
            {
                return;
            }

            _cachedMatrixValid = false;
            field = value;
            RotationChanged?.Invoke(this);
        }
    }

    public Matrix Matrix
    {
        get
        {
            if (_cachedMatrixValid)
            {
                return field;
            }

            field = Matrix.CreateFromQuaternion(Rotation);
            field.Translation = Position;

            return field;
        }
    }

    public event Action<ComponentFrame>? PositionChanged;

    public event Action<ComponentFrame>? RotationChanged;

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        Position = valuesDictionary.GetValue<Vector3>("Position");
        Rotation = valuesDictionary.GetValue<Quaternion>("Rotation");
        RotationChanged += obj => { SendRotation = obj.Rotation; };
        PositionChanged += obj => { SendPosition = obj.Position; };
    }

    public override void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap)
    {
        valuesDictionary.SetValue("Position", Position);
        valuesDictionary.SetValue("Rotation", Rotation);
    }
}
