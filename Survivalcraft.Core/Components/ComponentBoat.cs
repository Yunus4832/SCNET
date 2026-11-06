using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Components;

public class ComponentBoat : Component, IUpdateable
{
    public UpdateExecutionSide UpdateSide => UpdateExecutionSide.Both;

    private ComponentBody _componentBody = null!;

    private ComponentDamage _componentDamage = null!;

    private ComponentMount _componentMount = null!;

    private SubsystemTime _subsystemTime = null!;

    private float _turnSpeed;

    public float MoveOrder { get; set; }

    public float TurnOrder { get; set; }

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public void Update(float dt)
    {
        if (!WorldRuntimeContext.IsProjectActive(Project)) { return; }

        if (_componentDamage.HitPoints < .33f) { _componentBody.Density = 1.15f; }
        else if (_componentDamage.HitPoints < .66f) { _componentBody.Density = .7f; }

        var num = _componentBody.ImmersionFactor > 0.95f;
        var num2 = !num && _componentBody is { ImmersionFactor: > 0.01f, StandingOnValue: null, StandingOnBody: null };
        _turnSpeed += 2.5f * _subsystemTime.GameTimeDelta * (1f * TurnOrder - _turnSpeed);
        var rotation = _componentBody.Rotation;
        var num3 = MathUtils.Atan2(
            2f * rotation.Y * rotation.W - 2f * rotation.X * rotation.Z,
            1f - 2f * rotation.Y * rotation.Y - 2f * rotation.Z * rotation.Z
        );
        if (num2)
        {
            num3 -= _turnSpeed * dt;
        }

        _componentBody.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, num3);
        if (num2 && MoveOrder != 0f)
        {
            _componentBody.Velocity += dt * 3f * MoveOrder * _componentBody.Matrix.Forward;
        }

        if (num && WorldRuntimeContext.For(Project).IsAuthority)
        {
            _componentDamage.Damage(0.005f * dt);
            _componentMount.Rider?.StartDismounting();
        }

        MoveOrder = 0f;
        TurnOrder = 0f;
    }

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        _subsystemTime = Project.FindSubsystem<SubsystemTime>(true)!;
        _componentMount = Entity.FindComponent<ComponentMount>(true)!;
        _componentBody = Entity.FindComponent<ComponentBody>(true)!;
        _componentDamage = Entity.FindComponent<ComponentDamage>(true)!;
    }
}
