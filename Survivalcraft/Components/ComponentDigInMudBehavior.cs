using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;
using Game.NetWork;
using Game.NetWork.Packages;

namespace Game.Components;

public class ComponentDigInMudBehavior : ComponentBehavior, IUpdateable
{
    private ComponentBody? _collidedWithBody;

    private ComponentCreature _componentCreature = null!;

    private ComponentFishModel _componentFishModel = null!;

    private ComponentMiner _componentMiner = null!;

    private ComponentPathfinding _componentPathfinding = null!;

    private ComponentSwimAwayBehavior _componentSwimAwayBehavior = null!;

    private int _digInBlockIndex;

    private double _digInTime;

    private double _digOutTime = double.NegativeInfinity;

    private float _importanceLevel;

    private bool _isDigIn;

    private float _maxDigInDepth;

    private readonly Random _random = new();

    private double _sinkTime;

    private SubsystemTerrain _subsystemTerrain = null!;

    private SubsystemTime _subsystemTime = null!;

    public override float ImportanceLevel => _importanceLevel;

    public bool IsDigIn
    {
        get => _isDigIn;
        set
        {
            if (_isDigIn == value)
            {
                return;
            }

            _isDigIn = value;
            if (CommonLib.WorkType == WorkType.Server)
            {
                CommonLib.Net.QueuePackage(new ComponentBehaviorPackage(this, _isDigIn));
            }
        }
    }

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public void Update(float dt)
    {
        if (CommonLib.WorkType == WorkType.Client)
        {
            _componentFishModel.DigInOrder = IsDigIn ? _maxDigInDepth : 0f;
        }
        else
        {
            stateMachine.Update();
            _collidedWithBody = null;
        }
    }

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        _subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true)!;
        _subsystemTime = Project.FindSubsystem<SubsystemTime>(true)!;
        _componentCreature = Entity.FindComponent<ComponentCreature>(true)!;
        _componentPathfinding = Entity.FindComponent<ComponentPathfinding>(true)!;
        _componentMiner = Entity.FindComponent<ComponentMiner>(true)!;
        _componentFishModel = Entity.FindComponent<ComponentFishModel>(true)!;
        _componentSwimAwayBehavior = Entity.FindComponent<ComponentSwimAwayBehavior>(true)!;
        var digInBlockName = valuesDictionary.GetValue<string>("DigInBlockName");
        _digInBlockIndex = !string.IsNullOrEmpty(digInBlockName)
            ? BlocksManager.Blocks.First(b => b.GetType().Name == digInBlockName).BlockIndex
            : 0;
        _maxDigInDepth = valuesDictionary.GetValue<float>("MaxDigInDepth");
        _componentCreature.ComponentBody.CollidedWithBody += delegate(ComponentBody b) { _collidedWithBody = b; };
        stateMachine.AddState(
            "Inactive",
            delegate { _importanceLevel = 0f; },
            delegate
            {
                if (_random.Float(0f, 1f) < 0.5f * _subsystemTime.GameTimeDelta &&
                    _subsystemTime.GameTime > _digOutTime + 15.0 && _digInBlockIndex != 0)
                {
                    var x = Terrain.ToCell(_componentCreature.ComponentBody.Position.X);
                    var y = Terrain.ToCell(_componentCreature.ComponentBody.Position.Y - 0.9f);
                    var z = Terrain.ToCell(_componentCreature.ComponentBody.Position.Z);
                    if (_subsystemTerrain.Terrain.GetCellContents(x, y, z) == _digInBlockIndex)
                    {
                        _importanceLevel = _random.Float(1f, 3f);
                    }
                }

                if (IsActive)
                {
                    stateMachine.TransitionTo("Sink");
                }
            },
            Actions.Empty
        );
        stateMachine.AddState(
            "Sink",
            delegate
            {
                _importanceLevel = 10f;
                _sinkTime = _subsystemTime.GameTime;
                _componentPathfinding.Stop();
            },
            delegate
            {
                if (_random.Float(0f, 1f) < 2f * _subsystemTime.GameTimeDelta &&
                    _componentCreature.ComponentBody.StandingOnValue == _digInBlockIndex &&
                    _componentCreature.ComponentBody.Velocity.LengthSquared() < 1f)
                {
                    stateMachine.TransitionTo("DigIn");
                }

                if (!IsActive || _subsystemTime.GameTime > _sinkTime + 6.0)
                {
                    stateMachine.TransitionTo("Inactive");
                }
            },
            Actions.Empty
        );
        stateMachine.AddState(
            "DigIn",
            delegate
            {
                _digInTime = _subsystemTime.GameTime;
                _digOutTime = _digInTime + _random.Float(30f, 60f);
                IsDigIn = true;
            },
            delegate
            {
                _componentFishModel.DigInOrder = _maxDigInDepth;
                if (_collidedWithBody != null)
                {
                    if (_subsystemTime.GameTime - _digInTime > 2.0 && _collidedWithBody.Density < 0.95f)
                    {
                        _componentMiner.Hit(_collidedWithBody, _collidedWithBody.Position,
                            Vector3.Normalize(_collidedWithBody.Position - _componentCreature.ComponentBody.Position));
                    }

                    _componentSwimAwayBehavior.SwimAwayFrom(_collidedWithBody);
                    stateMachine.TransitionTo("Inactive");
                }

                if (!IsActive || _subsystemTime.GameTime >= _digOutTime ||
                    _componentCreature.ComponentBody.StandingOnValue != _digInBlockIndex ||
                    _componentCreature.ComponentBody.Velocity.LengthSquared() > 1f)
                {
                    stateMachine.TransitionTo("Inactive");
                }
            },
            delegate { IsDigIn = false; }
        );
        stateMachine.TransitionTo("Inactive");
    }

    public Vector3? FindDestination()
    {
        for (var i = 0; i < 8; i++)
        {
            var vector = _random.Vector2(1f, 1f);
            var y = 0.2f * _random.Float(-0.8f, 1f);
            var v = Vector3.Normalize(new Vector3(vector.X, y, vector.Y));
            var vector2 = _componentCreature.ComponentBody.Position + _random.Float(8f, 16f) * v;
            var terrainRaycastResult = _subsystemTerrain.Raycast(
                _componentCreature.ComponentBody.Position,
                vector2,
                false,
                false,
                delegate(int value, float _)
                {
                    var num = Terrain.ExtractContents(value);
                    return !(BlocksManager.Blocks[num] is WaterBlock);
                }
            );
            if (!terrainRaycastResult.HasValue)
            {
                return vector2;
            }

            if (terrainRaycastResult.Value.Distance > 4f)
            {
                return _componentCreature.ComponentBody.Position + v * terrainRaycastResult.Value.Distance;
            }
        }

        return null;
    }
}
