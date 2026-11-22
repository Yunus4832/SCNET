using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Components;

public class ComponentOnFire : Component, IUpdateable
{
    public UpdateExecutionSide UpdateSide => UpdateExecutionSide.Both;

    private float _fireDuration;

    private int _fireTouchCount;

    private double _nextCheckTime;

    private readonly Random _random = new();

    private SubsystemTerrain _subsystemTerrain = null!;

    private SubsystemTime _subsystemTime = null!;

    private ComponentBody ComponentBody { get; set; } = null!;

    public bool IsOnFire => _fireDuration > 0f;

    public float FireDuration => _fireDuration;

    public bool TouchesFire { get; set; }

    public ComponentCreature? Attacker { get; set; }

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public void Update(float dt)
    {
        if (!IsAddedToProject)
        {
            return;
        }

        var runtime = WorldRuntimeContext.For(Project);
        if (IsOnFire)
        {
            _fireDuration = MathUtils.Max(_fireDuration - dt, 0f);
            if (runtime.IsAuthority && ComponentBody is { ImmersionFactor: > 0.5f, ImmersionFluidBlock: WaterBlock })
            {
                Extinguish();
            }
        }

        if (!runtime.IsAuthority) { return; }

        if (!(_subsystemTime.GameTime > _nextCheckTime))
        {
            return;
        }

        _nextCheckTime = _subsystemTime.GameTime + _random.Float(0.9f, 1.1f);
        TouchesFire = CheckIfBodyTouchesFire();
        if (TouchesFire)
        {
            _fireTouchCount++;
            if (_fireTouchCount >= 5)
            {
                SetOnFire(null, _random.Float(12f, 15f));
            }
        }
        else
        {
            _fireTouchCount = 0;
        }

        if (ComponentBody is { ImmersionFactor: > 0.2f, ImmersionFluidBlock: MagmaBlock })
        {
            SetOnFire(null, _random.Float(12f, 15f));
        }
    }

    public void SetOnFire(ComponentCreature? attacker, float duration)
    {
        if (!WorldRuntimeContext.For(Project).IsAuthority)
        {
            return;
        }

        if (!float.IsFinite(duration) || duration <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(duration));
        }

        if (!IsOnFire) { Attacker = attacker; }

        _fireDuration = MathUtils.Max(_fireDuration, duration);
        NetworkSender.SendToObservers(Entity, EntityStatePackageBuilder.Capture(this));
    }

    public void ApplyFireState(ComponentCreature? attacker, float duration)
    {
        if (WorldRuntimeContext.For(Project).IsAuthority) { return; }

        Attacker = duration > 0f ? attacker : null;
        _fireDuration = duration;
    }

    public void Extinguish()
    {
        if (!WorldRuntimeContext.For(Project).IsAuthority || !IsOnFire) { return; }

        Attacker = null;
        _fireDuration = 0f;
        NetworkSender.SendToObservers(Entity, EntityStatePackageBuilder.Capture(this));
    }

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        _subsystemTime = Project.FindSubsystem<SubsystemTime>(true)!;
        _subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true)!;
        ComponentBody = Entity.FindComponent<ComponentBody>(true)!;
        var value = valuesDictionary.GetValue<float>("FireDuration");
        // Loading a baseline restores state; it is not an authority-side ignition event.
        _fireDuration = value > 0f ? value : 0f;
    }

    public override void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap)
    {
        valuesDictionary.SetValue("FireDuration", _fireDuration);
    }

    public bool CheckIfBodyTouchesFire()
    {
        var boundingBox = ComponentBody.BoundingBox;
        boundingBox.Min -= new Vector3(0.25f);
        boundingBox.Max += new Vector3(0.25f);
        var minX = Terrain.ToCell(boundingBox.Min.X);
        var minY = Terrain.ToCell(boundingBox.Min.Y);
        var minZ = Terrain.ToCell(boundingBox.Min.Z);
        var maxX = Terrain.ToCell(boundingBox.Max.X);
        var maxY = Terrain.ToCell(boundingBox.Max.Y);
        var maxZ = Terrain.ToCell(boundingBox.Max.Z);
        for (var i = minX; i <= maxX; i++)
        {
            for (var j = minY; j <= maxY; j++)
            {
                for (var k = minZ; k <= maxZ; k++)
                {
                    var cellValue = _subsystemTerrain.Terrain.GetCellValue(i, j, k);
                    var contents = Terrain.ExtractContents(cellValue);
                    var blockData = Terrain.ExtractData(cellValue);
                    switch (contents)
                    {
                        case 104:
                            if (blockData == 0)
                            {
                                var box2 = new BoundingBox(new Vector3(i, j, k), new Vector3(i + 1, j + 1, k + 1));
                                if (boundingBox.Intersection(box2))
                                {
                                    return true;
                                }

                                break;
                            }

                            if ((blockData & 1) != 0)
                            {
                                var box3 = new BoundingBox(
                                    new Vector3(i, j, k + 0.5f),
                                    new Vector3(i + 1, j + 1, k + 1)
                                );
                                if (boundingBox.Intersection(box3))
                                {
                                    return true;
                                }
                            }

                            if ((blockData & 2) != 0)
                            {
                                var box4 = new BoundingBox(
                                    new Vector3(i + 0.5f, j, k),
                                    new Vector3(i + 1, j + 1, k + 1)
                                );
                                if (boundingBox.Intersection(box4))
                                {
                                    return true;
                                }
                            }

                            if ((blockData & 4) != 0)
                            {
                                var box5 = new BoundingBox(new Vector3(i, j, k), new Vector3(i + 1, j + 1, k + 0.5f));
                                if (boundingBox.Intersection(box5))
                                {
                                    return true;
                                }
                            }

                            if ((blockData & 8) != 0)
                            {
                                var box6 = new BoundingBox(new Vector3(i, j, k), new Vector3(i + 0.5f, j + 1, k + 1));
                                if (boundingBox.Intersection(box6))
                                {
                                    return true;
                                }
                            }

                            break;
                        case 209:
                            if (blockData > 0)
                            {
                                var box = new BoundingBox(
                                    new Vector3(i, j, k) + new Vector3(0.2f),
                                    new Vector3(i + 1, j + 1, k + 1) - new Vector3(0.2f)
                                );
                                if (boundingBox.Intersection(box))
                                {
                                    return true;
                                }
                            }

                            break;
                    }
                }
            }
        }

        return false;
    }
}
