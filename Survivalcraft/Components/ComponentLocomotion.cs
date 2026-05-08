using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;
using Game.NetWork;

namespace Game.Components;

public class ComponentLocomotion : Component, IUpdateable
{
    private readonly SafeFloat _jumpOrder = new();

    private readonly Random _random = new();

    private readonly SafeFloat _speed = new();

    public bool FlyOrderChange;

    private bool _climbing;

    private ComponentCreature _componentCreature = null!;

    private ComponentLevel? _componentLevel;

    private ComponentMount? _componentMount;

    private ComponentPlayer? _componentPlayer;

    private ComponentRider? _componentRider;

    private bool _falling;

    private bool _flying;

    private bool _jumping;

    private double _ladderActivationTime;

    private Vector3? _lastPosition;

    private bool _lookAutoLevelX;

    private bool _lookAutoLevelY;

    private float _minFrictionFactor;

    private double _shoesWarningTime;

    private SubsystemGameInfo _subsystemGameInfo = null!;

    private SubsystemNoise _subsystemNoise = null!;

    private SubsystemTerrain _subsystemTerrain = null!;

    private SubsystemTime _subsystemTime = null!;

    private float _swimBurstRemaining;

    private bool _swimming;

    private bool _walking;

    private float _walkSpeedWhenTurning;

    public NetVector2 NetLookAngles = null!;

    public Vector2? SendLookAngles;

    public float AccelerationFactor { get; set; }

    public float WalkSpeed
    {
        get => _speed.Get();
        set => _speed.Set(value);
    }

    public float LadderSpeed { get; set; }

    public float JumpSpeed { get; set; }

    public float FlySpeed { get; set; }

    public float CreativeFlySpeed { get; set; }

    public float SwimSpeed { get; set; }

    public float TurnSpeed { get; set; }

    public float LookSpeed { get; set; }

    public float InAirWalkFactor { get; set; }

    public float? SlipSpeed { get; set; }

    public Vector2 LookAngles
    {
        get;
        set
        {
            value.X = MathUtils.Clamp(value.X, 0f - MathUtils.DegToRad(140f), MathUtils.DegToRad(140f));
            value.Y = MathUtils.Clamp(value.Y, 0f - MathUtils.DegToRad(82f), MathUtils.DegToRad(82f));
            if (field != value)
            {
                SendLookAngles = value;
            }

            field = value;
        }
    }

    public int? LadderValue { get; set; }

    public Vector2? WalkOrder
    {
        get;
        set
        {
            field = value;
            if (!field.HasValue)
            {
                return;
            }

            var num = field.Value.LengthSquared();
            if (num > 1f)
            {
                field = field.Value / MathUtils.Sqrt(num);
            }
        }
    }

    public Vector3? FlyOrder
    {
        get;
        set
        {
            if (field != value)
            {
                FlyOrderChange = true;
            }

            field = value;
            if (!field.HasValue)
            {
                return;
            }

            var num = field.Value.LengthSquared();
            if (num > 1f)
            {
                field = field.Value / MathUtils.Sqrt(num);
            }
        }
    }

    public Vector3? SwimOrder
    {
        get;
        set
        {
            field = value;
            if (!field.HasValue)
            {
                return;
            }

            var num = field.Value.LengthSquared();
            if (num > 1f)
            {
                field = field.Value / MathUtils.Sqrt(num);
            }
        }
    }

    public Vector2 TurnOrder { get; set; }

    public Vector2 LookOrder { get; set; }

    public float JumpOrder
    {
        get => _jumpOrder.Get();
        set => _jumpOrder.Set(MathUtils.Saturate(value));
    }

    public float StunTime { get; set; }

    public Vector2? LastWalkOrder { get; set; }

    public float LastJumpOrder { get; set; }

    public Vector3? LastFlyOrder { get; set; }

    public Vector3? LastSwimOrder { get; set; }

    public Vector2 LastTurnOrder { get; set; }

    public bool IsCreativeFlyEnabled { get; set; }

