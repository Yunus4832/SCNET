using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Network.Interpolation;

namespace Game.Components;

public class ComponentBody : ComponentFrame, IUpdateable
{
    public UpdateExecutionSide UpdateSide => UpdateExecutionSide.Both;

    public const float SleepThresholdSpeed = 1E-05f;

    public const float MaxSpeed = 25f;

    private static readonly Vector3[] _freeSpaceOffsets;

    public static bool DrawBodiesBounds;

    public bool CanCrouch;

    public Action<ComponentBody>? CollidedWithBody;

    public event Action<Vector3>? ImmersionUpdated;

    public ComponentLocomotion? Locomotion;

    private readonly DynamicArray<CollisionBox> _bodiesCollisionBoxes = [];

    private readonly List<ComponentBody> _childBodies = [];

    private readonly DynamicArray<CollisionBox> _collisionBoxes = [];

    private readonly DynamicArray<ComponentBody> _componentBodies = [];

    private float _crouchFactor;

    private float _crushInjureTime;

    private Vector3 _directMove;

    private readonly DynamicArray<CollisionBox> _movingBlocksCollisionBoxes = [];

    private readonly DynamicArray<IMovingBlockSet> _movingBlockSets = [];

    private ComponentBody? _parentBody;

    private float _shakingStrength;

    private float _stoppedTime;

    private SubsystemBlockBehaviors _subsystemBlockBehaviors = null!;

    private SubsystemBodies _subsystemBodies = null!;

    private SubsystemFluidBlockBehavior _subsystemFluidBlockBehavior = null!;

    private SubsystemMovingBlocks _subsystemMovingBlocks = null!;

    private SubsystemTerrain _subsystemTerrain = null!;

    private SubsystemTime _subsystemTime = null!;

    private float _targetCrouchFactor;

    private Vector3 _totalImpulse;

    private Vector3 _velocity;

    public NetPosition NetPosition = null!;

    public NetRotation NetRotation = null!;

    public NetVelocity NetVelocity = null!;

    public ComponentCharacter? Character;

    /// <summary>最近一次应用的状态流轮次序号（客户端联机用，用于丢弃旧包）。</summary>
    public uint LastBodyStateTick;

    static ComponentBody()
    {
        var list = new List<Vector3>();
        for (var i = -2; i <= 2; i++)
        {
            for (var j = -2; j <= 2; j++)
            {
                for (var k = -2; k <= 2; k++)
                {
                    var item = new Vector3(0.25f * i, 0.25f * j, 0.25f * k);
                    list.Add(item);
                }
            }
        }

        list.Sort((o1, o2) => Comparer<float>.Default.Compare(o1.LengthSquared(), o2.LengthSquared()));
        _freeSpaceOffsets = list.ToArray();
    }

    public float TargetCrouchFactor
    {
        get => _targetCrouchFactor;
        set
        {
            if (!StandingOnValue.HasValue || !CanCrouch)
            {
                value = 0f;
            }

            _targetCrouchFactor = value;
        }
    }

    public float CrouchFactor
    {
        get => _crouchFactor;
        set
        {
            if (!StandingOnValue.HasValue || !CanCrouch)
            {
                value = 0f;
            }

            _crouchFactor = value;
            _targetCrouchFactor = value;
        }
    }

    public bool IsSneaking { get => CrouchFactor > 0; set => TargetCrouchFactor = value ? 1 : 0; }

    public Vector3 StanceBoxSize => new(BoxSize.X, (CrouchFactor >= 1f ? 0.4f : 1f) * BoxSize.Y, BoxSize.Z); //参与运算的碰撞箱

    public Vector3 BoxSize //原始碰撞箱
    {
        get;
        set;
    }

    public float Mass { get; set; }

    public float Density { get; set; }

    public Vector2 AirDrag { get; set; }

    public Vector2 WaterDrag { get; set; }

    public float WaterSwayAngle { get; set; }

    public float WaterTurnSpeed { get; set; }

    public float ImmersionDepth { get; set; }

    public float ImmersionFactor { get; set; }

    public FluidBlock? ImmersionFluidBlock { get; set; }

    public int? StandingOnValue { get; set; }

    public ComponentBody? StandingOnBody { get; set; }

    public Vector3 StandingOnVelocity { get; set; }

    public Vector3 Velocity
    {
        get
        {
            if (_velocity.LengthSquared() < 0.00001f)
            {
                _velocity = Vector3.Zero;
            }

            return _velocity;
        }
        set
        {
            if (value.LengthSquared() > 625f)
            {
                _velocity = 25f * Vector3.Normalize(value);
            }
            else
            {
                _velocity = value;
            }
        }
    }

    public bool IsGravityEnabled { get; set; }

    public bool IsGroundDragEnabled { get; set; }

    public bool IsWaterDragEnabled { get; set; }

    public bool IsSmoothRiseEnabled { get; set; }

    public float MaxSmoothRiseHeight { get; set; }

    public Vector3 CollisionVelocityChange { get; set; }

    public BoundingBox BoundingBox
    {
        get
        {
            var stanceBoxSize = StanceBoxSize;
            var position = base.Position;
            return new BoundingBox(
                position - new Vector3(stanceBoxSize.X / 2f, 0f, stanceBoxSize.Z / 2f),
                position + new Vector3(stanceBoxSize.X / 2f, stanceBoxSize.Y, stanceBoxSize.Z / 2f)
            );
        }
    }

    public ReadOnlyList<ComponentBody> ChildBodies => new(_childBodies);

    public ComponentBody? ParentBody
    {
        get => _parentBody;
        set
        {
            if (value == _parentBody)
            {
                return;
            }

            _parentBody?._childBodies.Remove(this);
            _parentBody = value;
            _parentBody?._childBodies.Add(this);
        }
    }

    public Vector3 ParentBodyPositionOffset { get; set; }

    public Quaternion ParentBodyRotationOffset { get; set; }

    public bool IsTerrainReady => TerrainMovementBoundary.HasCollisionContents(_subsystemTerrain.Terrain, Position);

    public UpdateOrder UpdateOrder
    {
        get
        {
            if (_parentBody == null)
            {
                return UpdateOrder.Body;
            }

            return _parentBody.UpdateOrder + 1;
        }
    }

    public void ApplyAuthoritativePosition(Vector3 position, Vector3 velocity)
    {
        Position = position;
        Velocity = velocity;
        NetPosition = new NetPosition(position);
        NetVelocity = new NetVelocity(velocity);
        CollisionVelocityChange = Vector3.Zero;
        StandingOnValue = null;
        StandingOnBody = null;
        StandingOnVelocity = Vector3.Zero;
        _totalImpulse = Vector3.Zero;
        _directMove = Vector3.Zero;
        _stoppedTime = 0f;
    }

    public bool IsCrushed { get; private set; }

