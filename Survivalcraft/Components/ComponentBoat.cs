using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Components;

public class ComponentBoat : Component, IUpdateable
{
    private ComponentBody _componentBody = null!;

    private ComponentDamage _componentDamage = null!;

    private ComponentMount _componentMount = null!;

    private SubsystemAudio _subsystemAudio = null!;

    private SubsystemTime _subsystemTime = null!;

    private float _turnSpeed;

    public float MoveOrder { get; set; }

    public float TurnOrder { get; set; }

    public float Health { get; set; }

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public void Update(float dt)
    {
        if (_componentDamage.HitPoints < 0.33f)
        {
            _componentBody.Density = 1.15f;
            if (_componentDamage.HitPoints - _componentDamage.HitPointsChange >= 0.33f &&
                _componentBody.ImmersionFactor > 0f)
            {
                _subsystemAudio.PlaySound("Audio/Sinking", 1f, 0f, _componentBody.Position, 4f, true);
            }
        }
        else if (_componentDamage.HitPoints < 0.66f)
        {
            _componentBody.Density = 0.7f;
            if (_componentDamage.HitPoints - _componentDamage.HitPointsChange >= 0.66f &&
                _componentBody.ImmersionFactor > 0f)
            {
                _subsystemAudio.PlaySound("Audio/Sinking", 1f, 0f, _componentBody.Position, 4f, true);
            }
        }

        var num = _componentBody.ImmersionFactor > 0.95f;
        var num2 = !num && _componentBody is { ImmersionFactor: > 0.01f, StandingOnValue: null, StandingOnBody: null };
        _turnSpeed += 2.5f * _subsystemTime.GameTimeDelta * (1f * TurnOrder - _turnSpeed);
        var rotation = _componentBody.Rotation;
        var num3 = MathUtils.Atan2(2f * rotation.Y * rotation.W - 2f * rotation.X * rotation.Z,
            1f - 2f * rotation.Y * rotation.Y - 2f * rotation.Z * rotation.Z);
        if (num2)
        {
            num3 -= _turnSpeed * dt;
        }

        _componentBody.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, num3);
        if (num2 && MoveOrder != 0f)
        {
            _componentBody.Velocity += dt * 3f * MoveOrder * _componentBody.Matrix.Forward;
        }

        if (num)
        {
            _componentDamage.Damage(0.005f * dt);
            _componentMount.Rider?.StartDismounting();
        }

        MoveOrder = 0f;
        TurnOrder = 0f;
    }

    public void Injure(float amount, ComponentCreature attacker, bool ignoreInvulnerability)
    {
        if (amount > 0f)
        {
            Health = MathUtils.Max(Health - amount, 0f);
        }
    }

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        _subsystemTime = Project.FindSubsystem<SubsystemTime>(true)!;
        _subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true)!;
        _componentMount = Entity.FindComponent<ComponentMount>(true)!;
        _componentBody = Entity.FindComponent<ComponentBody>(true)!;
        _componentDamage = Entity.FindComponent<ComponentDamage>(true)!;
    }
}