    public UpdateOrder UpdateOrder => UpdateOrder.Locomotion;

    public virtual void Update(float dt)
    {
        if (CommonLib.WorkType == WorkType.Client)
        {
            //非玩家
            if (_componentPlayer == null)
            {
                //没有被骑乘
                if (_componentCreature.ComponentBody.ChildBodies.Count == 0)
                {
                    LookAngles = NetLookAngles.Get(dt);
                    return;
                }
                //被骑乘

                var pb = _componentCreature.ComponentBody.ChildBodies[0];
                //骑乘者不是主玩家
                if (pb.Player is { PlayerData.IsMainPlayer: false })
                {
                    LookAngles = NetLookAngles.Get(dt);
                    return;
                }
            }

            if (_componentPlayer is { PlayerData.IsMainPlayer: false })
            {
                LookAngles = NetLookAngles.Get(dt);
                NormalMovement(dt);
                return;
            }
        }
        else if (CommonLib.WorkType == WorkType.Server)
        {
            //非玩家
            if (_componentPlayer == null)
                //被骑乘
            {
                if (_componentCreature.ComponentBody.ChildBodies.Count > 0)
                {
                    var pb = _componentCreature.ComponentBody.ChildBodies[0];
                    //骑乘者不是主玩家
                    if (pb.Player is { PlayerData.IsMainPlayer: false })
                    {
                        LookAngles = NetLookAngles.Get(dt);
                        return;
                    }
                }
            }

            //玩家且不是主玩家
            if (_componentPlayer is { PlayerData.IsMainPlayer: false })
            {
                LookAngles = NetLookAngles.Get(dt);
                NormalMovement(dt);
                return;
            }
        }

        SlipSpeed = null;
        if (_subsystemGameInfo.WorldSettings.GameMode != GameMode.Creative)
        {
            IsCreativeFlyEnabled = false;
        }

        StunTime = MathUtils.Max(StunTime - dt, 0f);
        if (_componentCreature.ComponentHealth.Health > 0f && StunTime <= 0f)
        {
            var position = _componentCreature.ComponentBody.Position;
            var playerStats = _componentCreature.PlayerStats;
            if (playerStats != null)
            {
                var x = _lastPosition.HasValue ? Vector3.Distance(position, _lastPosition.Value) : 0f;
                x = MathUtils.Min(x, 25f * _subsystemTime.PreviousGameTimeDelta);
                playerStats.DistanceTravelled += x;
                if (_componentRider is { Mount: not null })
                {
                    playerStats.DistanceRidden += x;
                }
                else
                {
                    if (_walking)
                    {
                        playerStats.DistanceWalked += x;
                        _walking = false;
                    }

                    if (_falling)
                    {
                        playerStats.DistanceFallen += x;
                        _falling = false;
                    }

                    if (_climbing)
                    {
                        playerStats.DistanceClimbed += x;
                        _climbing = false;
                    }

                    if (_jumping)
                    {
                        playerStats.Jumps++;
                        _jumping = false;
                    }

                    if (_swimming)
                    {
                        playerStats.DistanceSwam += x;
                        _swimming = false;
                    }

                    if (_flying)
                    {
                        playerStats.DistanceFlown += x;
                        _flying = false;
                    }
                }

                playerStats.DeepestDive = MathUtils.Max(playerStats.DeepestDive,
                    _componentCreature.ComponentBody.ImmersionDepth);
                playerStats.LowestAltitude = MathUtils.Min(playerStats.LowestAltitude, position.Y);
                playerStats.HighestAltitude = MathUtils.Max(playerStats.HighestAltitude, position.Y);
                playerStats.EasiestModeUsed = (GameMode)MathUtils.Min((int)_subsystemGameInfo.WorldSettings.GameMode,
                    (int)playerStats.EasiestModeUsed);
            }

            _lastPosition = position;
            _swimBurstRemaining = MathUtils.Saturate(0.1f * _swimBurstRemaining + dt);
            var x2 = Terrain.ToCell(position.X);
            var y = Terrain.ToCell(position.Y + 0.2f);
            var z = Terrain.ToCell(position.Z);
            var cellValue = _subsystemTerrain.Terrain.GetCellValue(x2, y, z);
            var num = Terrain.ExtractContents(cellValue);
            var block = BlocksManager.Blocks[num];
            if (LadderSpeed > 0f && !LadderValue.HasValue && block is LadderBlock &&
                _subsystemTime.GameTime >= _ladderActivationTime && !IsCreativeFlyEnabled &&
                _componentCreature.ComponentBody.ParentBody == null)
            {
                var face = LadderBlock.GetFace(Terrain.ExtractData(cellValue));
                if ((face == 0 && _componentCreature.ComponentBody.CollisionVelocityChange.Z > 0f) ||
                    (face == 1 && _componentCreature.ComponentBody.CollisionVelocityChange.X > 0f) ||
                    (face == 2 && _componentCreature.ComponentBody.CollisionVelocityChange.Z < 0f) ||
                    (face == 3 && _componentCreature.ComponentBody.CollisionVelocityChange.X < 0f) ||
                    !_componentCreature.ComponentBody.StandingOnValue.HasValue)
                {
                    LadderValue = cellValue;
                    _ladderActivationTime = _subsystemTime.GameTime + 0.20000000298023224;
                    _componentCreature.ComponentCreatureSounds.PlayFootstepSound(1f);
                }
            }

            var rotation = _componentCreature.ComponentBody.Rotation;
            var num2 = MathUtils.Atan2(2f * rotation.Y * rotation.W - 2f * rotation.X * rotation.Z,
                1f - 2f * rotation.Y * rotation.Y - 2f * rotation.Z * rotation.Z);
            num2 += (0f - TurnSpeed) * TurnOrder.X * dt;
            _componentCreature.ComponentBody.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, num2);
            LookAngles += LookSpeed * LookOrder * dt;
            if (LadderValue.HasValue)
            {
                LadderMovement(dt, cellValue);
            }
            else
            {
                NormalMovement(dt);
            }
        }
        else
        {
            _componentCreature.ComponentBody.IsGravityEnabled = true;
            _componentCreature.ComponentBody.IsGroundDragEnabled = true;
            _componentCreature.ComponentBody.IsWaterDragEnabled = true;
        }

