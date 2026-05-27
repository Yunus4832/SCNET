using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Network;
using Game.Network.Enums;
using Game.Network.Packages;

namespace Game.Components;

public class ComponentFishOutOfWaterBehavior : ComponentBehavior, IUpdateable
{
    private ComponentCreature _componentCreature = null!;

    private ComponentFishModel _componentFishModel = null!;

    private Vector2 _direction;

    private float _importanceLevel;

    private float _outOfWaterTime;

    private readonly Random _random = new();

    private SubsystemTerrain _subsystemTerrain = null!;

    private SubsystemTime _subsystemTime = null!;

    public override float ImportanceLevel => _importanceLevel;

    public bool IsBend
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
            if (IsBend)
            {
                _componentFishModel.BendOrder = 2f * (2f * MathUtils.Saturate(
                    SimplexNoise.OctavedNoise((float)MathUtils.Remainder(_subsystemTime.GameTime, 1000.0),
                        1.2f * _componentCreature.ComponentLocomotion.TurnSpeed, 1, 1f, 1f)) - 1f);
            }
            else
            {
                _componentFishModel.BendOrder = null;
            }
        }
        else
        {
            stateMachine.Update();
        }
    }

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        _subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true)!;
        _subsystemTime = Project.FindSubsystem<SubsystemTime>(true)!;
        _componentCreature = Entity.FindComponent<ComponentCreature>(true)!;
        _componentFishModel = Entity.FindComponent<ComponentFishModel>(true)!;
        stateMachine.AddState(
            "Inactive",
            Actions.Empty,
            delegate
            {
                if (IsOutOfWater())
                {
                    _outOfWaterTime += _subsystemTime.GameTimeDelta;
                }
                else
                {
                    _outOfWaterTime = 0f;
                }

                if (_outOfWaterTime > 3f)
                {
                    _importanceLevel = 1000f;
                }

                if (IsActive)
                {
                    stateMachine.TransitionTo("Jump");
                }
            },
            Actions.Empty
        );
        stateMachine.AddState(
            "Jump",
            delegate { IsBend = true; },
            delegate
            {
                _componentFishModel.BendOrder = 2f * (2f * MathUtils.Saturate(SimplexNoise.OctavedNoise(
                    (float)MathUtils.Remainder(_subsystemTime.GameTime, 1000.0),
                    1.2f * _componentCreature.ComponentLocomotion.TurnSpeed, 1, 1f, 1f)) - 1f);
                if (!IsActive)
                {
                    stateMachine.TransitionTo("Inactive");
                }

                if (!IsOutOfWater())
                {
                    _importanceLevel = 0f;
                }

                if (_random.Float(0f, 1f) < 2.5f * _subsystemTime.GameTimeDelta)
                {
                    _componentCreature.ComponentLocomotion.JumpOrder = _random.Float(0.33f, 1f);
                    _direction = new Vector2(MathUtils.Sign(_componentFishModel.BendOrder.Value), 0f);
                }

                if (_componentCreature.ComponentBody.StandingOnValue.HasValue)
                {
                    return;
                }

                _componentCreature.ComponentLocomotion.TurnOrder =
                    new Vector2(0f - _componentFishModel.BendOrder.Value, 0f);
                _componentCreature.ComponentLocomotion.WalkOrder = _direction;
            },
            delegate { IsBend = false; }
        );
        stateMachine.TransitionTo("Inactive");
    }

    public bool IsOutOfWater()
    {
        return _componentCreature.ComponentBody.ImmersionFactor < 0.33f;
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
                _componentCreature.ComponentBody.Position, vector2,
                false,
                false,
                delegate(int value, float _)
                {
                    var num = Terrain.ExtractContents(value);
                    return BlocksManager.Blocks[num] is not WaterBlock;
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
