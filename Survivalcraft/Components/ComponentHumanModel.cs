using Engine.Graphics;
using Engine.Media;
using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;
using Game.NetWork;
using Game.NetWork.Packages;

namespace Game.Components;

public class ComponentHumanModel : ComponentCreatureModel
{
    public bool HasData;

    private float _aimHandAngle;

    private ModelBone _bodyBone = null!;

    private ComponentMiner? _componentMiner;

    private ComponentPlayer? _componentPlayer;

    private ComponentRider? _componentRider;

    private ComponentSleep? _componentSleep;

    private readonly DrawBlockEnvironmentData _drawBlockEnvironmentData = new();

    private float _footstepsPhase;

    private ModelBone _hand1Bone = null!;

    private ModelBone _hand2Bone = null!;

    private Vector2 _handAngles1;

    private Vector2 _handAngles2;

    private Vector2 _headAngles;

    private ModelBone _headBone = null!;

    private float _headingOffset;

    private Vector3 _inHandItemOffset;

    private Vector3 _inHandItemRotation;

    private ModelBone _leg1Bone = null!;

    private ModelBone _leg2Bone = null!;

    private Vector2 _legAngles1;

    private Vector2 _legAngles2;

    private float _lieDownFactorEye;

    private float _lieDownFactorModel;

    private int _punchCounter;

    private float _punchFactor;

    private float _punchPhase;

    public bool RowLeft;

    public bool RowRight;

    private float _sneakFactor;

    private SubsystemAudio _subsystemAudio = null!;

    private SubsystemModelsRenderer _subsystemModelsRenderer = null!;

    private SubsystemNoise _subsystemNoise = null!;

    private SubsystemTerrain _subsystemTerrain = null!;

    private float _walkAnimationSpeed;

    private float _walkBobHeight;

    private float _walkLegsAngle;

