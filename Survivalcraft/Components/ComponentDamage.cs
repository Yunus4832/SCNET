using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Network;
using Game.Network.Enums;
using Game.Network.Packages;

namespace Game.Components;

public class ComponentDamage : Component, IUpdateable
{
    private ComponentBody _componentBody = null!;

    private ComponentOnFire? _componentOnFire;

    private float _debrisScale;

    private float _debrisStrength;

    private int _debrisTextureSlot;

    private float _fallResilience;

    private float _fireResilience;

    private float _lastHitPoints;

    private SubsystemAudio _subsystemAudio = null!;

    private SubsystemParticles _subsystemParticles = null!;

    private SubsystemTerrain _subsystemTerrain = null!;

    public float HitPoints { get; set; }

    public float HitPointsChange { get; set; }

    public float AttackResilience { get; set; }

    public string DamageSoundName { get; set; } = string.Empty;

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public void Update(float dt)
    {
        var position = _componentBody.Position;
        if (HitPoints <= 0f)
        {
            if (CommonLib.WorkType != WorkType.Client)
            {
                CommonLib.Net.QueuePackage(new ComponentHealthPackage(this));
            }

            _subsystemParticles.AddParticleSystem(new BlockDebrisParticleSystem(_subsystemTerrain,
                position + _componentBody.StanceBoxSize.Y / 2f * Vector3.UnitY, _debrisStrength, _debrisScale,
                Color.White, _debrisTextureSlot));
            _subsystemAudio.PlayRandomSound(DamageSoundName, 1f, 0f, _componentBody.Position, 4f, true);
            Project.RemoveEntity(Entity, true);
        }

        var num = MathUtils.Abs(_componentBody.CollisionVelocityChange.Y);
        if (num > _fallResilience)
        {
            var amount = MathUtils.Sqr(MathUtils.Max(num - _fallResilience, 0f)) / 15f;
            Damage(amount);
        }

        if (position.Y is < -10f or > 276f)
        {
            Damage(HitPoints);
        }

        if (_componentOnFire != null && (_componentOnFire.IsOnFire || _componentOnFire.TouchesFire))
        {
            Damage(dt / _fireResilience);
        }

        HitPointsChange = HitPoints - _lastHitPoints;
        _lastHitPoints = HitPoints;
    }

    public void Damage(float amount)
    {
        if (CommonLib.WorkType == WorkType.Client)
        {
            return;
        }

        if (amount > 0f && HitPoints > 0f)
        {
            HitPoints = MathUtils.Max(HitPoints - amount, 0f);
        }

        CommonLib.Net.QueuePackage(new ComponentHealthPackage(this));
    }

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        _subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true)!;
        _subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true)!;
        _subsystemParticles = Project.FindSubsystem<SubsystemParticles>(true)!;
        _componentBody = Entity.FindComponent<ComponentBody>(true)!;
        _componentOnFire = Entity.FindComponent<ComponentOnFire>();
        HitPoints = valuesDictionary.GetValue<float>("HitPoints");
        AttackResilience = valuesDictionary.GetValue<float>("AttackResilience");
        _fallResilience = valuesDictionary.GetValue<float>("FallResilience");
        _fireResilience = valuesDictionary.GetValue<float>("FireResilience");
        _debrisTextureSlot = valuesDictionary.GetValue<int>("DebrisTextureSlot");
        _debrisStrength = valuesDictionary.GetValue<float>("DebrisStrength");
        _debrisScale = valuesDictionary.GetValue<float>("DebrisScale");
        DamageSoundName = valuesDictionary.GetValue<string>("DestructionSoundName");
    }

    public override void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap)
    {
        valuesDictionary.SetValue("HitPoints", HitPoints);
    }
}
