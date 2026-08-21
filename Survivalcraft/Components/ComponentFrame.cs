using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Components;

public class ComponentFrame : Component
{
    private bool _cachedMatrixValid;

    public Vector3? SendPosition;

    public Quaternion? SendRotation;

    public virtual Vector3 Position
    {
        get;
        set
        {
            if (value == field)
            {
                return;
            }

            _cachedMatrixValid = false;
            field = value;
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
