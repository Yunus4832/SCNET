using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Components;

public class ComponentPilot : Component, IUpdateable
{
    public static bool DrawPilotDestination;

    private double? _aboveBelowTime;

    private ComponentCreature _componentCreature = null!;

    private Vector3? _flyOrder;

    private float _jumpOrder;

    private Vector3? _lastStuckCheckPosition;

    private double _lastStuckCheckTime;

    private readonly DynamicArray<ComponentBody> _nearbyBodies = [];

    private double _nextBodiesUpdateTime;

    private double _nextUpdateTime;

    private readonly Random _random = new();

    private int _stuckCount;

    private SubsystemBodies _subsystemBodies = null!;

    private SubsystemTerrain _subsystemTerrain = null!;

    private SubsystemTime _subsystemTime = null!;

    private Vector3? _swimOrder;

    private Vector2 _turnOrder;

    private Vector2? _walkOrder;

    public Vector3? Destination { get; set; }

    public float Speed { get; set; }

    public float Range { get; set; }

    public bool IgnoreHeightDifference { get; set; }

    public bool RaycastDestination { get; set; }

    public bool TakeRisks { get; set; }

    public ComponentBody? DoNotAvoidBody { get; set; }

    public bool IsStuck { get; set; }

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public void Update(float dt)
    {
        if (_subsystemTime.GameTime >= _nextUpdateTime)
        {
            _nextUpdateTime = _subsystemTime.GameTime + _random.Float(0.09f, 0.11f);
            _walkOrder = null;
            _flyOrder = null;
            _swimOrder = null;
            _turnOrder = Vector2.Zero;
            _jumpOrder = 0f;
            if (Destination.HasValue)
            {
                var position = _componentCreature.ComponentBody.Position;
                var forward = _componentCreature.ComponentBody.Matrix.Forward;
                var v = AvoidNearestBody(position, Destination.Value);
                var vector = v - position;
                var num = vector.LengthSquared();
                var vector2 = new Vector2(v.X, v.Z) - new Vector2(position.X, position.Z);
                var num2 = vector2.LengthSquared();
                var x = Vector2.Angle(forward.XZ, vector.XZ);
                var num3 =
                    (_componentCreature.ComponentBody.CollisionVelocityChange * new Vector3(1f, 0f, 1f))
                    .LengthSquared() > 0f && _componentCreature.ComponentBody.StandingOnValue.HasValue
                        ? 0.15f
                        : 0.4f;
                if (_subsystemTime.GameTime >= _lastStuckCheckTime + num3 || !_lastStuckCheckPosition.HasValue)
                {
                    _lastStuckCheckTime = _subsystemTime.GameTime;
                    if (MathUtils.Abs(x) > MathUtils.DegToRad(20f) || !_lastStuckCheckPosition.HasValue ||
                        Vector3.Dot(position - _lastStuckCheckPosition.Value, Vector3.Normalize(vector)) > 0.2f)
                    {
                        _lastStuckCheckPosition = position;
                        _stuckCount = 0;
                    }
                    else
                    {
                        _stuckCount++;
                    }

                    IsStuck = _stuckCount >= 4;
                }

                if (_componentCreature.ComponentLocomotion.FlySpeed > 0f && (num > 9f || vector.Y > 0.5f ||
                                                                             vector.Y < -1.5f ||
                                                                             (!_componentCreature.ComponentBody
                                                                                  .StandingOnValue.HasValue &&
                                                                              _componentCreature.ComponentBody
                                                                                  .ImmersionFactor == 0f)) &&
                    _componentCreature.ComponentBody.ImmersionFactor < 1f)
                {
                    var y = MathUtils.Min(0.08f * vector2.LengthSquared(), 12f);
                    var v2 = v + new Vector3(0f, y, 0f);
                    var value2 = Speed * Vector3.Normalize(v2 - position);
                    value2.Y = MathUtils.Max(value2.Y, -0.5f);
                    _flyOrder = value2;
                    _turnOrder = new Vector2(MathUtils.Clamp(x, -1f, 1f), 0f);
                }
                else if (_componentCreature.ComponentLocomotion.SwimSpeed > 0f &&
                         _componentCreature.ComponentBody.ImmersionFactor > 0.5f)
                {
                    var value3 = Speed * Vector3.Normalize(v - position);
                    value3.Y = MathUtils.Clamp(value3.Y, -0.5f, 0.5f);
                    _swimOrder = value3;
                    _turnOrder = new Vector2(MathUtils.Clamp(x, -1f, 1f), 0f);
                }
                else if (_componentCreature.ComponentLocomotion.WalkSpeed > 0f)
                {
                    if (IsTerrainSafeToGo(position, vector))
                    {
                        _turnOrder = new Vector2(MathUtils.Clamp(x, -1f, 1f), 0f);
                        if (num2 > 1f)
                        {
                            _walkOrder = new Vector2(0f,
                                MathUtils.Lerp(Speed, 0f, MathUtils.Saturate((MathUtils.Abs(x) - 0.33f) / 0.66f)));
                            if (Speed >= 1f && _componentCreature.ComponentLocomotion.InAirWalkFactor >= 1f &&
                                num > 1f && _random.Float(0f, 1f) < 0.05f)
                            {
                                _jumpOrder = 1f;
                            }
                        }
                        else
                        {
                            var x2 = Speed * MathUtils.Min(1f * MathUtils.Sqrt(num2), 1f);
                            _walkOrder = new Vector2(0f,
                                MathUtils.Lerp(x2, 0f, MathUtils.Saturate(2f * MathUtils.Abs(x))));
                        }
                    }
                    else
                    {
                        IsStuck = true;
                    }

                    _componentCreature.ComponentBody.IsSmoothRiseEnabled = num2 >= 1f || vector.Y >= -0.1f;
                    if (num2 < 1f && vector.Y is < -0.5f or > 1f)
                    {
                        if (vector.Y > 0f && _random.Float(0f, 1f) < 0.05f)
                        {
                            _jumpOrder = 1f;
                        }

                        if (!_aboveBelowTime.HasValue)
                        {
                            _aboveBelowTime = _subsystemTime.GameTime;
                        }
                        else if (_subsystemTime.GameTime - _aboveBelowTime.Value > 2.0 &&
                                 _componentCreature.ComponentBody.StandingOnValue.HasValue)
                        {
                            IsStuck = true;
                        }
                    }
                    else
                    {
                        _aboveBelowTime = null;
                    }
                }

                if (!IgnoreHeightDifference ? num <= Range * Range : num2 <= Range * Range)
                {
                    if (RaycastDestination)
                    {
                        if (!_subsystemTerrain.Raycast(position + new Vector3(0f, 0.5f, 0f),
                                    v + new Vector3(0f, 0.5f, 0f), false, true,
                                    (value, _) =>
                                        BlocksManager.Blocks[Terrain.ExtractContents(value)].Collidable)
                                .HasValue)
                        {
                            Destination = null;
                        }
                    }
                    else
                    {
                        Destination = null;
                    }
                }
            }

            if (!Destination.HasValue && _componentCreature.ComponentLocomotion.FlySpeed > 0f &&
                !_componentCreature.ComponentBody.StandingOnValue.HasValue &&
                _componentCreature.ComponentBody.ImmersionFactor == 0f)
            {
                _turnOrder = Vector2.Zero;
                _walkOrder = null;
                _swimOrder = null;
                _flyOrder = new Vector3(0f, -0.5f, 0f);
            }
        }

        _componentCreature.ComponentLocomotion.WalkOrder =
            CombineNullables(_componentCreature.ComponentLocomotion.WalkOrder, _walkOrder);
        _componentCreature.ComponentLocomotion.SwimOrder =
            CombineNullables(_componentCreature.ComponentLocomotion.SwimOrder, _swimOrder);
        _componentCreature.ComponentLocomotion.TurnOrder += _turnOrder;
        _componentCreature.ComponentLocomotion.FlyOrder =
            CombineNullables(_componentCreature.ComponentLocomotion.FlyOrder, _flyOrder);
        _componentCreature.ComponentLocomotion.JumpOrder =
            MathUtils.Max(_jumpOrder, _componentCreature.ComponentLocomotion.JumpOrder);
        _jumpOrder = 0f;
    }

