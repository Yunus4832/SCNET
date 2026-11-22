using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Components.Behavior;

public class ComponentChaseBehavior : ComponentBehavior, IUpdateable
{
    public UpdateExecutionSide UpdateSide => UpdateExecutionSide.Authority;

    private CreatureCategory _autoChaseMask;

    private float _autoChaseSuppressionTime;

    private float _chaseNonCharacterProbability;

    private float _chaseOnTouchProbability;

    private float _chaseTime;

    private float _chaseWhenAttackedProbability;

    private readonly DynamicArray<ComponentBody> _componentBodies = [];

    private ComponentCreature _componentCreature = null!;

    private ComponentCreaturePose _creaturePose = null!;

    private ComponentRandomFeedBehavior? _componentFeedBehavior;

    private ComponentMiner _componentMiner = null!;

    private ComponentPathfinding _componentPathfinding = null!;

    private float _dayChaseRange;

    private float _dayChaseTime;

    private float _dt;

    private float _importanceLevel;

    private bool _isAttack;

    private bool _isPersistent;

    private double _nextUpdateTime;

    private float _nightChaseRange;

    private float _nightChaseTime;

    private readonly Random _random = new();

    private float _range;

    private SubsystemBodies _subsystemBodies = null!;

    private SubsystemGameInfo _subsystemGameInfo = null!;

    private SubsystemNoise _subsystemNoise = null!;

    private SubsystemCharacters _subsystemCharacters = null!;

    private SubsystemSky _subsystemSky = null!;

    private SubsystemTime _subsystemTime = null!;

    private ComponentCreature? _target;

    private float _targetInRangeTime;

    private float _targetUnsuitableTime;

    public ComponentCreature? Target => _target;

    public override float ImportanceLevel => _importanceLevel;

    public bool IsAttack
    {
        get => _isAttack;
        set
        {
            if (_isAttack == value)
            {
                return;
            }

            _isAttack = value;
            if (WorldRuntimeContext.For(Project).IsAuthority)
            {
                NetworkSender.SendToObservers(Entity, BehaviorPackageBuilder.Capture(this, value));
            }
        }
    }

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public void Update(float dt)
    {
        // 客户端逻辑
        if (!WorldRuntimeContext.For(Project).IsAuthority)
        {
            _creaturePose.AttackOrder = IsAttack;
        }
        else
        {
            _autoChaseSuppressionTime -= dt;
            if (IsActive && _target != null)
            {
                _chaseTime -= dt;
                _componentCreature.ComponentCreaturePose.LookAtOrder = _target.ComponentCreaturePose.EyePosition;
                if (IsTargetInAttackRange(_target.ComponentBody))
                {
                    _creaturePose.AttackOrder = true;
                    IsAttack = true;
                }
                else
                {
                    IsAttack = false;
                }

                if (_creaturePose.IsAttackHitMoment)
                {
                    var hitBody = GetHitBody(_target.ComponentBody, out var hitPoint);
                    if (hitBody != null)
                    {
                        var x = _isPersistent ? _random.Float(8f, 10f) : 2f;
                        _chaseTime = MathUtils.Max(_chaseTime, x);
                        _componentMiner.Hit(hitBody, hitPoint, _componentCreature.ComponentBody.Matrix.Forward);
                        _componentCreature.ComponentCreatureSounds.PlayAttackSound();
                    }
                }
            }

            if (_subsystemTime.GameTime >= _nextUpdateTime)
            {
                _dt = _random.Float(0.25f, 0.35f) +
                      MathUtils.Min((float)(_subsystemTime.GameTime - _nextUpdateTime), 0.1f);
                _nextUpdateTime = _subsystemTime.GameTime + _dt;
                stateMachine.Update();
            }
        }
    }

