using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Network;
using Game.Network.Enums;
using Game.Network.Packages;

namespace Game.Components;

public class ComponentEatPickableBehavior : ComponentBehavior, IUpdateable
{
    public const float Range = 16f;

    private int _blockedCount;

    private float _blockedTime;

    private ComponentCreature _componentCreature = null!;

    private ComponentPathfinding _componentPathfinding = null!;

    private double _eatTime;

    private float[] _foodFactors = null!;

    private float _importanceLevel;

    private double _nextFindPickableTime;

    private double _nextPickablesUpdateTime;

    private Pickable? _pickable;

    private readonly Dictionary<Pickable, bool> _pickables = new();

    private readonly Random _random = new();

    private float _satiation;

    private SubsystemPickables _subsystemPickables = null!;

    private SubsystemTime _subsystemTime = null!;

    public float Satiation => _satiation;

    public override float ImportanceLevel => _importanceLevel;

    public bool IsFeed
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            if (CommonLib.WorkType == WorkType.Server)
            {
                CommonLib.Net.QueuePackage(new ComponentBehaviorPackage(this, field));
            }
        }
    }

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public void Update(float dt)
    {
        if (CommonLib.WorkType == WorkType.Client)
        {
            _componentCreature.ComponentCreatureModel.FeedOrder = IsFeed;
        }
        else
        {
            if (_satiation > 0f)
            {
                _satiation = MathUtils.Max(_satiation - 0.01f * _subsystemTime.GameTimeDelta, 0f);
            }

            stateMachine.Update();
        }
    }

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        _subsystemTime = Project.FindSubsystem<SubsystemTime>(true)!;
        _subsystemPickables = Project.FindSubsystem<SubsystemPickables>(true)!;
        _componentCreature = Entity.FindComponent<ComponentCreature>(true)!;
        _componentPathfinding = Entity.FindComponent<ComponentPathfinding>(true)!;
        _foodFactors = new float[EnumUtils.GetEnumValues(typeof(FoodType)).Max() + 1];
        foreach (var item in valuesDictionary.GetValue<ValuesDictionary>("FoodFactors"))
        {
            var foodType = (FoodType)Enum.Parse(typeof(FoodType), item.Key, false);
            _foodFactors[(int)foodType] = (float)item.Value;
        }

        _subsystemPickables.PickableAdded += delegate(Pickable pickable)
        {
            if (TryAddPickable(pickable) && _pickable == null)
            {
                _pickable = pickable;
            }
        };
        _subsystemPickables.PickableRemoved += delegate(Pickable pickable)
        {
            _pickables.Remove(pickable);
            if (_pickable == pickable)
            {
                _pickable = null;
            }
        };
        stateMachine.AddState(
            "Inactive",
            delegate
            {
                _importanceLevel = 0f;
                _pickable = null;
            },
            delegate
            {
                if (_satiation < 1f)
                {
                    if (_pickable == null)
                    {
                        if (_subsystemTime.GameTime > _nextFindPickableTime)
                        {
                            _nextFindPickableTime = _subsystemTime.GameTime + _random.Float(2f, 4f);
                            _pickable = FindPickable(_componentCreature.ComponentBody.Position);
                        }
                    }
                    else
                    {
                        _importanceLevel = _random.Float(5f, 10f);
                    }
                }

                if (!IsActive)
                {
                    return;
                }

                stateMachine.TransitionTo("Move");
                _blockedCount = 0;
            },
            Actions.Empty
        );
        stateMachine.AddState(
            "Move",
            delegate
            {
                if (_pickable == null)
                {
                    return;
                }

                var speed = _satiation == 0f ? _random.Float(0.5f, 0.7f) : 0.5f;
                var maxPathfindingPositions = _satiation == 0f ? 1000 : 500;
                var num2 = Vector3.Distance(_componentCreature.ComponentCreatureModel.EyePosition,
                    _componentCreature.ComponentBody.Position);
                _componentPathfinding.SetDestination(_pickable.Position, speed, 1f + num2, maxPathfindingPositions,
                    true, false, true, null);
                if (_random.Float(0f, 1f) < 0.66f)
                {
                    _componentCreature.ComponentCreatureSounds.PlayIdleSound(true);
                }
            },
            delegate
            {
                if (!IsActive)
                {
                    stateMachine.TransitionTo("Inactive");
                }
                else if (_pickable == null)
                {
                    _importanceLevel = 0f;
                }
                else if (_componentPathfinding.IsStuck)
                {
                    _importanceLevel = 0f;
                    _satiation += 0.75f;
                }
                else if (!_componentPathfinding.Destination.HasValue)
                {
                    stateMachine.TransitionTo("Eat");
                }
                else if (Vector3.DistanceSquared(_componentPathfinding.Destination.Value, _pickable.Position) > 0.0625f)
                {
                    stateMachine.TransitionTo("PickableMoved");
                }

                if (_random.Float(0f, 1f) < 0.1f * _subsystemTime.GameTimeDelta)
                {
                    _componentCreature.ComponentCreatureSounds.PlayIdleSound(true);
                }

                if (_pickable != null)
                {
                    _componentCreature.ComponentCreatureModel.LookAtOrder = _pickable.Position;
                }
                else
                {
                    _componentCreature.ComponentCreatureModel.LookRandomOrder = true;
                }
            },
            Actions.Empty
        );

        stateMachine.AddState(
            "PickableMoved",
            Actions.Empty,
            delegate
            {
                if (_pickable != null)
                {
                    _componentCreature.ComponentCreatureModel.LookAtOrder = _pickable.Position;
                }

                if (_subsystemTime.PeriodicGameTimeEvent(0.25, GetHashCode() % 100 * 0.01))
                {
                    stateMachine.TransitionTo("Move");
                }
            }, Actions.Empty
        );
        stateMachine.AddState(
            "Eat",
            delegate
            {
                IsFeed = true;
                _eatTime = _random.Float(4f, 5f);
                _blockedTime = 0f;
            },
            delegate
            {
                if (!IsActive)
                {
                    stateMachine.TransitionTo("Inactive");
                }

                if (_pickable == null)
                {
                    _importanceLevel = 0f;
                }

                if (_pickable != null)
                {
                    if (Vector3.DistanceSquared(
                            new Vector3(_componentCreature.ComponentCreatureModel.EyePosition.X,
                                _componentCreature.ComponentBody.Position.Y,
                                _componentCreature.ComponentCreatureModel.EyePosition.Z), _pickable.Position) <
                        0.640000045f)
                    {
                        _eatTime -= _subsystemTime.GameTimeDelta;
                        _blockedTime = 0f;
                        if (_eatTime <= 0.0)
                        {
                            _satiation += 1f;
                            _pickable.Count = MathUtils.Max(_pickable.Count - 1, 0);
                            if (_pickable.Count == 0)
                            {
                                _pickable.ToRemove = true;
                                _importanceLevel = 0f;
                            }
                            else if (_random.Float(0f, 1f) < 0.5f)
                            {
                                _importanceLevel = 0f;
                            }
                        }
                    }
                    else
                    {
                        var num = Vector3.Distance(_componentCreature.ComponentCreatureModel.EyePosition,
                            _componentCreature.ComponentBody.Position);
                        _componentPathfinding.SetDestination(_pickable.Position, 0.3f, 0.5f + num, 0, false, true,
                            false,
                            null);
                        _blockedTime += _subsystemTime.GameTimeDelta;
                    }

                    if (_blockedTime > 3f)
                    {
                        _blockedCount++;
                        if (_blockedCount >= 3)
                        {
                            _importanceLevel = 0f;
                            _satiation += 0.75f;
                        }
                        else
                        {
                            stateMachine.TransitionTo("Move");
                        }
                    }
                }

                _componentCreature.ComponentCreatureModel.FeedOrder = true;
                if (_random.Float(0f, 1f) < 0.1f * _subsystemTime.GameTimeDelta)
                {
                    _componentCreature.ComponentCreatureSounds.PlayIdleSound(true);
                }

                if (_random.Float(0f, 1f) < 1.5f * _subsystemTime.GameTimeDelta)
                {
                    _componentCreature.ComponentCreatureSounds.PlayFootstepSound(2f);
                }
            },
            delegate { IsFeed = false; }
        );
        stateMachine.TransitionTo("Inactive");
    }

    public float GetFoodFactor(FoodType foodType)
    {
        return _foodFactors[(int)foodType];
    }

    public Pickable? FindPickable(Vector3 position)
    {
        if (!(_subsystemTime.GameTime > _nextPickablesUpdateTime))
        {
            return _pickables.Keys
                .Select(key => new { key, num = Vector3.DistanceSquared(position, key.Position) })
                .Where(t => _random.Float(0f, 1f) > t.num / 512f)
                .Select(t => t.key)
                .FirstOrDefault();
        }

        _nextPickablesUpdateTime = _subsystemTime.GameTime + _random.Float(2f, 4f);
        _pickables.Clear();
        foreach (var pickable in _subsystemPickables.Pickables)
        {
            TryAddPickable(pickable);
        }

        if (_pickable != null && !_pickables.ContainsKey(_pickable))
        {
            _pickable = null;
        }

        return (from key in _pickables.Keys
            let num = Vector3.DistanceSquared(position, key.Position)
            where _random.Float(0f, 1f) > num / 512f
            select key).FirstOrDefault();
    }

    public bool TryAddPickable(Pickable pickable)
    {
        var block = BlocksManager.Blocks[Terrain.ExtractContents(pickable.Value)];
        if (!(_foodFactors[(int)block.FoodType] > 0f) ||
            !(Vector3.DistanceSquared(pickable.Position, _componentCreature.ComponentBody.Position) < 512f))
        {
            return false;
        }

        _pickables.Add(pickable, true);
        return true;
    }
}
