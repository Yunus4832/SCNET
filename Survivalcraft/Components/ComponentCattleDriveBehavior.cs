using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Components;

public class ComponentCattleDriveBehavior : ComponentBehavior, IUpdateable, INoiseListener
{
    private ComponentCreature _componentCreature = null!;

    private ComponentHerdBehavior _componentHerdBehavior = null!;

    private ComponentPathfinding _componentPathfinding = null!;

    private Vector3 _driveVector;

    private float _importanceLevel;

    private readonly Random _random = new();

    private SubsystemCreatureSpawn _subsystemCreatureSpawn = null!;

    private SubsystemTime _subsystemTime = null!;

    public override float ImportanceLevel => _importanceLevel;

    public void HearNoise(ComponentBody? sourceBody, Vector3 sourcePosition, float loudness)
    {
        if (!(loudness >= 0.5f))
        {
            return;
        }

        var v = _componentCreature.ComponentBody.Position - sourcePosition;
        _driveVector += Vector3.Normalize(v) * MathUtils.Max(8f - 0.25f * v.Length(), 1f);
        var num = 12f + _random.Float(0f, 3f);
        if (_driveVector.Length() > num)
        {
            _driveVector = num * Vector3.Normalize(_driveVector);
        }
    }

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public void Update(float dt)
    {
        stateMachine.Update();
    }

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        _subsystemTime = Project.FindSubsystem<SubsystemTime>(true)!;
        _subsystemCreatureSpawn = Project.FindSubsystem<SubsystemCreatureSpawn>(true)!;
        _componentCreature = Entity.FindComponent<ComponentCreature>(true)!;
        _componentPathfinding = Entity.FindComponent<ComponentPathfinding>(true)!;
        _componentHerdBehavior = Entity.FindComponent<ComponentHerdBehavior>(true)!;
        stateMachine.AddState(
            "Inactive",
            delegate
            {
                _importanceLevel = 0f;
                _driveVector = Vector3.Zero;
            },
            delegate
            {
                if (IsActive)
                {
                    stateMachine.TransitionTo("Drive");
                }

                if (_driveVector.Length() > 3f)
                {
                    _importanceLevel = 7f;
                }

                FadeDriveVector();
            },
            Actions.Empty
        );
        stateMachine.AddState(
            "Drive",
            Actions.Empty,
            delegate
            {
                if (!IsActive)
                {
                    stateMachine.TransitionTo("Inactive");
                }

                if (_driveVector.LengthSquared() < 1f || _componentPathfinding.IsStuck)
                {
                    _importanceLevel = 0f;
                }

                if (_random.Float(0f, 1f) < 0.1f * _subsystemTime.GameTimeDelta)
                {
                    _componentCreature.ComponentCreatureSounds.PlayIdleSound(true);
                }

                if (_random.Float(0f, 1f) < 3f * _subsystemTime.GameTimeDelta)
                {
                    var v = CalculateDriveDirectionAndSpeed();
                    var speed = MathUtils.Saturate(0.2f * v.Length());
                    _componentPathfinding.SetDestination(
                        _componentCreature.ComponentBody.Position + 15f * Vector3.Normalize(v), speed, 5f, 0, false,
                        true,
                        false, null);
                }

                FadeDriveVector();
            },
            Actions.Empty
        );
        stateMachine.TransitionTo("Inactive");
    }

    public void FadeDriveVector()
    {
        var num = _driveVector.Length();
        if (num > 0.1f)
        {
            _driveVector -= _subsystemTime.GameTimeDelta * _driveVector / num;
        }
    }

    public Vector3 CalculateDriveDirectionAndSpeed()
    {
        var num = 1;
        var position = _componentCreature.ComponentBody.Position;
        var v = position;
        var driveVector = _driveVector;
        foreach (var creature in _subsystemCreatureSpawn.Creatures)
        {
            if (creature == _componentCreature || !(creature.ComponentHealth.Health > 0f))
            {
                continue;
            }

            var componentCattleDriveBehavior = creature.Entity.FindComponent<ComponentCattleDriveBehavior>();
            if (componentCattleDriveBehavior == null ||
                componentCattleDriveBehavior._componentHerdBehavior.HerdName !=
                _componentHerdBehavior.HerdName)
            {
                continue;
            }

            var position2 = creature.ComponentBody.Position;
            if (!(Vector3.DistanceSquared(position, position2) < 625f))
            {
                continue;
            }

            v += position2;
            driveVector += componentCattleDriveBehavior._driveVector;
            num++;
        }

        v /= num;
        driveVector /= num;
        var v2 = v - position;
        var s = MathUtils.Max(1.5f * v2.Length() - 3f, 0f);
        return 0.33f * _driveVector + 0.66f * driveVector + s * Vector3.Normalize(v2);
    }
}