        LastWalkOrder = WalkOrder;
        LastFlyOrder = FlyOrder;
        LastSwimOrder = SwimOrder;
        LastTurnOrder = TurnOrder;
        LastJumpOrder = JumpOrder;
        WalkOrder = null;
        FlyOrder = null;
        SwimOrder = null;
        TurnOrder = Vector2.Zero;
        JumpOrder = 0f;
        LookOrder = new Vector2(_lookAutoLevelX ? -10f * LookAngles.X / LookSpeed : 0f,
            _lookAutoLevelY ? -10f * LookAngles.Y / LookSpeed : 0f);
    }

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        _subsystemTime = Project.FindSubsystem<SubsystemTime>(true)!;
        _subsystemNoise = Project.FindSubsystem<SubsystemNoise>(true)!;
        _subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true)!;
        _subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true)!;
        _componentCreature = Entity.FindComponent<ComponentCreature>(true)!;
        _componentPlayer = Entity.FindComponent<ComponentPlayer>();
        _componentLevel = Entity.FindComponent<ComponentLevel>();
        _componentMount = Entity.FindComponent<ComponentMount>();
        _componentRider = Entity.FindComponent<ComponentRider>();
        IsCreativeFlyEnabled = valuesDictionary.GetValue<bool>("IsCreativeFlyEnabled");
        AccelerationFactor = valuesDictionary.GetValue<float>("AccelerationFactor");
        WalkSpeed = valuesDictionary.GetValue<float>("WalkSpeed");
        LadderSpeed = valuesDictionary.GetValue<float>("LadderSpeed");
        JumpSpeed = valuesDictionary.GetValue<float>("JumpSpeed");
        CreativeFlySpeed = valuesDictionary.GetValue<float>("CreativeFlySpeed");
        FlySpeed = valuesDictionary.GetValue<float>("FlySpeed");
        SwimSpeed = valuesDictionary.GetValue<float>("SwimSpeed");
        TurnSpeed = valuesDictionary.GetValue<float>("TurnSpeed");
        LookSpeed = valuesDictionary.GetValue<float>("LookSpeed");
        InAirWalkFactor = valuesDictionary.GetValue<float>("InAirWalkFactor");
        _walkSpeedWhenTurning = valuesDictionary.GetValue<float>("WalkSpeedWhenTurning");
        _minFrictionFactor = valuesDictionary.GetValue<float>("MinFrictionFactor");
        _lookAutoLevelX = valuesDictionary.GetValue<bool>("LookAutoLevelX");
        _lookAutoLevelY = valuesDictionary.GetValue<bool>("LookAutoLevelY");
        if (_componentPlayer is null)
        {
            WalkSpeed *= _random.Float(0.85f, 1f);
            FlySpeed *= _random.Float(0.85f, 1f);
            SwimSpeed *= _random.Float(0.85f, 1f);
        }

        NetLookAngles = new NetVector2(LookAngles);
        if (_subsystemGameInfo.WorldSettings.GameMode != GameMode.Creative && IsCreativeFlyEnabled)
        {
            IsCreativeFlyEnabled = false;
        }
    }

    public override void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap)
    {
        valuesDictionary.SetValue("IsCreativeFlyEnabled", IsCreativeFlyEnabled);
    }

    private void NormalMovement(float dt)
    {
        _componentCreature.ComponentBody.IsGravityEnabled = true;
        _componentCreature.ComponentBody.IsGroundDragEnabled = true;
        _componentCreature.ComponentBody.IsWaterDragEnabled = true;
        var velocity = _componentCreature.ComponentBody.Velocity;
        var right = _componentCreature.ComponentBody.Matrix.Right;
        var vector = Vector3.Transform(_componentCreature.ComponentBody.Matrix.Forward,
            Quaternion.CreateFromAxisAngle(right, LookAngles.Y));
        if (WalkSpeed > 0f && WalkOrder.HasValue)
        {
            if (IsCreativeFlyEnabled)
            {
                var v = new Vector3(WalkOrder.Value.X, 0f, WalkOrder.Value.Y);
                if (FlyOrder.HasValue)
                {
                    v += FlyOrder.Value;
                }

                var v2 =
                    !SettingsManager.HorizontalCreativeFlight || _componentPlayer == null ||
                    _componentPlayer.ComponentInput.IsControlledByTouch
                        ? Vector3.Normalize(vector + 0.1f * Vector3.UnitY)
                        : Vector3.Normalize(vector * new Vector3(1f, 0f, 1f));
                var v3 = CreativeFlySpeed * (right * v.X + Vector3.UnitY * v.Y + v2 * v.Z);
                var num = v == Vector3.Zero ? 5f : 3f;
                velocity += MathUtils.Saturate(num * dt) * (v3 - velocity);
                _componentCreature.ComponentBody.IsGravityEnabled = false;
                _componentCreature.ComponentBody.IsGroundDragEnabled = false;
                _flying = true;
            }
            else
            {
                var value = WalkOrder.Value;
                if (_walkSpeedWhenTurning > 0f && MathUtils.Abs(TurnOrder.X) > 0.02f)
                {
                    value.Y = MathUtils.Max(value.Y,
                        MathUtils.Lerp(0f, _walkSpeedWhenTurning,
                            MathUtils.Saturate(2f * MathUtils.Abs(TurnOrder.X))));
                }

                var num2 = WalkSpeed;
                if (_componentCreature.ComponentBody.ImmersionFactor > 0.2f)
                {
                    num2 *= 0.66f;
                }

                if (value.Y < 0f)
                {
                    num2 *= 0.6f;
                }

                if (_componentLevel != null)
                {
                    num2 *= _componentLevel.SpeedFactor;
                }

                if (_componentMount != null)
                {
                    var rider = _componentMount.Rider;
                    var componentClothing = rider?.Entity.FindComponent<ComponentClothing>();
                    if (componentClothing != null)
                    {
                        num2 *= componentClothing.SteedMovementSpeedFactor;
                    }
                }

                var v4 = value.X * Vector3.Normalize(new Vector3(right.X, 0f, right.Z)) +
                         value.Y * Vector3.Normalize(new Vector3(vector.X, 0f, vector.Z));
                var vector2 = num2 * v4 + _componentCreature.ComponentBody.StandingOnVelocity;
                float num4;
                if (_componentCreature.ComponentBody.StandingOnValue.HasValue)
                {
                    var num3 = MathUtils.Max(
                        BlocksManager
                            .Blocks[Terrain.ExtractContents(_componentCreature.ComponentBody.StandingOnValue.Value)]
                            .FrictionFactor, _minFrictionFactor);
                    num4 = MathUtils.Saturate(dt * 6f * AccelerationFactor * num3);
                    if (num3 < 0.25f)
                    {
                        SlipSpeed = num2 * value.Length();
                    }

                    _walking = true;
                }
                else
                {
                    num4 = MathUtils.Saturate(dt * 6f * AccelerationFactor * InAirWalkFactor);
                    if (_componentCreature.ComponentBody.ImmersionFactor > 0f)
                    {
                        _swimming = true;
                    }
                    else
                    {
                        _falling = true;
                    }
                }

                velocity.X += num4 * (vector2.X - velocity.X);
                velocity.Z += num4 * (vector2.Z - velocity.Z);
                var vector3 = value.X * right + value.Y * vector;
                if (_componentLevel != null)
                {
                    vector3 *= _componentLevel.SpeedFactor;
                }

                velocity.Y += 10f * AccelerationFactor * vector3.Y * _componentCreature.ComponentBody.ImmersionFactor *
                              dt;
                _componentCreature.ComponentBody.IsGroundDragEnabled = false;
                if (_componentPlayer != null && Time.PeriodicEvent(10.0, 0.0) &&
                    (_shoesWarningTime == 0.0 || Time.FrameStartTime - _shoesWarningTime > 300.0) &&
                    _componentCreature.ComponentBody is { StandingOnValue: not null, ImmersionFactor: < 0.1f })
                {
                    var flag = false;
                    var value2 = _componentPlayer.ComponentClothing.GetClothes(ClothingSlot.Feet).LastOrDefault();
                    if (Terrain.ExtractContents(value2) == ClothingBlock.Index)
                    {
                        flag = BlocksManager.Blocks[Terrain.ExtractContents(value2)]
                            .GetClothingData(Terrain.ExtractData(value2)).MovementSpeedFactor > 1f;
                    }

                    if (!flag && vector2.LengthSquared() / velocity.LengthSquared() > 0.99f &&
                        WalkOrder.Value.LengthSquared() > 0.99f)
                    {
                        _componentPlayer.ComponentGui.DisplaySmallMessage(LanguageControl.Get(GetType().Name, 0),
                            Color.White, true, true);
                        _shoesWarningTime = Time.FrameStartTime;
                    }
                }
            }
        }

        if (FlySpeed > 0f && FlyOrder.HasValue)
        {
            var value3 = FlyOrder.Value;
            var v5 = FlySpeed * value3;
            velocity += MathUtils.Saturate(2f * AccelerationFactor * dt) * (v5 - velocity);
            _componentCreature.ComponentBody.IsGravityEnabled = false;
            _flying = true;
        }

        if (SwimSpeed > 0f && SwimOrder.HasValue && _componentCreature.ComponentBody.ImmersionFactor > 0.5f)
        {
            var value4 = SwimOrder.Value;
            var v6 = SwimSpeed * value4;
            var num5 = 2f;
            if (value4.LengthSquared() >= 0.99f)
            {
                v6 *= MathUtils.Lerp(1f, 2f, _swimBurstRemaining);
                num5 *= MathUtils.Lerp(1f, 4f, _swimBurstRemaining);
                _swimBurstRemaining -= dt;
            }

            velocity += MathUtils.Saturate(num5 * AccelerationFactor * dt) * (v6 - velocity);
            _componentCreature.ComponentBody.IsGravityEnabled = MathUtils.Abs(value4.Y) <= 0.07f;
            _componentCreature.ComponentBody.IsWaterDragEnabled = false;
            _componentCreature.ComponentBody.IsGroundDragEnabled = false;
            _swimming = true;
        }

        if (JumpOrder > 0f &&
            (_componentCreature.ComponentBody.StandingOnValue.HasValue ||
             _componentCreature.ComponentBody.ImmersionFactor > 0.5f) && !_componentCreature.ComponentBody.IsSneaking)
        {
            var num6 = JumpSpeed;
            if (_componentLevel != null)
            {
                num6 *= 0.25f * (_componentLevel.SpeedFactor - 1f) + 1f;
            }

            velocity.Y = MathUtils.Min(velocity.Y + MathUtils.Saturate(JumpOrder) * num6, num6);
            _jumping = true;
            _componentCreature.ComponentCreatureSounds.PlayFootstepSound(2f);
            _subsystemNoise.MakeNoise(_componentCreature.ComponentBody, 0.25f, 10f);
        }

        if (MathUtils.Abs(_componentCreature.ComponentBody.CollisionVelocityChange.Y) > 3f)
        {
            _componentCreature.ComponentCreatureSounds.PlayFootstepSound(2f);
            _subsystemNoise.MakeNoise(_componentCreature.ComponentBody, 0.25f, 10f);
        }

        _componentCreature.ComponentBody.Velocity = velocity;
    }

    private void LadderMovement(float dt, int value)
    {
        _componentCreature.ComponentBody.IsGravityEnabled = false;
        var position = _componentCreature.ComponentBody.Position;
        var velocity = _componentCreature.ComponentBody.Velocity;
        var num = Terrain.ExtractContents(value);
        if (BlocksManager.Blocks[num] is LadderBlock)
        {
            LadderValue = value;
            if (WalkOrder.HasValue)
            {
                var value2 = WalkOrder.Value;
                var num2 = LadderSpeed * value2.Y;
                velocity.X = 5f * (MathUtils.Floor(position.X) + 0.5f - position.X);
                velocity.Z = 5f * (MathUtils.Floor(position.Z) + 0.5f - position.Z);
                velocity.Y += MathUtils.Saturate(20f * dt) * (num2 - velocity.Y);
                _climbing = true;
            }

            if (_componentCreature.ComponentBody.StandingOnValue.HasValue &&
                _subsystemTime.GameTime >= _ladderActivationTime)
            {
                LadderValue = null;
                _ladderActivationTime = _subsystemTime.GameTime + 0.20000000298023224;
            }
        }
        else
        {
            LadderValue = null;
            _ladderActivationTime = _subsystemTime.GameTime + 0.20000000298023224;
        }

        if (JumpOrder > 0f)
        {
            _componentCreature.ComponentCreatureSounds.PlayFootstepSound(2f);
            velocity += JumpSpeed * _componentCreature.ComponentBody.Matrix.Forward;
            _ladderActivationTime = _subsystemTime.GameTime + 0.33000001311302185;
            LadderValue = null;
            _jumping = true;
        }

        if (IsCreativeFlyEnabled)
        {
            _componentCreature.ComponentCreatureSounds.PlayFootstepSound(1f);
            LadderValue = null;
        }

        if (_componentCreature.ComponentBody.ParentBody != null)
        {
            LadderValue = null;
        }

        _componentCreature.ComponentBody.Velocity = velocity;
    }
}