    public void SetDestination(
        Vector3? destination,
        float speed,
        float range,
        bool ignoreHeightDifference,
        bool raycastDestination,
        bool takeRisks,
        ComponentBody? doNotAvoidBody
    )
    {
        var flag = true;
        if (Destination.HasValue && destination.HasValue)
        {
            var v = Vector3.Normalize(Destination.Value - _componentCreature.ComponentBody.Position);
            if (Vector3.Dot(Vector3.Normalize(destination.Value - _componentCreature.ComponentBody.Position), v) >
                0.5f)
            {
                flag = false;
            }
        }

        if (flag)
        {
            IsStuck = false;
            _lastStuckCheckPosition = null;
            _aboveBelowTime = null;
        }

        Destination = destination;
        Speed = speed;
        Range = range;
        IgnoreHeightDifference = ignoreHeightDifference;
        RaycastDestination = raycastDestination;
        TakeRisks = takeRisks;
        DoNotAvoidBody = doNotAvoidBody;
    }

    public void Stop()
    {
        SetDestination(null, 0f, 0f, false, false, false, null);
    }

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        _subsystemTime = Project.FindSubsystem<SubsystemTime>(true)!;
        _subsystemBodies = Project.FindSubsystem<SubsystemBodies>(true)!;
        _subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true)!;
        _componentCreature = Entity.FindComponent<ComponentCreature>(true)!;
    }

    public bool IsTerrainSafeToGo(Vector3 position, Vector3 direction)
    {
        var vector = position + new Vector3(0f, 0.1f, 0f) + (direction.LengthSquared() < 1.2f
            ? new Vector3(direction.X, 0f, direction.Z)
            : 1.2f * Vector3.Normalize(new Vector3(direction.X, 0f, direction.Z)));
        for (var i = -1; i <= 1; i++)
        for (var j = -1; j <= 1; j++)
        {
            if (!(Vector3.Dot(direction, new Vector3(i, 0f, j)) > 0f))
            {
                continue;
            }

            for (var num = 0; num >= -2; num--)
            {
                var cellValue = _subsystemTerrain.Terrain.GetCellValue(Terrain.ToCell(vector.X) + i,
                    Terrain.ToCell(vector.Y) + num, Terrain.ToCell(vector.Z) + j);
                var block = BlocksManager.Blocks[Terrain.ExtractContents(cellValue)];
                if (block.ShouldAvoid(cellValue))
                {
                    return false;
                }

                if (block.Collidable)
                {
                    break;
                }
            }
        }

        var vector2 = position + new Vector3(0f, 0.1f, 0f) + (direction.LengthSquared() < 1f
            ? new Vector3(direction.X, 0f, direction.Z)
            : 1f * Vector3.Normalize(new Vector3(direction.X, 0f, direction.Z)));
        var flag = true;
        var num2 = TakeRisks ? 7 : 5;
        for (var num3 = 0; num3 >= -num2; num3--)
        {
            var cellValue2 = _subsystemTerrain.Terrain.GetCellValue(Terrain.ToCell(vector2.X),
                Terrain.ToCell(vector2.Y) + num3, Terrain.ToCell(vector2.Z));
            var block2 = BlocksManager.Blocks[Terrain.ExtractContents(cellValue2)];
            if ((!block2.Collidable && block2.BlockIndex != 18) || block2.ShouldAvoid(cellValue2))
            {
                continue;
            }

            flag = false;
            break;
        }

        return !flag;
    }

    public ComponentBody? FindNearestBodyInFront(Vector3 position, Vector2 direction)
    {
        if (_subsystemTime.GameTime >= _nextBodiesUpdateTime)
        {
            _nextBodiesUpdateTime = _subsystemTime.GameTime + 0.5;
            _nearbyBodies.Clear();
            _subsystemBodies.FindBodiesAroundPoint(_componentCreature.ComponentBody.Position.XZ, 4f, _nearbyBodies);
        }

        ComponentBody? result = null;
        var num = float.MaxValue;
        foreach (var nearbyBody in _nearbyBodies)
        {
            if (nearbyBody != _componentCreature.ComponentBody &&
                !(MathUtils.Abs(nearbyBody.Position.Y - _componentCreature.ComponentBody.Position.Y) > 1.1f) &&
                Vector2.Dot(nearbyBody.Position.XZ - position.XZ, direction) > 0f)
            {
                var num2 = Vector2.DistanceSquared(nearbyBody.Position.XZ, position.XZ);
                if (!(num2 < num))
                {
                    continue;
                }

                num = num2;
                result = nearbyBody;
            }
        }

        return result;
    }

    public Vector3 AvoidNearestBody(Vector3 position, Vector3 destination)
    {
        var v = destination.XZ - position.XZ;
        var componentBody = FindNearestBodyInFront(position, Vector2.Normalize(v));
        if (componentBody == null || componentBody == DoNotAvoidBody)
        {
            return destination;
        }

        var num = 0.72f * (componentBody.BoxSize.X + _componentCreature.ComponentBody.BoxSize.X) + 0.5f;
        var xZ = componentBody.Position.XZ;
        var v2 = Segment2.NearestPoint(new Segment2(position.XZ, destination.XZ), xZ) - xZ;
        if (!(v2.LengthSquared() < num * num))
        {
            return destination;
        }

        var num2 = v.Length();
        var v3 = Vector2.Normalize(xZ + Vector2.Normalize(v2) * num - position.XZ);
        return Vector2.Dot(v / num2, v3) > 0.5f
            ? new Vector3(position.X + v3.X * num2, destination.Y, position.Z + v3.Y * num2)
            : destination;
    }

    public static Vector2? CombineNullables(Vector2? v1, Vector2? v2)
    {
        if (!v1.HasValue)
        {
            return v2;
        }

        if (!v2.HasValue)
        {
            return v1;
        }

        return v1.Value + v2.Value;
    }

    public static Vector3? CombineNullables(Vector3? v1, Vector3? v2)
    {
        if (!v1.HasValue)
        {
            return v2;
        }

        if (!v2.HasValue)
        {
            return v1;
        }

        return v1.Value + v2.Value;
    }
}