    public void Update(float dt)
    {
        IsCrushed = false;
        if (!WorldRuntimeContext.For(Project).IsAuthority)
        {
            if (Character is { CharacterData.IsLocalCharacter: false, ComponentBody.ParentBody: null } ||
                (Character == null && ChildBodies.Count == 0) ||
                (ChildBodies.Count > 0 &&
                 ChildBodies[0].Character != null &&
                 !ChildBodies[0].Character!.CharacterData.IsLocalCharacter))
            {
                Position = NetPosition.Get(dt);
                Rotation = NetRotation.Get(dt);
                Velocity = NetVelocity.Get(dt);
            }
            else
            {
                NetPosition.SetNext(Position);
                NetRotation.SetNext(Rotation);
                NetVelocity.SetNext(Velocity);
            }
        }

        if (WorldRuntimeContext.For(Project).IsAuthority)
        {
            if (Character is { CharacterData.IsLocalCharacter: false, ComponentBody.ParentBody: null } ||
                (Character == null &&
                 ChildBodies.Count > 0 &&
                 ChildBodies[0].Character != null &&
                 !ChildBodies[0].Character!.CharacterData.IsLocalCharacter))
            {
                Position = NetPosition.Get(dt);
                Rotation = NetRotation.Get(dt);
                Velocity = NetVelocity.Get(dt);
            }
            else
            {
                NetPosition.SetNext(Position);
                NetRotation.SetNext(Rotation);
                NetVelocity.SetNext(Velocity);
            }
        }

        Velocity += _totalImpulse;
        _totalImpulse = Vector3.Zero;
        if (_parentBody != null ||
            _velocity.LengthSquared() > 9.99999944E-11f ||
            _directMove != Vector3.Zero ||
            _targetCrouchFactor.UncloseTo(_crouchFactor))
        {
            _stoppedTime = 0f;
        }
        else
        {
            _stoppedTime += dt;
            if (_stoppedTime > 0.5f && !WorldRuntimeContext.For(Project).Clock.PeriodicEvent(0.25, 0.0))
            {
                return;
            }
        }

        if (_targetCrouchFactor > _crouchFactor)
        {
            Velocity += new Vector3(0, 0.000001f, 0);
            _crouchFactor = MathUtils.Min(_crouchFactor + 2f * dt, _targetCrouchFactor);
        }

        if (_targetCrouchFactor < _crouchFactor)
        {
            Velocity += new Vector3(0, 0.000001f, 0);
            if (Entity.FindComponent<ComponentRider>()?.Mount == null)
            {
                _crouchFactor = MathUtils.Max(_crouchFactor - 2f * dt, _targetCrouchFactor);
            }
        }

        var position = base.Position;
        if (!IsTerrainReady)
        {
            Velocity = Vector3.Zero;
            return;
        }

        _bodiesCollisionBoxes.Clear();
        FindBodiesCollisionBoxes(position, _bodiesCollisionBoxes);
        _movingBlocksCollisionBoxes.Clear();
        FindMovingBlocksCollisionBoxes(position, _movingBlocksCollisionBoxes);
        //卡住地形中受伤
        if (!MoveToFreeSpace(0.6f))
        {
            _crouchFactor = CanCrouch ? 1f : 0f;
            _targetCrouchFactor = CanCrouch ? 1f : 0f;
            if (!MoveToFreeSpace(float.PositiveInfinity))
            {
                var componentHealth = Entity.FindComponent<ComponentHealth>();
                if (componentHealth != null)
                {
                    if (_crushInjureTime >= 1f)
                    {
                        componentHealth.Injure(0.15f, null, true, LocalizedText.Literal("Crushed"));
                        _crushInjureTime = 0f;
                    }

                    IsCrushed = true;
                    _crushInjureTime += dt;
                }
                else
                {
                    Project.RemoveEntity(Entity, true);
                }

                return;
            }

            _crushInjureTime = 1f;
        }

        if (IsGravityEnabled)
        {
            _velocity.Y -= 10f * dt;
            if (ImmersionFactor > 0f)
            {
                var buoyancyFactor = ImmersionFactor *
                                     (1f +
                                      0.03f *
                                      MathUtils.Sin(
                                          (float)MathUtils.Remainder(2.0 * _subsystemTime.GameTime, 6.2831854820251465)
                                      ));
                _velocity.Y += 10f * (1f / Density * buoyancyFactor) * dt;
            }
        }

        var horizontalAirDrag = MathUtils.Saturate(AirDrag.X * dt);
        var verticalAirDrag = MathUtils.Saturate(AirDrag.Y * dt);
        _velocity.X *= 1f - horizontalAirDrag;
        _velocity.Y *= 1f - verticalAirDrag;
        _velocity.Z *= 1f - horizontalAirDrag;
        if (IsWaterDragEnabled && ImmersionFactor > 0f && ImmersionFluidBlock != null)
        {
            var flowVelocity = _subsystemFluidBlockBehavior.CalculateFlowSpeed(
                Terrain.ToCell(position.X),
                Terrain.ToCell(position.Y),
                Terrain.ToCell(position.Z)
            );
            var fluidVelocity = flowVelocity.HasValue
                ? new Vector3(flowVelocity.Value.X, 0f, flowVelocity.Value.Y)
                : Vector3.Zero;
            var fluidFriction = 1f;
            if (ImmersionFluidBlock.FrictionFactor.UncloseTo(1f))
            {
                fluidFriction = SimplexNoise.Noise(
                                    (float)MathUtils.Remainder(
                                        6.0 * Time.RealTime + GetHashCode() % 1000,
                                        1000.0
                                    )
                                ) >
                                0.5f
                    ? ImmersionFluidBlock.FrictionFactor
                    : 1f;
            }

            var f = MathUtils.Saturate(WaterDrag.X * fluidFriction * ImmersionFactor * dt);
            var f2 = MathUtils.Saturate(WaterDrag.Y * fluidFriction * dt);
            _velocity.X = MathUtils.Lerp(_velocity.X, fluidVelocity.X, f);
            _velocity.Y = MathUtils.Lerp(_velocity.Y, fluidVelocity.Y, f2);
            _velocity.Z = MathUtils.Lerp(_velocity.Z, fluidVelocity.Z, f);
            if (_parentBody == null && flowVelocity.HasValue && !StandingOnValue.HasValue)
            {
                if (WaterTurnSpeed > 0f)
                {
                    var s = MathUtils.Saturate(MathUtils.Lerp(1f, 0f, _velocity.Length()));
                    var turnDirection = Vector2.Normalize(flowVelocity.Value) * s;
                    Rotation *= Quaternion.CreateFromAxisAngle(
                        Vector3.UnitY,
                        WaterTurnSpeed * (-1f * turnDirection.X + 0.71f * turnDirection.Y) * dt
                    );
                }

                if (WaterSwayAngle > 0f)
                {
                    Rotation *= Quaternion.CreateFromAxisAngle(
                        Vector3.UnitX,
                        WaterSwayAngle * (float)MathUtils.Sin(200f / Mass * _subsystemTime.GameTime)
                    );
                }
            }
        }

        if (_parentBody != null)
        {
            var v = Vector3.Transform(ParentBodyPositionOffset, _parentBody.Rotation) +
                    _parentBody.Position -
                    position;
            _velocity = dt > 0f ? v / dt : Vector3.Zero;
            Rotation = ParentBodyRotationOffset * _parentBody.Rotation;
        }

        StandingOnValue = null;
        StandingOnBody = null;
        StandingOnVelocity = Vector3.Zero;
        var velocity = _velocity;
        var speed = _velocity.Length();
        if (speed > 0f)
        {
            var x = 0.45f * MathUtils.Min(StanceBoxSize.X, StanceBoxSize.Y, StanceBoxSize.Z) / speed;
            var remainingTime = dt;
            while (remainingTime > 0f)
            {
                var stepTime = MathUtils.Min(remainingTime, x);
                MoveWithCollision(stepTime, _velocity * stepTime + _directMove);
                _directMove = Vector3.Zero;
                remainingTime -= stepTime;
            }
        }

        CollisionVelocityChange = _velocity - velocity;
        if (_shakingStrength > 1f)
        {
            var shakeAcceleration = default(Vector3);
            shakeAcceleration.X = _shakingStrength *
                                  MathUtils.Sin(
                                      (float)MathUtils.Remainder(31.0 * _subsystemTime.GameTime, Math.PI * 2.0)
                                  );
            shakeAcceleration.Y = 0.4f *
                                  _shakingStrength *
                                  MathUtils.Sin(
                                      (float)MathUtils.Remainder(23.3 * _subsystemTime.GameTime, Math.PI * 2.0)
                                  );
            shakeAcceleration.Z = _shakingStrength *
                                  MathUtils.Sin(
                                      (float)MathUtils.Remainder(27.6 * _subsystemTime.GameTime, Math.PI * 2.0)
                                  );
            Velocity += shakeAcceleration * dt;
            _shakingStrength *= MathUtils.Saturate(1f - 3.5f * dt);
        }
        else
        {
            _shakingStrength = 0f;
        }

        if (IsGroundDragEnabled && StandingOnValue.HasValue)
        {
            _velocity = Vector3.Lerp(_velocity, StandingOnVelocity, 6f * dt);
        }

        if (!StandingOnValue.HasValue && dt != 0)
        {
            //原版从高处掉落会变站立状态，这里改为不会
            //TargetCrouchFactor = 0f;
        }

        UpdateImmersionData();
        ImmersionUpdated?.Invoke(position);
    }