    public void Attack(ComponentCreature? componentCreature, float maxRange, float maxChaseTime, bool isPersistent)
    {
        if (componentCreature != null)
        {
            var context = new CreatureTargetScoringContext(
                _componentCreature,
                componentCreature,
                CreatureTargetingKind.Chase,
                1f
            );
            WorldRuleHooks.For(Project).Invoke(context);
            if (!WorldRuntimeContext.IsProjectActive(context.Project)) { return; }

            if (context.Cancel || context.Score <= 0f)
            {
                return;
            }
        }

        _target = componentCreature;
        _nextUpdateTime = 0.0;
        _range = maxRange;
        _chaseTime = maxChaseTime;
        _isPersistent = isPersistent;
        _importanceLevel = 200f;
    }

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        _subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true)!;
        _subsystemCharacters = Project.FindSubsystem<SubsystemCharacters>(true)!;
        _subsystemSky = Project.FindSubsystem<SubsystemSky>(true)!;
        _subsystemBodies = Project.FindSubsystem<SubsystemBodies>(true)!;
        _subsystemTime = Project.FindSubsystem<SubsystemTime>(true)!;
        _subsystemNoise = Project.FindSubsystem<SubsystemNoise>(true)!;
        _componentCreature = Entity.FindComponent<ComponentCreature>(true)!;
        _componentPathfinding = Entity.FindComponent<ComponentPathfinding>(true)!;
        _componentMiner = Entity.FindComponent<ComponentMiner>(true)!;
        _componentFeedBehavior = Entity.FindComponent<ComponentRandomFeedBehavior>();
        _creaturePose = Entity.FindComponent<ComponentCreaturePose>(true)!;
        _dayChaseRange = valuesDictionary.GetValue<float>("DayChaseRange");
        _nightChaseRange = valuesDictionary.GetValue<float>("NightChaseRange");
        _dayChaseTime = valuesDictionary.GetValue<float>("DayChaseTime");
        _nightChaseTime = valuesDictionary.GetValue<float>("NightChaseTime");
        _autoChaseMask = valuesDictionary.GetValue<CreatureCategory>("AutoChaseMask");
        _chaseNonCharacterProbability = valuesDictionary.GetValue<float>("ChaseNonCharacterProbability");
        _chaseWhenAttackedProbability = valuesDictionary.GetValue<float>("ChaseWhenAttackedProbability");
        _chaseOnTouchProbability = valuesDictionary.GetValue<float>("ChaseOnTouchProbability");
        _componentCreature.ComponentHealth.Attacked += delegate(ComponentCreature attacker)
        {
            if (!(_random.Float(0f, 1f) < _chaseWhenAttackedProbability))
            {
                return;
            }

            if (_chaseWhenAttackedProbability >= 1f)
            {
                Attack(attacker, 30f, 60f, true);
            }
            else
            {
                Attack(attacker, 7f, 7f, false);
            }
        };
        _componentCreature.ComponentBody.CollidedWithBody += delegate(ComponentBody body)
        {
            if (_target == null &&
                _autoChaseSuppressionTime <= 0f &&
                _random.Float(0f, 1f) < _chaseOnTouchProbability)
            {
                var componentCreature2 = body.Entity.FindComponent<ComponentCreature>();
                if (componentCreature2 != null)
                {
                    var isCharacter = _subsystemCharacters.IsCharacter(body.Entity);
                    var matchesChaseMask = (componentCreature2.Category & _autoChaseMask) != 0;
                    if ((isCharacter && _subsystemGameInfo.WorldSettings.GameMode > GameMode.Harmless) ||
                        (!isCharacter && matchesChaseMask))
                    {
                        Attack(componentCreature2, 7f, 7f, false);
                    }
                }
            }

            if (_target != null &&
                body == _target.ComponentBody &&
                body.StandingOnBody == _componentCreature.ComponentBody)
            {
                _componentCreature.ComponentLocomotion.JumpOrder = 1f;
            }
        };
        stateMachine.AddState(
            "LookingForTarget",
            delegate
            {
                _importanceLevel = 0f;
                _target = null;
            },
            delegate
            {
                if (IsActive)
                {
                    stateMachine.TransitionTo("Chasing");
                }
                else if (_autoChaseSuppressionTime <= 0f &&
                         (_target == null || ScoreTarget(_target) <= 0f) &&
                         _componentCreature.ComponentHealth.Health > 0.4f)
                {
                    _range = _subsystemSky.SkyLightIntensity < 0.2f ? _nightChaseRange : _dayChaseRange;
                    var componentCreature = FindTarget();
                    if (componentCreature != null)
                    {
                        _targetInRangeTime += _dt;
                    }
                    else
                    {
                        _targetInRangeTime = 0f;
                    }

                    if (!(_targetInRangeTime > 3f))
                    {
                        return;
                    }

                    var isDaylight = _subsystemSky.SkyLightIntensity >= 0.1f;
                    var maxRange = isDaylight ? _dayChaseRange + 6f : _nightChaseRange + 6f;
                    var maxChaseTime = isDaylight
                        ? _dayChaseTime * _random.Float(0.75f, 1f)
                        : _nightChaseTime * _random.Float(0.75f, 1f);
                    Attack(componentCreature, maxRange, maxChaseTime, !isDaylight);
                }
            },
            Actions.Empty
        );
        stateMachine.AddState(
            "RandomMoving",
            delegate
            {
                _componentPathfinding.SetDestination(
                    _componentCreature.ComponentBody.Position +
                    new Vector3(
                        6f * _random.Float(-1f, 1f),
                        0f,
                        6f * _random.Float(-1f, 1f)
                    ),
                    1f,
                    1f,
                    0,
                    false,
                    true,
                    false,
                    null
                );
            },
            delegate
            {
                if (_componentPathfinding.IsStuck || !_componentPathfinding.Destination.HasValue)
                {
                    stateMachine.TransitionTo("Chasing");
                }

                if (!IsActive)
                {
                    stateMachine.TransitionTo("LookingForTarget");
                }
            },
            delegate { _componentPathfinding.Stop(); }
        );
        stateMachine.AddState(
            "Chasing",
            delegate
            {
                _subsystemNoise.MakeNoise(_componentCreature.ComponentBody, 0.25f, 6f);
                _componentCreature.ComponentCreatureSounds.PlayIdleSound(false);
                _nextUpdateTime = 0.0;
            },
            delegate
            {
                if (!IsActive)
                {
                    stateMachine.TransitionTo("LookingForTarget");
                }
                else if (_chaseTime <= 0f)
                {
                    _autoChaseSuppressionTime = _random.Float(10f, 60f);
                    _importanceLevel = 0f;
                }
                else if (_target == null)
                {
                    _importanceLevel = 0f;
                }
                else if (_target.ComponentHealth.Health <= 0f)
                {
                    if (_componentFeedBehavior != null)
                    {
                        _subsystemTime.QueueGameTimeDelayedExecution(
                            _subsystemTime.GameTime + _random.Float(1f, 3f),
                            delegate
                            {
                                if (_target != null)
                                {
                                    _componentFeedBehavior.Feed(_target.ComponentBody.Position);
                                }
                            }
                        );
                    }

                    _importanceLevel = 0f;
                }
                else if (!_isPersistent && _componentPathfinding.IsStuck)
                {
                    _importanceLevel = 0f;
                }
                else if (_isPersistent && _componentPathfinding.IsStuck)
                {
                    stateMachine.TransitionTo("RandomMoving");
                }
                else
                {
                    if (ScoreTarget(_target) <= 0f)
                    {
                        _targetUnsuitableTime += _dt;
                    }
                    else
                    {
                        _targetUnsuitableTime = 0f;
                    }

                    if (_targetUnsuitableTime > 3f)
                    {
                        _importanceLevel = 0f;
                    }
                    else
                    {
                        var maxPathfindingPositions = 0;
                        if (_isPersistent)
                        {
                            maxPathfindingPositions = _subsystemTime.FixedTimeStep.HasValue ? 1500 : 500;
                        }

                        var boundingBox = _componentCreature.ComponentBody.BoundingBox;
                        var boundingBox2 = _target.ComponentBody.BoundingBox;
                        var v = 0.5f * (boundingBox.Min + boundingBox.Max);
                        var vector = 0.5f * (boundingBox2.Min + boundingBox2.Max);
                        var targetDistance = Vector3.Distance(v, vector);
                        var leadFactor = targetDistance < 4f ? 0.2f : 0f;
                        _componentPathfinding.SetDestination(
                            vector + leadFactor * targetDistance * _target.ComponentBody.Velocity,
                            1f,
                            1.5f,
                            maxPathfindingPositions,
                            true,
                            false,
                            true,
                            _target.ComponentBody
                        );
                        if (_random.Float(0f, 1f) < 0.33f * _dt)
                        {
                            _componentCreature.ComponentCreatureSounds.PlayAttackSound();
                        }
                    }
                }
            },
            Actions.Empty
        );
        stateMachine.TransitionTo("LookingForTarget");
    }

    public ComponentCreature? FindTarget()
    {
        var position = _componentCreature.ComponentBody.Position;
        ComponentCreature? result = null;
        var bestScore = 0f;
        _componentBodies.Clear();
        _subsystemBodies.FindBodiesAroundPoint(new Vector2(position.X, position.Z), _range, _componentBodies);
        for (var i = 0; i < _componentBodies.Count; i++)
        {
            var componentCreature = _componentBodies.Array[i].Entity.FindComponent<ComponentCreature>();
            if (componentCreature == null)
            {
                continue;
            }

            var candidateScore = ScoreTarget(componentCreature);
            if (!(candidateScore > bestScore))
            {
                continue;
            }

            bestScore = candidateScore;
            result = componentCreature;
        }

        return result;
    }

    public float ScoreTarget(ComponentCreature componentCreature)
    {
        var isCharacter = componentCreature.Entity.FindComponent<ComponentCharacter>() != null;
        var canChaseOnLand = _componentCreature.Category != CreatureCategory.WaterPredator &&
                             _componentCreature.Category != CreatureCategory.WaterOther;
        var canChaseCharacter =
            componentCreature == Target || _subsystemGameInfo.WorldSettings.GameMode > GameMode.Harmless;
        var matchesChaseMask = (componentCreature.Category & _autoChaseMask) != 0;
        var canChaseNonCharacter = componentCreature == Target ||
                                   (matchesChaseMask &&
                                    MathUtils.Remainder(
                                        0.004999999888241291 * _subsystemTime.GameTime +
                                        GetHashCode() % 1000 / 1000f +
                                        componentCreature.GetHashCode() % 1000 / 1000f,
                                        1.0
                                    ) <
                                    _chaseNonCharacterProbability);
        if (componentCreature != _componentCreature &&
            ((!isCharacter && canChaseNonCharacter) || (isCharacter && canChaseCharacter)) &&
            componentCreature.Entity.IsAddedToProject &&
            componentCreature.ComponentHealth.Health > 0f &&
            (canChaseOnLand || IsTargetInWater(componentCreature.ComponentBody)))
        {
            var distance = Vector3.Distance(
                _componentCreature.ComponentBody.Position,
                componentCreature.ComponentBody.Position
            );
            if (distance < _range)
            {
                var score = _range - distance;
                if (score <= 0f)
                {
                    return score;
                }

                var context = new CreatureTargetScoringContext(
                    _componentCreature,
                    componentCreature,
                    CreatureTargetingKind.Chase,
                    score
                );
                WorldRuleHooks.For(Project).Invoke(context);
                if (!WorldRuntimeContext.IsProjectActive(context.Project)) { return 0f; }

                return context.Cancel ? 0f : context.Score;
            }
        }

        return 0f;
    }

    public bool IsTargetInWater(ComponentBody target)
    {
        if (target.ImmersionDepth > 0f)
        {
            return true;
        }

        if (target.ParentBody != null && IsTargetInWater(target.ParentBody))
        {
            return true;
        }

        return target.StandingOnBody != null &&
               target.StandingOnBody.Position.Y < target.Position.Y &&
               IsTargetInWater(target.StandingOnBody);
    }

    public bool IsTargetInAttackRange(ComponentBody target)
    {
        if (IsBodyInAttackRange(target))
        {
            return true;
        }

        var boundingBox = _componentCreature.ComponentBody.BoundingBox;
        var boundingBox2 = target.BoundingBox;
        var v = 0.5f * (boundingBox.Min + boundingBox.Max);
        var v2 = 0.5f * (boundingBox2.Min + boundingBox2.Max) - v;
        var distance = v2.Length();
        var v3 = v2 / distance;
        var combinedHalfWidth =
            0.5f * (boundingBox.Max.X - boundingBox.Min.X + boundingBox2.Max.X - boundingBox2.Min.X);
        var combinedHalfHeight =
            0.5f * (boundingBox.Max.Y - boundingBox.Min.Y + boundingBox2.Max.Y - boundingBox2.Min.Y);
        if (MathUtils.Abs(v2.Y) < combinedHalfHeight * 0.99f)
        {
            if (distance < combinedHalfWidth + 0.99f &&
                Vector3.Dot(v3, _componentCreature.ComponentBody.Matrix.Forward) > 0.25f)
            {
                return true;
            }
        }
        else if (distance < combinedHalfHeight + 0.3f && MathUtils.Abs(Vector3.Dot(v3, Vector3.UnitY)) > 0.8f)
        {
            return true;
        }

        if (target.ParentBody != null && IsTargetInAttackRange(target.ParentBody))
        {
            return true;
        }

        return target.StandingOnBody != null &&
               target.StandingOnBody.Position.Y < target.Position.Y &&
               IsTargetInAttackRange(target.StandingOnBody);
    }

    public bool IsBodyInAttackRange(ComponentBody target)
    {
        var boundingBox = _componentCreature.ComponentBody.BoundingBox;
        var boundingBox2 = target.BoundingBox;
        var v = 0.5f * (boundingBox.Min + boundingBox.Max);
        var v2 = 0.5f * (boundingBox2.Min + boundingBox2.Max) - v;
        var distance = v2.Length();
        var v3 = v2 / distance;
        var combinedHalfWidth =
            0.5f * (boundingBox.Max.X - boundingBox.Min.X + boundingBox2.Max.X - boundingBox2.Min.X);
        var combinedHalfHeight =
            0.5f * (boundingBox.Max.Y - boundingBox.Min.Y + boundingBox2.Max.Y - boundingBox2.Min.Y);
        if (MathUtils.Abs(v2.Y) < combinedHalfHeight * 0.99f)
        {
            if (distance < combinedHalfWidth + 0.99f &&
                Vector3.Dot(v3, _componentCreature.ComponentBody.Matrix.Forward) > 0.25f)
            {
                return true;
            }
        }
        else if (distance < combinedHalfHeight + 0.3f && MathUtils.Abs(Vector3.Dot(v3, Vector3.UnitY)) > 0.8f)
        {
            return true;
        }

        return false;
    }

    public ComponentBody? GetHitBody(ComponentBody target, out Vector3 hitPoint)
    {
        var vector = _componentCreature.ComponentBody.BoundingBox.Center();
        var v = target.BoundingBox.Center();
        var ray = new Ray3(vector, Vector3.Normalize(v - vector));
        var bodyRaycastResult = _componentMiner.Raycast<BodyRaycastResult>(ray, RaycastMode.Interaction);
        if (bodyRaycastResult is { Distance: < 1.75f } &&
            (bodyRaycastResult.Value.ComponentBody == target ||
             bodyRaycastResult.Value.ComponentBody.IsChildOfBody(target) ||
             target.IsChildOfBody(bodyRaycastResult.Value.ComponentBody) ||
             target.StandingOnBody == bodyRaycastResult.Value.ComponentBody))
        {
            hitPoint = bodyRaycastResult.Value.HitPoint();
            return bodyRaycastResult.Value.ComponentBody;
        }

        hitPoint = default;
        return null;
    }
}