    public override void Update(float dt)
    {
        _sneakFactor = ComponentCreature.ComponentBody.IsSneaking
            ? MathUtils.Min(_sneakFactor + 2f * dt, 1f)
            : MathUtils.Max(_sneakFactor - 2f * dt, 0f);
        if (_componentSleep is { IsSleeping: true } ||
            ComponentCreature.ComponentHealth.Health <= 0f)
        {
            _lieDownFactorEye = MathUtils.Min(_lieDownFactorEye + 1f * dt, 1f);
            _lieDownFactorModel = MathUtils.Min(_lieDownFactorModel + 3f * dt, 1f);
        }
        else
        {
            _lieDownFactorEye = MathUtils.Max(_lieDownFactorEye - 1f * dt, 0f);
            _lieDownFactorModel = MathUtils.Max(_lieDownFactorModel - 3f * dt, 0f);
        }

        var flag = true;
        var flag2 = true;
        var footstepsPhase = _footstepsPhase;
        if (ComponentCreature.ComponentLocomotion.LadderValue.HasValue)
        {
            _footstepsPhase += 1.5f * _walkAnimationSpeed * ComponentCreature.ComponentBody.Velocity.Length() * dt;
            flag2 = false;
        }
        else if (!ComponentCreature.ComponentLocomotion.IsCreativeFlyEnabled)
        {
            var num = ComponentCreature.ComponentLocomotion.SlipSpeed ??
                      (ComponentCreature.ComponentBody.Velocity.XZ -
                       ComponentCreature.ComponentBody.StandingOnVelocity.XZ).Length();
            if (num > 0.5f)
            {
                MovementAnimationPhase += num * dt * _walkAnimationSpeed;
                _footstepsPhase += 1f * _walkAnimationSpeed * num * dt;
                flag = false;
                flag2 = false;
            }
        }

        if (flag)
        {
            var num2 = 0.5f * MathUtils.Floor(2f * MovementAnimationPhase);
            if (MovementAnimationPhase.UncloseTo(num2))
            {
                MovementAnimationPhase = MovementAnimationPhase - num2 > 0.25f
                    ? MathUtils.Min(MovementAnimationPhase + 2f * dt, num2 + 0.5f)
                    : MathUtils.Max(MovementAnimationPhase - 2f * dt, num2);
            }
        }

        if (flag2)
        {
            _footstepsPhase = 0f;
        }

        var num3 = 0f;
        var componentMount = _componentRider != null ? _componentRider.Mount : null;
        if (componentMount != null)
        {
            var componentCreatureModel = componentMount.Entity.FindComponent<ComponentCreatureModel>();
            if (componentCreatureModel != null)
            {
                Bob = componentCreatureModel.Bob;
                num3 = Bob;
            }

            _headingOffset = 0f;
        }
        else
        {
            var x = MathUtils.Sin((float)Math.PI * 2f * MovementAnimationPhase);
            num3 = _walkBobHeight * MathUtils.Sqr(x);
            var num4 = 0f;
            if (ComponentCreature.ComponentLocomotion.LastWalkOrder.HasValue &&
                ComponentCreature.ComponentLocomotion.LastWalkOrder != Vector2.Zero)
            {
                num4 = Vector2.Angle(Vector2.UnitY, ComponentCreature.ComponentLocomotion.LastWalkOrder.Value);
            }

            _headingOffset += MathUtils.NormalizeAngle(num4 - _headingOffset) *
                              MathUtils.Saturate(8f * subsystemTime.GameTimeDelta);
            _headingOffset = MathUtils.NormalizeAngle(_headingOffset);
        }

        var num5 = MathUtils.Min(12f * subsystemTime.GameTimeDelta, 1f);
        Bob += num5 * (num3 - Bob);
        IsAttackHitMoment = false;
        if (AttackOrder)
        {
            _punchFactor = MathUtils.Min(_punchFactor + 4f * dt, 1f);
            var punchPhase = _punchPhase;
            _punchPhase = MathUtils.Remainder(_punchPhase + dt * 2f, 1f);
            if (punchPhase < 0.5f && _punchPhase >= 0.5f)
            {
                IsAttackHitMoment = true;
                _punchCounter++;
            }
        }
        else
        {
            _punchFactor = MathUtils.Max(_punchFactor - 4f * dt, 0f);
            if (_punchPhase != 0f)
            {
                if (_punchPhase > 0.5f)
                {
                    _punchPhase = MathUtils.Remainder(MathUtils.Min(_punchPhase + dt * 2f, 1f), 1f);
                }
                else if (_punchPhase > 0f)
                {
                    _punchPhase = MathUtils.Max(_punchPhase - dt * _punchPhase, 0f);
                }
            }
        }

        if (!HasData)
        {
            RowLeft = RowLeftOrder;
            RowRight = RowRightOrder;
        }
        //保持一段时间
        else if (Time.PeriodicEvent(1.0, 0.9))
        {
            HasData = false;
        }

        if ((RowLeft || RowRight) && componentMount is { ComponentBody.ImmersionFactor: > 0f } &&
            MathUtils.Floor(1.1000000238418579 * subsystemTime.GameTime) !=
            MathUtils.Floor(1.1000000238418579 * (subsystemTime.GameTime - subsystemTime.GameTimeDelta)))
        {
            CommonLib.Net.QueuePackage(new ComponentBehaviorPackage(this));
            _subsystemAudio.PlayRandomSound("Audio/Rowing", random.Float(0.4f, 0.6f), random.Float(-0.3f, 0.2f),
                ComponentCreature.ComponentBody.Position, 3f, true);
        }

        var num6 = MathUtils.Floor(_footstepsPhase);
        if (_footstepsPhase > num6 && footstepsPhase <= num6)
        {
            if (!ComponentCreature.ComponentBody.IsSneaking)
            {
                _subsystemNoise.MakeNoise(ComponentCreature.ComponentBody, 0.25f, 8f);
            }

            if (!ComponentCreature.ComponentCreatureSounds.PlayFootstepSound(1f))
            {
                _footstepsPhase = 0f;
            }
        }

        _aimHandAngle = AimHandAngleOrder;
        _inHandItemOffset = Vector3.Lerp(_inHandItemOffset, InHandItemOffsetOrder, 10f * dt);
        _inHandItemRotation = Vector3.Lerp(_inHandItemRotation, InHandItemRotationOrder, 10f * dt);
        LastAttackOrder = AttackOrder;
        RowLeftOrder = false;
        RowRightOrder = false;
        AttackOrder = false;
        AimHandAngleOrder = 0f;
        InHandItemOffsetOrder = Vector3.Zero;
        InHandItemRotationOrder = Vector3.Zero;
        base.Update(dt);
    }

