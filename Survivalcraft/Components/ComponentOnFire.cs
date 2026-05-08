using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;
using Game.NetWork;
using Game.NetWork.Packages;

namespace Game.Components;

public class ComponentOnFire : Component, IUpdateable
{
    private float _fireDuration;

    private int _fireTouchCount;

    private double _nextCheckTime;

    private OnFireParticleSystem? _onFireParticleSystem;

    private readonly Random _random = new();

    private float _soundVolume;

    private SubsystemAmbientSounds _subsystemAmbientSounds = null!;

    private SubsystemAudio _subsystemAudio = null!;

    private SubsystemParticles _subsystemParticles = null!;

    private SubsystemTerrain _subsystemTerrain = null!;

    private SubsystemTime _subsystemTime = null!;

    private ComponentBody ComponentBody { get; set; } = null!;

    public bool IsOnFire => _fireDuration > 0f;

    public bool TouchesFire { get; set; }

    public ComponentCreature? Attacker { get; set; }

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public void Update(float dt)
    {
        if (!IsAddedToProject)
        {
            return;
        }

        if (IsOnFire)
        {
            _fireDuration = MathUtils.Max(_fireDuration - dt, 0f);
            if (_onFireParticleSystem == null)
            {
                _onFireParticleSystem = new OnFireParticleSystem();
                _subsystemParticles.AddParticleSystem(_onFireParticleSystem);
            }

            var boundingBox = ComponentBody.BoundingBox;
            _onFireParticleSystem.Position = 0.5f * (boundingBox.Min + boundingBox.Max);
            _onFireParticleSystem.Radius = 0.5f * MathUtils.Min(boundingBox.Max.X - boundingBox.Min.X,
                boundingBox.Max.Z - boundingBox.Min.Z);
            if (ComponentBody is { ImmersionFactor: > 0.5f, ImmersionFluidBlock: WaterBlock })
            {
                Extinguish();
                _subsystemAudio.PlaySound("Audio/SizzleLong", 1f, 0f, _onFireParticleSystem.Position, 4f, true);
            }

            if (Time.PeriodicEvent(0.5, 0.0))
            {
                var distance = _subsystemAudio.CalculateListenerDistance(ComponentBody.Position);
                _soundVolume = _subsystemAudio.CalculateVolume(distance, 2f, 5f);
            }

            _subsystemAmbientSounds.FireSoundVolume =
                MathUtils.Max(_subsystemAmbientSounds.FireSoundVolume, _soundVolume);
        }
        else
        {
            if (_onFireParticleSystem != null)
            {
                _onFireParticleSystem.IsStopped = true;
                _onFireParticleSystem = null;
            }

            _soundVolume = 0f;
        }

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
        if (CommonLib.WorkType == WorkType.Client)
        {
            return;
        }

        CommonLib.Net.QueuePackage(new ComponentOnFirePackage(this, attacker, duration));
        SetOnFireNet(attacker, duration);
    }

    public void SetOnFireNet(ComponentCreature? attacker, float duration)
    {
        if (!IsOnFire)
        {
            Attacker = attacker;
        }

        _fireDuration = MathUtils.Max(_fireDuration, duration);
    }

    public void Extinguish()
    {
        Attacker = null;
        _fireDuration = 0f;
    }

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        _subsystemTime = Project.FindSubsystem<SubsystemTime>(true)!;
        _subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true)!;
        _subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true)!;
        _subsystemAmbientSounds = Project.FindSubsystem<SubsystemAmbientSounds>(true)!;
        _subsystemParticles = Project.FindSubsystem<SubsystemParticles>(true)!;
        ComponentBody = Entity.FindComponent<ComponentBody>(true)!;
        var value = valuesDictionary.GetValue<float>("FireDuration");
        if (value > 0f)
        {
            SetOnFire(null, value);
        }
    }

    public override void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap)
    {
        valuesDictionary.SetValue("FireDuration", _fireDuration);
    }

    public override void OnEntityRemoved()
    {
        if (_onFireParticleSystem != null)
        {
            _onFireParticleSystem.IsStopped = true;
        }
    }

    public bool CheckIfBodyTouchesFire()
    {
        var boundingBox = ComponentBody.BoundingBox;
        boundingBox.Min -= new Vector3(0.25f);
        boundingBox.Max += new Vector3(0.25f);
        var num = Terrain.ToCell(boundingBox.Min.X);
        var num2 = Terrain.ToCell(boundingBox.Min.Y);
        var num3 = Terrain.ToCell(boundingBox.Min.Z);
        var num4 = Terrain.ToCell(boundingBox.Max.X);
        var num5 = Terrain.ToCell(boundingBox.Max.Y);
        var num6 = Terrain.ToCell(boundingBox.Max.Z);
        for (var i = num; i <= num4; i++)
        for (var j = num2; j <= num5; j++)
        for (var k = num3; k <= num6; k++)
        {
            var cellValue = _subsystemTerrain.Terrain.GetCellValue(i, j, k);
            var num7 = Terrain.ExtractContents(cellValue);
            var num8 = Terrain.ExtractData(cellValue);
            switch (num7)
            {
                case 104:
                    if (num8 == 0)
                    {
                        var box2 = new BoundingBox(new Vector3(i, j, k), new Vector3(i + 1, j + 1, k + 1));
                        if (boundingBox.Intersection(box2))
                        {
                            return true;
                        }

                        break;
                    }

                    if ((num8 & 1) != 0)
                    {
                        var box3 = new BoundingBox(new Vector3(i, j, k + 0.5f), new Vector3(i + 1, j + 1, k + 1));
                        if (boundingBox.Intersection(box3))
                        {
                            return true;
                        }
                    }

                    if ((num8 & 2) != 0)
                    {
                        var box4 = new BoundingBox(new Vector3(i + 0.5f, j, k), new Vector3(i + 1, j + 1, k + 1));
                        if (boundingBox.Intersection(box4))
                        {
                            return true;
                        }
                    }

                    if ((num8 & 4) != 0)
                    {
                        var box5 = new BoundingBox(new Vector3(i, j, k), new Vector3(i + 1, j + 1, k + 0.5f));
                        if (boundingBox.Intersection(box5))
                        {
                            return true;
                        }
                    }

                    if ((num8 & 8) != 0)
                    {
                        var box6 = new BoundingBox(new Vector3(i, j, k), new Vector3(i + 0.5f, j + 1, k + 1));
                        if (boundingBox.Intersection(box6))
                        {
                            return true;
                        }
                    }

                    break;
                case 209:
                    if (num8 > 0)
                    {
                        var box = new BoundingBox(new Vector3(i, j, k) + new Vector3(0.2f),
                            new Vector3(i + 1, j + 1, k + 1) - new Vector3(0.2f));
                        if (boundingBox.Intersection(box))
                        {
                            return true;
                        }
                    }

                    break;
            }
        }

        return false;
    }
}