    public void ApplyImpulse(Vector3 impulse)
    {
        if (!WorldRuntimeContext.For(Project).IsAuthority)
        {
            return;
        }

        NetworkSender.SendToObservers(
            Entity,
            new BodyImpulsePackage { CreatureId = Entity.EntityId, Impulse = impulse }
        );
        ApplyImpulseNet(impulse);
    }

    public void ApplyImpulseNet(Vector3 impulse)
    {
        _totalImpulse += impulse;
    }

    public void ApplyDirectMove(Vector3 directMove)
    {
        _directMove += directMove;
    }

    public bool IsChildOfBody(ComponentBody componentBody)
    {
        if (ParentBody == componentBody)
        {
            return true;
        }

        return ParentBody != null && ParentBody.IsChildOfBody(componentBody);
    }

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        base.Load(valuesDictionary, idToEntityMap);
        _subsystemTime = Project.FindSubsystem<SubsystemTime>(true)!;
        _subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true)!;
        _subsystemBodies = Project.FindSubsystem<SubsystemBodies>(true)!;
        _subsystemMovingBlocks = Project.FindSubsystem<SubsystemMovingBlocks>(true)!;
        _subsystemBlockBehaviors = Project.FindSubsystem<SubsystemBlockBehaviors>(true)!;
        _subsystemFluidBlockBehavior = Project.FindSubsystem<SubsystemFluidBlockBehavior>(true)!;
        CanCrouch = Entity.FindComponent<ComponentCharacter>() != null;
        BoxSize = valuesDictionary.GetValue<Vector3>("BoxSize");
        Mass = valuesDictionary.GetValue<float>("Mass");
        Density = valuesDictionary.GetValue<float>("Density");
        AirDrag = valuesDictionary.GetValue<Vector2>("AirDrag");
        WaterDrag = valuesDictionary.GetValue<Vector2>("WaterDrag");
        WaterSwayAngle = valuesDictionary.GetValue<float>("WaterSwayAngle");
        WaterTurnSpeed = valuesDictionary.GetValue<float>("WaterTurnSpeed");
        Velocity = valuesDictionary.GetValue<Vector3>("Velocity");
        MaxSmoothRiseHeight = valuesDictionary.GetValue<float>("MaxSmoothRiseHeight");
        var parentReference = valuesDictionary.GetValue<object>("ParentBody");

        ParentBodyPositionOffset = valuesDictionary.GetValue<Vector3>("ParentBodyPositionOffset");
        ParentBodyRotationOffset = valuesDictionary.GetValue<Quaternion>("ParentBodyRotationOffset");
        IsSmoothRiseEnabled = true;
        IsGravityEnabled = true;
        IsGroundDragEnabled = true;
        IsWaterDragEnabled = true;
        Character = Entity.FindComponent<ComponentCharacter>();
        Locomotion = Entity.FindComponent<ComponentLocomotion>();
        if (parentReference is EntityReference reference)
        {
            ParentBody = reference.GetEntity(Entity, idToEntityMap, false)?.FindComponent<ComponentBody>();
        }
        else
        {
            var parentEntityId = Convert.ToInt32(parentReference);
            var entity = idToEntityMap.FindEntity(parentEntityId);
            if (entity is null && parentEntityId > 0)
            {
                Project.FindEntityById(parentEntityId, existing => entity = existing);
            }

            if (entity != null)
            {
                ParentBody = entity.FindComponent<ComponentBody>();
            }
        }

        _crushInjureTime = 1f;
        NetPosition = new NetPosition(Position);
        NetRotation = new NetRotation(Rotation);
        NetVelocity = new NetVelocity(Velocity);
    }

    public override void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap)
    {
        base.Save(valuesDictionary, entityToIdMap);
        if (Velocity != Vector3.Zero)
        {
            valuesDictionary.SetValue("Velocity", Velocity);
        }

        if (ParentBody != null)
        {
            valuesDictionary.SetValue("ParentBody", ParentBody.Entity.EntityId);
            valuesDictionary.SetValue("ParentBodyPositionOffset", ParentBodyPositionOffset);
            valuesDictionary.SetValue("ParentBodyRotationOffset", ParentBodyRotationOffset);
        }
        else
        {
            valuesDictionary.SetValue("ParentBody", 0);
        }
    }

    public override void OnEntityRemoved()
    {
        ParentBody = null;
        var array = ChildBodies.ToArray();
        foreach (var item in array)
        {
            item.ParentBody = null;
        }
    }

    public void ApplyShaking(float strength)
    {
        _shakingStrength += strength;
    }

    public void UpdateImmersionData()
    {
        var position = base.Position;
        var x = Terrain.ToCell(position.X);
        var y = Terrain.ToCell(position.Y + 0.01f);
        var z = Terrain.ToCell(position.Z);
        var surfaceHeight = _subsystemFluidBlockBehavior.GetSurfaceHeight(x, y, z, out _);
        if (surfaceHeight.HasValue)
        {
            var cellValue = _subsystemTerrain.Terrain.GetCellValue(x, y, z);
            ImmersionDepth = MathUtils.Max(surfaceHeight.Value - position.Y, 0f);
            ImmersionFactor = MathUtils.Saturate(MathUtils.Pow(ImmersionDepth / StanceBoxSize.Y, 0.7f));
            ImmersionFluidBlock = WorldContentBinding.For(Project).Catalog
                .FluidBlocks[Terrain.ExtractContents(cellValue)];
        }
        else
        {
            ImmersionDepth = 0f;
            ImmersionFactor = 0f;
            ImmersionFluidBlock = null;
        }
    }

    public bool MoveToFreeSpace(float maxMoveDistance)
    {
        var stanceBoxSize = StanceBoxSize;
        var position = base.Position;
        foreach (var offset in _freeSpaceOffsets)
        {
            Vector3? vector = null;
            var candidatePosition = position + offset;
            if (Terrain.ToCell(candidatePosition) != Terrain.ToCell(position))
            {
                continue;
            }

            var box = new BoundingBox(
                candidatePosition - new Vector3(stanceBoxSize.X / 2f, 0f, stanceBoxSize.Z / 2f),
                candidatePosition + new Vector3(stanceBoxSize.X / 2f, stanceBoxSize.Y, stanceBoxSize.Z / 2f)
            );
            box.Min += new Vector3(0.01f, MaxSmoothRiseHeight + 0.01f, 0.01f);
            box.Max -= new Vector3(0.01f);
            _collisionBoxes.Clear();
            FindTerrainCollisionBoxes(box, _collisionBoxes);
            _collisionBoxes.AddRange(_movingBlocksCollisionBoxes);
            _collisionBoxes.AddRange(_bodiesCollisionBoxes);
            if (IsColliding(box, _collisionBoxes))
            {
                _stoppedTime = 0f;
                var pushX = CalculatePushBack(box, 0, _collisionBoxes, out _);
                var pushY = CalculatePushBack(box, 1, _collisionBoxes, out _);
                var pushZ = CalculatePushBack(box, 2, _collisionBoxes, out _);
                var squaredPushX = pushX * pushX;
                var squaredPushY = pushY * pushY;
                var squaredPushZ = pushZ * pushZ;
                var list = new List<Vector3>();
                if (squaredPushX <= squaredPushY && squaredPushX <= squaredPushZ)
                {
                    list.Add(candidatePosition + new Vector3(pushX, 0f, 0f));
                    if (squaredPushY <= squaredPushZ)
                    {
                        list.Add(candidatePosition + new Vector3(0f, pushY, 0f));
                        list.Add(candidatePosition + new Vector3(0f, 0f, pushZ));
                    }
                    else
                    {
                        list.Add(candidatePosition + new Vector3(0f, 0f, pushZ));
                        list.Add(candidatePosition + new Vector3(0f, pushY, 0f));
                    }
                }
                else if (squaredPushY <= squaredPushX && squaredPushY <= squaredPushZ)
                {
                    list.Add(candidatePosition + new Vector3(0f, pushY, 0f));
                    if (squaredPushX <= squaredPushZ)
                    {
                        list.Add(candidatePosition + new Vector3(pushX, 0f, 0f));
                        list.Add(candidatePosition + new Vector3(0f, 0f, pushZ));
                    }
                    else
                    {
                        list.Add(candidatePosition + new Vector3(0f, 0f, pushZ));
                        list.Add(candidatePosition + new Vector3(pushX, 0f, 0f));
                    }
                }
                else
                {
                    list.Add(candidatePosition + new Vector3(0f, 0f, pushZ));
                    if (squaredPushX <= squaredPushY)
                    {
                        list.Add(candidatePosition + new Vector3(pushX, 0f, 0f));
                        list.Add(candidatePosition + new Vector3(0f, pushY, 0f));
                    }
                    else
                    {
                        list.Add(candidatePosition + new Vector3(0f, pushY, 0f));
                        list.Add(candidatePosition + new Vector3(pushX, 0f, 0f));
                    }
                }

                foreach (var item in list)
                {
                    box = new BoundingBox(
                        item - new Vector3(stanceBoxSize.X / 2f, 0f, stanceBoxSize.Z / 2f),
                        item + new Vector3(stanceBoxSize.X / 2f, stanceBoxSize.Y, stanceBoxSize.Z / 2f)
                    );
                    box.Min += new Vector3(0.02f, MaxSmoothRiseHeight + 0.02f, 0.02f);
                    box.Max -= new Vector3(0.02f);
                    _collisionBoxes.Clear();
                    FindTerrainCollisionBoxes(box, _collisionBoxes);
                    _collisionBoxes.AddRange(_movingBlocksCollisionBoxes);
                    _collisionBoxes.AddRange(_bodiesCollisionBoxes);
                    if (IsColliding(box, _collisionBoxes))
                    {
                        continue;
                    }

                    vector = item;
                    break;
                }
            }
            else
            {
                vector = candidatePosition;
            }

            if (!vector.HasValue ||
                !TerrainMovementBoundary.HasCollisionContents(_subsystemTerrain.Terrain, vector.Value) ||
                !(Vector3.Distance(vector.Value, base.Position) <= maxMoveDistance))
            {
                continue;
            }

            base.Position = vector.Value;
            return true;
        }

        return false;
    }

    public bool MoveToFreeSpace()
    {
        var boxSize = BoxSize;
        var position = Position;
        foreach (var offset in _freeSpaceOffsets)
        {
            Vector3? vector = null;
            var candidatePosition = position + offset;
            if (Terrain.ToCell(candidatePosition) != Terrain.ToCell(position))
            {
                continue;
            }

            var box = new BoundingBox(
                candidatePosition - new Vector3(boxSize.X / 2f, 0f, boxSize.Z / 2f),
                candidatePosition + new Vector3(boxSize.X / 2f, boxSize.Y, boxSize.Z / 2f)
            );
            box.Min += new Vector3(0.01f, MaxSmoothRiseHeight + 0.01f, 0.01f);
            box.Max -= new Vector3(0.01f);
            _collisionBoxes.Clear();
            FindTerrainCollisionBoxes(box, _collisionBoxes);
            _collisionBoxes.AddRange(_movingBlocksCollisionBoxes);
            _collisionBoxes.AddRange(_bodiesCollisionBoxes);
            if (!IsColliding(box, _collisionBoxes))
            {
                vector = candidatePosition;
            }
            else
            {
                _stoppedTime = 0f;
                var pushX = CalculatePushBack(box, 0, _collisionBoxes, out _);
                var pushY = CalculatePushBack(box, 1, _collisionBoxes, out _);
                var pushZ = CalculatePushBack(box, 2, _collisionBoxes, out _);
                var squaredPushX = pushX * pushX;
                var squaredPushY = pushY * pushY;
                var squaredPushZ = pushZ * pushZ;
                var list = new List<Vector3>();
                if (squaredPushX <= squaredPushY && squaredPushX <= squaredPushZ)
                {
                    list.Add(candidatePosition + new Vector3(pushX, 0f, 0f));
                    if (squaredPushY <= squaredPushZ)
                    {
                        list.Add(candidatePosition + new Vector3(0f, pushY, 0f));
                        list.Add(candidatePosition + new Vector3(0f, 0f, pushZ));
                    }
                    else
                    {
                        list.Add(candidatePosition + new Vector3(0f, 0f, pushZ));
                        list.Add(candidatePosition + new Vector3(0f, pushY, 0f));
                    }
                }
                else if (squaredPushY <= squaredPushX && squaredPushY <= squaredPushZ)
                {
                    list.Add(candidatePosition + new Vector3(0f, pushY, 0f));
                    if (squaredPushX <= squaredPushZ)
                    {
                        list.Add(candidatePosition + new Vector3(pushX, 0f, 0f));
                        list.Add(candidatePosition + new Vector3(0f, 0f, pushZ));
                    }
                    else
                    {
                        list.Add(candidatePosition + new Vector3(0f, 0f, pushZ));
                        list.Add(candidatePosition + new Vector3(pushX, 0f, 0f));
                    }
                }
                else
                {
                    list.Add(candidatePosition + new Vector3(0f, 0f, pushZ));
                    if (squaredPushX <= squaredPushY)
                    {
                        list.Add(candidatePosition + new Vector3(pushX, 0f, 0f));
                        list.Add(candidatePosition + new Vector3(0f, pushY, 0f));
                    }
                    else
                    {
                        list.Add(candidatePosition + new Vector3(0f, pushY, 0f));
                        list.Add(candidatePosition + new Vector3(pushX, 0f, 0f));
                    }
                }

                foreach (var item in list)
                {
                    box = new BoundingBox(
                        item - new Vector3(boxSize.X / 2f, 0f, boxSize.Z / 2f),
                        item + new Vector3(boxSize.X / 2f, boxSize.Y, boxSize.Z / 2f)
                    );
                    box.Min += new Vector3(0.02f, MaxSmoothRiseHeight + 0.02f, 0.02f);
                    box.Max -= new Vector3(0.02f);
                    _collisionBoxes.Clear();
                    FindTerrainCollisionBoxes(box, _collisionBoxes);
                    _collisionBoxes.AddRange(_movingBlocksCollisionBoxes);
                    _collisionBoxes.AddRange(_bodiesCollisionBoxes);
                    if (!IsColliding(box, _collisionBoxes))
                    {
                        vector = item;
                        break;
                    }
                }
            }

            if (!vector.HasValue)
            {
                continue;
            }

            if (!TerrainMovementBoundary.HasCollisionContents(_subsystemTerrain.Terrain, vector.Value)) { continue; }

            Position = vector.Value;
            return true;
        }

        return false;
    }

    /**
     * 骑乘位置控制
     * 下落位置控制
     */
    public void MoveWithCollision(float dt, Vector3 move)
    {
        var position = base.Position;
        var isSmoothRising =
            IsSmoothRiseEnabled && MaxSmoothRiseHeight > 0f && HandleSmoothRise(ref move, position, dt);
        HandleAxisCollision(1, move.Y, ref position, isSmoothRising);
        HandleAxisCollision(0, move.X, ref position, isSmoothRising);
        HandleAxisCollision(2, move.Z, ref position, isSmoothRising);
        Position = position;
    }

    public bool HandleSmoothRise(ref Vector3 move, Vector3 position, float dt)
    {
        var boxSize = StanceBoxSize;
        var box = new BoundingBox(
            position - new Vector3(boxSize.X / 2f, 0f, boxSize.Z / 2f),
            position + new Vector3(boxSize.X / 2f, boxSize.Y, boxSize.Z / 2f)
        );
        box.Min += new Vector3(0.04f, 0f, 0.04f);
        box.Max -= new Vector3(0.04f, 0f, 0.04f);
        _collisionBoxes.Clear();
        FindTerrainCollisionBoxes(box, _collisionBoxes);
        _collisionBoxes.AddRange(_movingBlocksCollisionBoxes);
        var riseHeight = MathUtils.Max(CalculatePushBack(box, 1, _collisionBoxes, out var pushingCollisionBox), 0f);
        if (WorldContentBinding.For(Project).Catalog
                .Blocks[Terrain.ExtractContents(pushingCollisionBox.BlockValue)].NoSmoothRise ||
            !(riseHeight > 0.04f))
        {
            return false;
        }

        var x = MathUtils.Min(4.5f * dt, riseHeight);
        move.Y = MathUtils.Max(move.Y, x);
        _velocity.Y = MathUtils.Max(_velocity.Y, 0f);
        StandingOnValue = pushingCollisionBox.BlockValue;
        StandingOnBody = pushingCollisionBox.ComponentBody;
        _stoppedTime = 0f;
        return true;
    }

    public void HandleAxisCollision(int axis, float move, ref Vector3 position, bool isSmoothRising)
    {
        var start = position;
        var bounded = TerrainMovementBoundary.ClipAxisMove(_subsystemTerrain.Terrain, start, StanceBoxSize, axis, move);
        var constrained = bounded != move;
        ResolveAxisCollision(axis, bounded, ref position, isSmoothRising);
        var displacement = GetVectorComponent(position - start, axis);
        var resolved = TerrainMovementBoundary.ClipAxisMove(
            _subsystemTerrain.Terrain,
            start,
            StanceBoxSize,
            axis,
            displacement
        );
        if (resolved != displacement)
        {
            constrained = true;
            if (axis == 0) { position.X = start.X + resolved; }
            else if (axis == 2) { position.Z = start.Z + resolved; }
        }

        if (constrained)
        {
            if (axis == 0) { _velocity.X = 0f; }
            else if (axis == 2) { _velocity.Z = 0f; }
        }
    }

    private void ResolveAxisCollision(int axis, float move, ref Vector3 position, bool isSmoothRising)
    {
        var boxSize = StanceBoxSize;
        _collisionBoxes.Clear();
        if (ImmersionFactor <= 0f && _crouchFactor >= 1f && axis != 1) //禁用玩家在水中蹲下的碰撞箱逻辑
        {
            FindSneakCollisionBoxes(position, new Vector2(boxSize.X - 0.08f, boxSize.Z - 0.08f), _collisionBoxes);
        }

        Vector3 v;
        switch (axis)
        {
            case 0:
                position.X += move;
                v = new Vector3(0f, 0.04f, 0.04f);
                break;
            case 1:
                position.Y += move;
                v = new Vector3(0.04f, 0f, 0.04f);
                break;
            default:
                position.Z += move;
                v = new Vector3(0.04f, 0.04f, 0f);
                break;
        }

        var boundingBox = new BoundingBox(
            position - new Vector3(boxSize.X / 2f, 0f, boxSize.Z / 2f) + v,
            position + new Vector3(boxSize.X / 2f, boxSize.Y, boxSize.Z / 2f) - v
        );
        FindTerrainCollisionBoxes(boundingBox, _collisionBoxes);
        _collisionBoxes.AddRange(_movingBlocksCollisionBoxes);
        float terrainPushBack;
        CollisionBox pushingCollisionBox;
        if ((axis != 1) | isSmoothRising)
        {
            var smoothRiseBox = boundingBox;
            // 提取 Min 的值到局部变量
            var min = smoothRiseBox.Min;

            // 修改 Min.Y 的值
            min.Y += MaxSmoothRiseHeight;

            // 将修改后的值重新赋值回 smoothRiseBox.Min
            smoothRiseBox.Min = min;
            terrainPushBack = CalculateSmoothRisePushBack(
                boundingBox,
                smoothRiseBox,
                axis,
                _collisionBoxes,
                out pushingCollisionBox
            );
        }
        else
        {
            terrainPushBack = CalculatePushBack(boundingBox, axis, _collisionBoxes, out pushingCollisionBox);
        }

        var box = new BoundingBox(
            position - new Vector3(boxSize.X / 2f, 0f, boxSize.Z / 2f) + v,
            position + new Vector3(boxSize.X / 2f, boxSize.Y, boxSize.Z / 2f) - v
        );
        // 客户端的 bodyPushBack 为 0
        var bodyPushBack = CalculatePushBack(box, axis, _bodiesCollisionBoxes, out var pushingCollisionBox2);
        if (MathUtils.Abs(terrainPushBack) > MathUtils.Abs(bodyPushBack))
        {
            if (terrainPushBack == 0f)
            {
                return;
            }

            pushingCollisionBox.Collided?.Invoke(this);
            var blockContents = Terrain.ExtractContents(pushingCollisionBox.BlockValue);
            if (WorldContentBinding.For(Project).Catalog.Blocks[blockContents].HasCollisionBehavior)
            {
                var blockBehaviors = _subsystemBlockBehaviors.GetBlockBehaviors(blockContents);
                foreach (var behavior in blockBehaviors)
                {
                    var vector = (pushingCollisionBox.Box.Min + pushingCollisionBox.Box.Max) / 2f;
                    var cellFace = CellFace.FromAxisAndDirection(
                        Terrain.ToCell(vector.X),
                        Terrain.ToCell(vector.Y),
                        Terrain.ToCell(vector.Z),
                        axis,
                        0f - GetVectorComponent(_velocity, axis)
                    );
                    behavior.OnCollide(cellFace, GetVectorComponent(_velocity, axis), this);
                }
            }

            switch (axis)
            {
                case 0:
                    position.X += terrainPushBack;
                    _velocity.X = pushingCollisionBox.BlockVelocity.X;
                    break;
                case 1:
                    position.Y += terrainPushBack;
                    _velocity.Y = pushingCollisionBox.BlockVelocity.Y;
                    if (move < 0f)
                    {
                        StandingOnValue = pushingCollisionBox.BlockValue;
                        StandingOnBody = pushingCollisionBox.ComponentBody;
                        StandingOnVelocity = pushingCollisionBox.BlockVelocity;
                    }

                    break;
                default:
                    position.Z += terrainPushBack;
                    _velocity.Z = pushingCollisionBox.BlockVelocity.Z;
                    break;
            }
        }
        else
        {
            if (bodyPushBack == 0f)
            {
                return;
            }

            //碰撞对方，使对方发生移动
            var componentBody = pushingCollisionBox2.ComponentBody;
            switch (axis)
            {
                case 0:
                    InelasticCollision(
                        _velocity.X,
                        componentBody._velocity.X,
                        Mass,
                        componentBody.Mass,
                        0.5f,
                        out _velocity.X,
                        out componentBody._velocity.X
                    );
                    position.X += bodyPushBack;
                    break;
                case 1:
                    InelasticCollision(
                        _velocity.Y,
                        componentBody._velocity.Y,
                        Mass,
                        componentBody.Mass,
                        0.5f,
                        out _velocity.Y,
                        out componentBody._velocity.Y
                    );
                    position.Y += bodyPushBack;
                    if (move < 0f)
                    {
                        StandingOnValue = pushingCollisionBox2.BlockValue;
                        StandingOnBody = pushingCollisionBox2.ComponentBody;
                        StandingOnVelocity = new Vector3(componentBody._velocity.X, 0f, componentBody._velocity.Z);
                    }

                    break;
                default:
                    InelasticCollision(
                        _velocity.Z,
                        componentBody._velocity.Z,
                        Mass,
                        componentBody.Mass,
                        0.5f,
                        out _velocity.Z,
                        out componentBody._velocity.Z
                    );
                    position.Z += bodyPushBack;
                    break;
            }

            if (!WorldRuntimeContext.For(Project).IsAuthority && Character is { CharacterData.IsLocalCharacter: true })
            {
                NetworkSender.SendToServer(
                    WorldRuntimeContext.For(Project),
                    new BodyCollisionPackage
                    {
                        CreatureId = Entity.EntityId, TargetCreatureId = componentBody.Entity.EntityId,
                        Impulse = componentBody._velocity
                    }
                );
            }

            CollidedWithBody?.Invoke(componentBody);
            componentBody.CollidedWithBody?.Invoke(this);
        }
    }

    public void FindBodiesCollisionBoxes(Vector3 position, DynamicArray<CollisionBox> result)
    {
        _componentBodies.Clear();
        _subsystemBodies.FindBodiesAroundPoint(new Vector2(position.X, position.Z), 4f, _componentBodies);
        for (var i = 0; i < _componentBodies.Count; i++)
        {
            var componentBody = _componentBodies.Array[i];
            if (componentBody != this && componentBody != _parentBody && componentBody._parentBody != this)
            {
                result.Add(
                    new CollisionBox
                    {
                        Box = componentBody.BoundingBox,
                        ComponentBody = componentBody
                    }
                );
            }
        }
    }

    public void FindMovingBlocksCollisionBoxes(Vector3 position, DynamicArray<CollisionBox> result)
    {
        var boxSize = StanceBoxSize;
        var boundingBox = new BoundingBox(
            position - new Vector3(boxSize.X / 2f, 0f, boxSize.Z / 2f),
            position + new Vector3(boxSize.X / 2f, boxSize.Y, boxSize.Z / 2f)
        );
        boundingBox.Min -= new Vector3(1f);
        boundingBox.Max += new Vector3(1f);
        _movingBlockSets.Clear();
        _subsystemMovingBlocks.FindMovingBlocks(boundingBox, false, _movingBlockSets);
        for (var i = 0; i < _movingBlockSets.Count; i++)
        {
            var movingBlockSet = _movingBlockSets.Array[i];
            foreach (var movingBlock in movingBlockSet.Blocks)
            {
                var blockContents = Terrain.ExtractContents(movingBlock.Value);
                var block = WorldContentBinding.For(Project).Catalog.Blocks[blockContents];
                if (!block.Collidable)
                {
                    continue;
                }

                var customCollisionBoxes = block.GetCustomCollisionBoxes(_subsystemTerrain, movingBlock.Value);
                var v = new Vector3(movingBlock.Offset) + movingBlockSet.Position;
                foreach (var box in customCollisionBoxes)
                {
                    result.Add(
                        new CollisionBox
                        {
                            Box = new BoundingBox(v + box.Min, v + box.Max),
                            BlockValue = movingBlock.Value,
                            BlockVelocity = movingBlockSet.CurrentVelocity
                        }
                    );
                }
            }
        }
    }

    public void FindTerrainCollisionBoxes(BoundingBox box, DynamicArray<CollisionBox> result)
    {
        var gameplay = WorldRuleHooks.For(Project);
        if (gameplay is { HasTerrainCollisionBoxesHandlers: true })
        {
            gameplay.Invoke(new TerrainCollisionBoxesContext(this, box, result));
        }

        var point = Terrain.ToCell(box.Min);
        var point2 = Terrain.ToCell(box.Max);
        point.Y = MathUtils.Max(point.Y, 0);
        point2.Y = MathUtils.Min(point2.Y, 255);
        if (point.Y > point2.Y)
        {
            return;
        }

        for (var i = point.X; i <= point2.X; i++)
        {
            for (var j = point.Z; j <= point2.Z; j++)
            {
                var chunkAtCell = _subsystemTerrain.Terrain.GetChunkAtCell(i, j, false);
                if (chunkAtCell is null || !TerrainMovementBoundary.HasCollisionContents(chunkAtCell))
                {
                    continue;
                }

                var cellIndex = TerrainChunk.CalculateCellIndex(i & 0xF, point.Y, j & 0xF);
                var cellY = point.Y;
                while (cellY <= point2.Y)
                {
                    var cellValueFast = chunkAtCell.GetCellValueFast(cellIndex);
                    var blockContents = Terrain.ExtractContents(cellValueFast);
                    if (blockContents != 0)
                    {
                        var block = WorldContentBinding.For(Project).Catalog.Blocks[blockContents];
                        if (block.Collidable)
                        {
                            var customCollisionBoxes = block.GetCustomCollisionBoxes(_subsystemTerrain, cellValueFast);
                            var v = new Vector3(i, cellY, j);
                            foreach (var collisionBox in customCollisionBoxes)
                            {
                                result.Add(
                                    new CollisionBox
                                    {
                                        Box = new BoundingBox(
                                            v + collisionBox.Min,
                                            v + collisionBox.Max
                                        ),
                                        BlockValue = cellValueFast
                                    }
                                );
                            }
                        }
                    }

                    cellY++;
                    cellIndex++;
                }
            }
        }
    }

    public void FindSneakCollisionBoxes(Vector3 position, Vector2 overhang, DynamicArray<CollisionBox> result)
    {
        var cellX = Terrain.ToCell(position.X);
        var cellY = Terrain.ToCell(position.Y);
        var cellZ = Terrain.ToCell(position.Z);
        if (WorldContentBinding.For(Project).Catalog
            .Blocks[_subsystemTerrain.Terrain.GetCellContents(cellX, cellY - 1, cellZ)].Collidable)
        {
            return;
        }

        var towardNegativeX = position.X < cellX + 0.5f;
        var towardNegativeZ = position.Z < cellZ + 0.5f;
        CollisionBox item;
        if (towardNegativeX)
        {
            if (towardNegativeZ)
            {
                var isCollidable = WorldContentBinding.For(Project).Catalog
                    .Blocks[_subsystemTerrain.Terrain.GetCellContents(cellX, cellY - 1, cellZ - 1)].Collidable;
                var isCollidable2 = WorldContentBinding.For(Project).Catalog
                    .Blocks[_subsystemTerrain.Terrain.GetCellContents(cellX - 1, cellY - 1, cellZ)].Collidable;
                var isCollidable3 = WorldContentBinding.For(Project).Catalog
                    .Blocks[_subsystemTerrain.Terrain.GetCellContents(cellX - 1, cellY - 1, cellZ - 1)].Collidable;
                if ((isCollidable && !isCollidable2) || (!isCollidable && !isCollidable2) & isCollidable3)
                {
                    item = new CollisionBox
                    {
                        Box = new BoundingBox(
                            new Vector3(cellX, cellY, cellZ + overhang.Y),
                            new Vector3(cellX + 1, cellY + 1, cellZ + 1)
                        ),
                        BlockValue = 0
                    };
                    result.Add(item);
                }

                if ((!isCollidable && isCollidable2) || (!isCollidable && !isCollidable2) & isCollidable3)
                {
                    item = new CollisionBox
                    {
                        Box = new BoundingBox(
                            new Vector3(cellX + overhang.X, cellY, cellZ),
                            new Vector3(cellX + 1, cellY + 1, cellZ + 1)
                        ),
                        BlockValue = 0
                    };
                    result.Add(item);
                }

                if (isCollidable && isCollidable2)
                {
                    item = new CollisionBox
                    {
                        Box = new BoundingBox(
                            new Vector3(cellX + overhang.X, cellY, cellZ + overhang.Y),
                            new Vector3(cellX + 1, cellY + 1, cellZ + 1)
                        ),
                        BlockValue = 0
                    };
                    result.Add(item);
                }
            }
            else
            {
                var isCollidable4 = WorldContentBinding.For(Project).Catalog
                    .Blocks[_subsystemTerrain.Terrain.GetCellContents(cellX, cellY - 1, cellZ + 1)].Collidable;
                var isCollidable5 = WorldContentBinding.For(Project).Catalog
                    .Blocks[_subsystemTerrain.Terrain.GetCellContents(cellX - 1, cellY - 1, cellZ)].Collidable;
                var isCollidable6 = WorldContentBinding.For(Project).Catalog
                    .Blocks[_subsystemTerrain.Terrain.GetCellContents(cellX - 1, cellY - 1, cellZ + 1)].Collidable;
                if ((isCollidable4 && !isCollidable5) || (!isCollidable4 && !isCollidable5) & isCollidable6)
                {
                    item = new CollisionBox
                    {
                        Box = new BoundingBox(
                            new Vector3(cellX, cellY, cellZ),
                            new Vector3(cellX + 1, cellY + 1, cellZ + 1 - overhang.Y)
                        ),
                        BlockValue = 0
                    };
                    result.Add(item);
                }

                if ((!isCollidable4 && isCollidable5) || (!isCollidable4 && !isCollidable5) & isCollidable6)
                {
                    item = new CollisionBox
                    {
                        Box = new BoundingBox(
                            new Vector3(cellX + overhang.X, cellY, cellZ),
                            new Vector3(cellX + 1, cellY + 1, cellZ + 1)
                        ),
                        BlockValue = 0
                    };
                    result.Add(item);
                }

                if (isCollidable4 && isCollidable5)
                {
                    item = new CollisionBox
                    {
                        Box = new BoundingBox(
                            new Vector3(cellX + overhang.X, cellY, cellZ),
                            new Vector3(cellX + 1, cellY + 1, cellZ + 1 - overhang.Y)
                        ),
                        BlockValue = 0
                    };
                    result.Add(item);
                }
            }
        }
        else if (towardNegativeZ)
        {
            var isCollidable7 = WorldContentBinding.For(Project).Catalog
                .Blocks[_subsystemTerrain.Terrain.GetCellContents(cellX, cellY - 1, cellZ - 1)].Collidable;
            var isCollidable8 = WorldContentBinding.For(Project).Catalog
                .Blocks[_subsystemTerrain.Terrain.GetCellContents(cellX + 1, cellY - 1, cellZ)].Collidable;
            var isCollidable9 = WorldContentBinding.For(Project).Catalog
                .Blocks[_subsystemTerrain.Terrain.GetCellContents(cellX + 1, cellY - 1, cellZ - 1)].Collidable;
            if ((isCollidable7 && !isCollidable8) || (!isCollidable7 && !isCollidable8) & isCollidable9)
            {
                item = new CollisionBox
                {
                    Box = new BoundingBox(
                        new Vector3(cellX, cellY, cellZ + overhang.Y),
                        new Vector3(cellX + 1, cellY + 1, cellZ + 1)
                    ),
                    BlockValue = 0
                };
                result.Add(item);
            }

            if ((!isCollidable7 && isCollidable8) || (!isCollidable7 && !isCollidable8) & isCollidable9)
            {
                item = new CollisionBox
                {
                    Box = new BoundingBox(
                        new Vector3(cellX, cellY, cellZ),
                        new Vector3(cellX + 1 - overhang.X, cellY + 1, cellZ + 1)
                    ),
                    BlockValue = 0
                };
                result.Add(item);
            }

            if (isCollidable7 && isCollidable8)
            {
                item = new CollisionBox
                {
                    Box = new BoundingBox(
                        new Vector3(cellX, cellY, cellZ + overhang.Y),
                        new Vector3(cellX + 1 - overhang.X, cellY + 1, cellZ + 1)
                    ),
                    BlockValue = 0
                };
                result.Add(item);
            }
        }
        else
        {
            var isCollidable10 = WorldContentBinding.For(Project).Catalog
                .Blocks[_subsystemTerrain.Terrain.GetCellContents(cellX, cellY - 1, cellZ + 1)].Collidable;
            var isCollidable11 = WorldContentBinding.For(Project).Catalog
                .Blocks[_subsystemTerrain.Terrain.GetCellContents(cellX + 1, cellY - 1, cellZ)].Collidable;
            var isCollidable12 = WorldContentBinding.For(Project).Catalog
                .Blocks[_subsystemTerrain.Terrain.GetCellContents(cellX + 1, cellY - 1, cellZ + 1)].Collidable;
            if ((isCollidable10 && !isCollidable11) || (!isCollidable10 && !isCollidable11) & isCollidable12)
            {
                item = new CollisionBox
                {
                    Box = new BoundingBox(
                        new Vector3(cellX, cellY, cellZ),
                        new Vector3(cellX + 1, cellY + 1, cellZ + 1 - overhang.Y)
                    ),
                    BlockValue = 0
                };
                result.Add(item);
            }

            if ((!isCollidable10 && isCollidable11) || (!isCollidable10 && !isCollidable11) & isCollidable12)
            {
                item = new CollisionBox
                {
                    Box = new BoundingBox(
                        new Vector3(cellX, cellY, cellZ),
                        new Vector3(cellX + 1 - overhang.X, cellY + 1, cellZ + 1)
                    ),
                    BlockValue = 0
                };
                result.Add(item);
            }

            if (!isCollidable10 || !isCollidable11)
            {
                return;
            }

            item = new CollisionBox
            {
                Box = new BoundingBox(
                    new Vector3(cellX, cellY, cellZ),
                    new Vector3(cellX + 1 - overhang.X, cellY + 1, cellZ + 1 - overhang.Y)
                ),
                BlockValue = 0
            };
            result.Add(item);
        }
    }

    public bool IsColliding(BoundingBox box, DynamicArray<CollisionBox> collisionBoxes)
    {
        return collisionBoxes.Where((_, i) => box.Intersection(collisionBoxes.Array[i].Box)).Any();
    }

    public float CalculatePushBack(
        BoundingBox box,
        int axis,
        DynamicArray<CollisionBox> collisionBoxes,
        out CollisionBox pushingCollisionBox
    )
    {
        pushingCollisionBox = default;
        var largestPushBack = 0f;
        for (var i = 0; i < collisionBoxes.Count; i++)
        {
            var overlap = CalculateBoxBoxOverlap(ref box, ref collisionBoxes.Array[i].Box, axis);
            if (!(MathUtils.Abs(overlap) > MathUtils.Abs(largestPushBack)))
            {
                continue;
            }

            largestPushBack = overlap;
            pushingCollisionBox = collisionBoxes.Array[i];
        }

        return largestPushBack;
    }

    public float CalculateSmoothRisePushBack(
        BoundingBox normalBox,
        BoundingBox smoothRiseBox,
        int axis,
        DynamicArray<CollisionBox> collisionBoxes,
        out CollisionBox pushingCollisionBox
    )
    {
        pushingCollisionBox = default;
        var largestPushBack = 0f;
        for (var i = 0; i < collisionBoxes.Count; i++)
        {
            var overlap = !WorldContentBinding.For(Project).Catalog
                .Blocks[Terrain.ExtractContents(collisionBoxes.Array[i].BlockValue)].NoSmoothRise
                ? CalculateBoxBoxOverlap(ref smoothRiseBox, ref collisionBoxes.Array[i].Box, axis)
                : CalculateBoxBoxOverlap(ref normalBox, ref collisionBoxes.Array[i].Box, axis);
            if (!(MathUtils.Abs(overlap) > MathUtils.Abs(largestPushBack)))
            {
                continue;
            }

            largestPushBack = overlap;
            pushingCollisionBox = collisionBoxes.Array[i];
        }

        return largestPushBack;
    }

    public static float CalculateBoxBoxOverlap(ref BoundingBox b1, ref BoundingBox b2, int axis)
    {
        if (b1.Max.X <= b2.Min.X ||
            b1.Min.X >= b2.Max.X ||
            b1.Max.Y <= b2.Min.Y ||
            b1.Min.Y >= b2.Max.Y ||
            b1.Max.Z <= b2.Min.Z ||
            b1.Min.Z >= b2.Max.Z)
        {
            return 0f;
        }

        switch (axis)
        {
            case 0:
            {
                var twiceFirstCenterX = b1.Min.X + b1.Max.X;
                var twiceSecondCenterX = b2.Min.X + b2.Max.X;
                var firstSizeX = b1.Max.X - b1.Min.X;
                var secondSizeX = b2.Max.X - b2.Min.X;
                var twiceCenterDeltaX = twiceSecondCenterX - twiceFirstCenterX;
                var combinedSizeX = firstSizeX + secondSizeX;
                return 0.5f *
                       (twiceCenterDeltaX > 0f ? twiceCenterDeltaX - combinedSizeX : twiceCenterDeltaX + combinedSizeX);
            }
            case 1:
            {
                var twiceFirstCenterY = b1.Min.Y + b1.Max.Y;
                var twiceSecondCenterY = b2.Min.Y + b2.Max.Y;
                var firstSizeY = b1.Max.Y - b1.Min.Y;
                var secondSizeY = b2.Max.Y - b2.Min.Y;
                var twiceCenterDeltaY = twiceSecondCenterY - twiceFirstCenterY;
                var combinedSizeY = firstSizeY + secondSizeY;
                return 0.5f *
                       (twiceCenterDeltaY > 0f ? twiceCenterDeltaY - combinedSizeY : twiceCenterDeltaY + combinedSizeY);
            }
            default:
            {
                var twiceFirstCenterZ = b1.Min.Z + b1.Max.Z;
                var twiceSecondCenterZ = b2.Min.Z + b2.Max.Z;
                var firstSizeZ = b1.Max.Z - b1.Min.Z;
                var secondSizeZ = b2.Max.Z - b2.Min.Z;
                var twiceCenterDeltaZ = twiceSecondCenterZ - twiceFirstCenterZ;
                var combinedSizeZ = firstSizeZ + secondSizeZ;
                return 0.5f *
                       (twiceCenterDeltaZ > 0f ? twiceCenterDeltaZ - combinedSizeZ : twiceCenterDeltaZ + combinedSizeZ);
            }
        }
    }

    public static float GetVectorComponent(Vector3 v, int axis)
    {
        return axis switch
        {
            0 => v.X,
            1 => v.Y,
            _ => v.Z
        };
    }

    public static void InelasticCollision(
        float v1,
        float v2,
        float m1,
        float m2,
        float cr,
        out float result1,
        out float result2
    )
    {
        var inverseTotalMass = 1f / (m1 + m2);
        result1 = (cr * m2 * (v2 - v1) + m1 * v1 + m2 * v2) * inverseTotalMass;
        result2 = (cr * m1 * (v1 - v2) + m1 * v1 + m2 * v2) * inverseTotalMass;
    }

    public struct CollisionBox
    {
        public int BlockValue;

        public Vector3 BlockVelocity;

        public ComponentBody ComponentBody;

        public BoundingBox Box;

        public Action<ComponentBody>? Collided;
    }
}