    public override void Animate()
    {
        var flag = false;
        var skip = false;
        ModsManager.HookAction("OnModelAnimate", loader =>
        {
            loader.OnModelAnimate(this, out skip);
            flag = flag | skip;
            return false;
        });
        if (flag)
        {
            base.Animate();
            return;
        }

        var position = ComponentCreature.ComponentBody.Position;
        var vector = ComponentCreature.ComponentBody.Rotation.ToYawPitchRoll();
        if (_lieDownFactorModel == 0f)
        {
            var componentMount = _componentRider != null ? _componentRider.Mount : null;
            var num = MathUtils.Sin((float)Math.PI * 2f * MovementAnimationPhase);
            position.Y += Bob;
            vector.X += _headingOffset;
            var num2 = (float)MathUtils.Remainder(
                0.75 * subsystemGameInfo.TotalElapsedGameTime + (GetHashCode() & 0xFFFF), 10000.0);
            var x = MathUtils.Clamp(
                MathUtils.Lerp(-0.3f, 0.3f, SimplexNoise.Noise(1.02f * num2 - 100f)) +
                ComponentCreature.ComponentLocomotion.LookAngles.X +
                1f * ComponentCreature.ComponentLocomotion.LastTurnOrder.X + _headingOffset,
                0f - MathUtils.DegToRad(80f), MathUtils.DegToRad(80f));
            var y = MathUtils.Clamp(
                MathUtils.Lerp(-0.3f, 0.3f, SimplexNoise.Noise(0.96f * num2 - 200f)) +
                ComponentCreature.ComponentLocomotion.LookAngles.Y, 0f - MathUtils.DegToRad(45f),
                MathUtils.DegToRad(45f));
            var num3 = 0f;
            var y2 = 0f;
            var x2 = 0f;
            var y3 = 0f;
            var num4 = 0f;
            var num5 = 0f;
            var num6 = 0f;
            var num7 = 0f;
            if (componentMount != null)
            {
                if (componentMount.Entity.ValuesDictionary.DatabaseObject.Name == "Boat")
                {
                    position.Y -= 0.2f;
                    vector.X += (float)Math.PI;
                    num4 = 0.4f;
                    num6 = 0.4f;
                    num5 = 0.2f;
                    num7 = -0.2f;
                    num3 = 1.1f;
                    x2 = 1.1f;
                    y2 = 0.2f;
                    y3 = -0.2f;
                }
                else
                {
                    num4 = 0.5f;
                    num6 = 0.5f;
                    num5 = 0.15f;
                    num7 = -0.15f;
                    y2 = 0.55f;
                    y3 = -0.55f;
                }
            }
            else if (ComponentCreature.ComponentLocomotion.IsCreativeFlyEnabled)
            {
                var num8 = ComponentCreature.ComponentLocomotion.LastWalkOrder.HasValue
                    ? MathUtils.Min(0.03f * ComponentCreature.ComponentBody.Velocity.XZ.LengthSquared(), 0.5f)
                    : 0f;
                num3 = -0.1f - num8;
                x2 = num3;
                y2 = MathUtils.Lerp(0f, 0.25f, SimplexNoise.Noise(1.07f * num2 + 400f));
                y3 = 0f - MathUtils.Lerp(0f, 0.25f, SimplexNoise.Noise(0.93f * num2 + 500f));
            }
            else if (MovementAnimationPhase != 0f)
            {
                num4 = -0.5f * num;
                num6 = 0.5f * num;
                num3 = _walkLegsAngle * num;
                x2 = 0f - num3;
            }

            var num9 = 0f;
            if (_componentMiner != null)
            {
                var num10 = MathUtils.Sin(MathUtils.Sqrt(_componentMiner.PokingPhase) * (float)Math.PI);
                num9 = _componentMiner.ActiveBlockValue == 0 ? 1f * num10 : 0.3f + 1f * num10;
            }

            var num11 = _punchPhase != 0f
                ? (0f - MathUtils.DegToRad(90f)) *
                  MathUtils.Sin((float)Math.PI * 2f * MathUtils.Sigmoid(_punchPhase, 4f))
                : 0f;
            var num12 = (_punchCounter & 1) == 0 ? num11 : 0f;
            var num13 = (_punchCounter & 1) != 0 ? num11 : 0f;
            var num14 = 0f;
            var num15 = 0f;
            var num16 = 0f;
            var num17 = 0f;
            if (RowLeft || RowRight)
            {
                var num18 = 0.6f * (float)MathUtils.Sin(6.91150426864624 * subsystemTime.GameTime);
                var num19 = 0.2f + 0.2f * (float)MathUtils.Cos(6.91150426864624 * (subsystemTime.GameTime + 0.5));
                if (RowLeft)
                {
                    num14 = num18;
                    num15 = num19;
                }

                if (RowRight)
                {
                    num16 = num18;
                    num17 = 0f - num19;
                }
            }

            var num20 = 0f;
            var num21 = 0f;
            var num22 = 0f;
            var num23 = 0f;
            if (_aimHandAngle != 0f)
            {
                num20 = 1.5f;
                num21 = -0.7f;
                num22 = _aimHandAngle * 1f;
                num23 = 0f;
            }

            float num24 = !ComponentCreature.ComponentLocomotion.IsCreativeFlyEnabled ? 1 : 4;
            num4 += MathUtils.Lerp(-0.1f, 0.1f, SimplexNoise.Noise(num2)) + num12 + num14 + num20;
            num5 += MathUtils.Lerp(0f, num24 * 0.15f, SimplexNoise.Noise(1.1f * num2 + 100f)) + num15 + num21;
            num6 += num9 + MathUtils.Lerp(-0.1f, 0.1f, SimplexNoise.Noise(0.9f * num2 + 200f)) + num13 + num16 + num22;
            num7 += 0f - MathUtils.Lerp(0f, num24 * 0.15f, SimplexNoise.Noise(1.05f * num2 + 300f)) + num17 + num23;
            var s = MathUtils.Min(12f * subsystemTime.GameTimeDelta, 1f);
            _headAngles += s * (new Vector2(x, y) - _headAngles);
            _handAngles1 += s * (new Vector2(num4, num5) - _handAngles1);
            _handAngles2 += s * (new Vector2(num6, num7) - _handAngles2);
            _legAngles1 += s * (new Vector2(num3, y2) - _legAngles1);
            _legAngles2 += s * (new Vector2(x2, y3) - _legAngles2);
            if (ComponentCreature.ComponentBody.CrouchFactor == 1)
            {
                _legAngles1 *= 0.5f;
                _legAngles2 *= 0.5f;
            }

            var f = MathUtils.Sigmoid(ComponentCreature.ComponentBody.CrouchFactor, 4f);
            var position2 = new Vector3(0f, MathUtils.Lerp(0f, 4f, f), MathUtils.Lerp(0f, -3.3f, f));
            var position3 = new Vector3(position.X, position.Y - MathUtils.Lerp(0f, 0.7f, f), position.Z);
            var position4 = new Vector3(0f, MathUtils.Lerp(0f, 7f, f), MathUtils.Lerp(0f, 28f, f));
            var scale = new Vector3(1f, 1f, MathUtils.Lerp(1f, 0.5f, f));
            SetBoneTransform(_bodyBone.Index, Matrix.CreateRotationY(vector.X) * Matrix.CreateTranslation(position3));
            SetBoneTransform(_headBone.Index,
                Matrix.CreateRotationX(_headAngles.Y) * Matrix.CreateRotationZ(0f - _headAngles.X));
            SetBoneTransform(_hand1Bone.Index,
                Matrix.CreateRotationY(_handAngles1.Y) * Matrix.CreateRotationX(_handAngles1.X));
            SetBoneTransform(_hand2Bone.Index,
                Matrix.CreateRotationY(_handAngles2.Y) * Matrix.CreateRotationX(_handAngles2.X));
            SetBoneTransform(_leg1Bone.Index,
                Matrix.CreateRotationY(_legAngles1.Y) * Matrix.CreateRotationX(_legAngles1.X) *
                Matrix.CreateTranslation(position4) * Matrix.CreateScale(scale));
            SetBoneTransform(_leg2Bone.Index,
                Matrix.CreateRotationY(_legAngles2.Y) * Matrix.CreateRotationX(_legAngles2.X) *
                Matrix.CreateTranslation(position4) * Matrix.CreateScale(scale));
        }
        else
        {
            var num25 = MathUtils.Max(DeathPhase, _lieDownFactorModel);
            var num26 = 1f - num25;
            var position2 = position +
                            num25 * 0.5f * ComponentCreature.ComponentBody.BoxSize.Y *
                            Vector3.Normalize(
                                ComponentCreature.ComponentBody.Matrix.Forward * new Vector3(1f, 0f, 1f)) +
                            num25 * Vector3.UnitY * ComponentCreature.ComponentBody.BoxSize.Z * 0.1f;
            SetBoneTransform(_bodyBone.Index,
                Matrix.CreateFromYawPitchRoll(vector.X, (float)Math.PI / 2f * num25, 0f) *
                Matrix.CreateTranslation(position2));
            SetBoneTransform(_headBone.Index, Matrix.Identity);
            SetBoneTransform(_hand1Bone.Index,
                Matrix.CreateRotationY(_handAngles1.Y * num26) * Matrix.CreateRotationX(_handAngles1.X * num26));
            SetBoneTransform(_hand2Bone.Index,
                Matrix.CreateRotationY(_handAngles2.Y * num26) * Matrix.CreateRotationX(_handAngles2.X * num26));
            SetBoneTransform(_leg1Bone.Index,
                Matrix.CreateRotationY(_legAngles1.Y * num26) * Matrix.CreateRotationX(_legAngles1.X * num26));
            SetBoneTransform(_leg2Bone.Index,
                Matrix.CreateRotationY(_legAngles2.Y * num26) * Matrix.CreateRotationX(_legAngles2.X * num26));
        }

        base.Animate();
    }

    public override void DrawExtras(Camera camera)
    {
        if (ComponentCreature.ComponentHealth.Health > 0f && _componentMiner != null &&
            _componentMiner.ActiveBlockValue != 0)
        {
            var num = Terrain.ExtractContents(_componentMiner.ActiveBlockValue);
            var block = BlocksManager.Blocks[num];
            var m = AbsoluteBoneTransformsForCamera[_hand2Bone.Index];
            m *= camera.InvertedViewMatrix;
            m.Right = Vector3.Normalize(m.Right);
            m.Up = Vector3.Normalize(m.Up);
            m.Forward = Vector3.Normalize(m.Forward);
            var matrix = Matrix.CreateRotationY(MathUtils.DegToRad(block.InHandRotation.Y) + _inHandItemRotation.Y) *
                         Matrix.CreateRotationZ(MathUtils.DegToRad(block.InHandRotation.Z) + _inHandItemRotation.Z) *
                         Matrix.CreateRotationX(MathUtils.DegToRad(block.InHandRotation.X) + _inHandItemRotation.X) *
                         Matrix.CreateTranslation(block.InHandOffset + _inHandItemOffset) *
                         Matrix.CreateTranslation(new Vector3(0.05f, 0.05f, -0.56f) *
                                                  (ComponentCreature.ComponentBody.BoxSize.Y / 1.77f)) * m;
            var x = Terrain.ToCell(matrix.Translation.X);
            var y = Terrain.ToCell(matrix.Translation.Y);
            var z = Terrain.ToCell(matrix.Translation.Z);
            _drawBlockEnvironmentData.DrawBlockMode = DrawBlockMode.ThirdPerson;
            _drawBlockEnvironmentData.InWorldMatrix = matrix;
            _drawBlockEnvironmentData.Humidity = _subsystemTerrain.Terrain.GetSeasonalHumidity(x, z);
            _drawBlockEnvironmentData.Temperature = _subsystemTerrain.Terrain.GetSeasonalTemperature(x, z) +
                                                    SubsystemWeather.GetTemperatureAdjustmentAtHeight(y);
            _drawBlockEnvironmentData.Light = _subsystemTerrain.Terrain.GetCellLight(x, y, z);
            _drawBlockEnvironmentData.BillboardDirection = -Vector3.UnitZ;
            _drawBlockEnvironmentData.SubsystemTerrain = _subsystemTerrain;
            var matrix2 = matrix * camera.ViewMatrix;
            block.DrawBlock(_subsystemModelsRenderer.PrimitivesRenderer, _componentMiner.ActiveBlockValue,
                Color.White, block.InHandScale, ref matrix2, _drawBlockEnvironmentData);
        }

        if (_componentPlayer != null && camera.GameWidget.PlayerData != _componentPlayer.PlayerData)
        {
            var position =
                Vector3.Transform(
                    ComponentCreature.ComponentBody.Position +
                    1.02f * Vector3.UnitY * ComponentCreature.ComponentBody.BoxSize.Y, camera.ViewMatrix);
            if (position.Z < 0f)
            {
                var color = Color.Lerp(Color.White, Color.Transparent,
                    MathUtils.Saturate((position.Length() - 4f) / 3f));
                if (color.A > 8)
                {
                    var right = Vector3.TransformNormal(
                        0.005f * Vector3.Normalize(Vector3.Cross(camera.ViewDirection, Vector3.UnitY)),
                        camera.ViewMatrix);
                    var down = Vector3.TransformNormal(-0.005f * Vector3.UnitY, camera.ViewMatrix);
                    var font = ContentManager.Get<BitmapFont>("Fonts/Pericles");
                    _subsystemModelsRenderer.PrimitivesRenderer
                        .FontBatch(font, 1, DepthStencilState.DepthRead, RasterizerState.CullNoneScissor,
                            BlendState.AlphaBlend, SamplerState.LinearClamp).QueueText(
                            _componentPlayer.PlayerData.Name, position, right, down, color,
                            TextAnchor.HorizontalCenter | TextAnchor.Bottom);
                }
            }
        }

        base.DrawExtras(camera);
    }

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        base.Load(valuesDictionary, idToEntityMap);
        _subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true)!;
        _subsystemModelsRenderer = Project.FindSubsystem<SubsystemModelsRenderer>(true)!;
        _subsystemNoise = Project.FindSubsystem<SubsystemNoise>(true)!;
        _subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true)!;
        _componentMiner = Entity.FindComponent<ComponentMiner>();
        _componentRider = Entity.FindComponent<ComponentRider>();
        _componentSleep = Entity.FindComponent<ComponentSleep>();
        _componentPlayer = Entity.FindComponent<ComponentPlayer>();
        _walkAnimationSpeed = valuesDictionary.GetValue<float>("WalkAnimationSpeed");
        _walkBobHeight = valuesDictionary.GetValue<float>("WalkBobHeight");
        _walkLegsAngle = valuesDictionary.GetValue<float>("WalkLegsAngle");
    }

    public override void SetModel(Model model)
    {
        base.SetModel(model);
        _bodyBone = Model.FindBone("Body")!;
        _headBone = Model.FindBone("Head")!;
        _leg1Bone = Model.FindBone("Leg1")!;
        _leg2Bone = Model.FindBone("Leg2")!;
        _hand1Bone = Model.FindBone("Hand1")!;
        _hand2Bone = Model.FindBone("Hand2")!;
    }

    public override Vector3 CalculateEyePosition()
    {
        var f = MathUtils.Sigmoid(_lieDownFactorEye, 1f);
        var num = MathUtils.Sigmoid(ComponentCreature.ComponentBody.CrouchFactor, 4f);
        var num2 = 0.875f * ComponentCreature.ComponentBody.BoxSize.Y;
        var num3 = MathUtils.Lerp(MathUtils.Lerp(num2, 0.45f * num2, num), 0.2f * num2, f);
        var matrix = ComponentCreature.ComponentBody.Matrix;
        return ComponentCreature.ComponentBody.Position + matrix.Up * (num3 + 2f * Bob) +
               matrix.Forward * -0.2f * num;
    }

    public override Quaternion CalculateEyeRotation()
    {
        var num = 0f;
        if (_lieDownFactorEye != 0f)
        {
            num += MathUtils.DegToRad(80f) * MathUtils.Sigmoid(MathUtils.Max(_lieDownFactorEye - 0.2f, 0f) / 0.8f, 4f);
        }

        return ComponentCreature.ComponentBody.Rotation * Quaternion.CreateFromYawPitchRoll(
            0f - ComponentCreature.ComponentLocomotion.LookAngles.X,
            ComponentCreature.ComponentLocomotion.LookAngles.Y, num);
    }
}
