using Engine.Graphics;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Network.Transport;

namespace Game.Components;

public class ComponentHumanModel : ComponentCreatureModel
{
    public bool HasData;

    private float _aimHandAngle;

    private ModelNode _bodyNode = null!;

    private ComponentMiner? _componentMiner;

    private ComponentCharacter? _componentCharacter;

    private ComponentRider? _componentRider;

    private ComponentSleep? _componentSleep;

    private readonly DrawBlockEnvironmentData _drawBlockEnvironmentData = new();

    private float _footstepsPhase;

    private ModelNode _hand1Node = null!;

    private ModelNode _hand2Node = null!;

    private Vector2 _handAngles1;

    private Vector2 _handAngles2;

    private Vector2 _headAngles;

    private ModelNode _headNode = null!;

    private float _headingOffset;

    private Vector3 _inHandItemOffset;

    private Vector3 _inHandItemRotation;

    private ModelNode _leg1Node = null!;

    private ModelNode _leg2Node = null!;

    private Vector2 _legAngles1;

    private Vector2 _legAngles2;

    private float _lieDownFactorModel;

    private int _punchCounter;

    private float _punchFactor;

    public bool RowLeft;

    public bool RowRight;

    private float _sneakFactor;

    private SubsystemAudio _subsystemAudio = null!;

    private SubsystemModelsRenderer _subsystemModelsRenderer = null!;

    private SubsystemTerrain _subsystemTerrain = null!;

    private float _walkAnimationSpeed;

    private float _walkBobHeight;

    private float _walkLegsAngle;

    public override void Update(float dt)
    {
        if (!WorldRuntimeContext.For(Project).HasPresentation) { return; }

        var runtime = WorldRuntimeContext.For(Project);
        _sneakFactor = ComponentCreature.ComponentBody.IsSneaking
            ? MathUtils.Min(_sneakFactor + 2f * dt, 1f)
            : MathUtils.Max(_sneakFactor - 2f * dt, 0f);
        if (_componentSleep is { IsSleeping: true } ||
            ComponentCreature.ComponentHealth.Health <= 0f)
        {
            _lieDownFactorModel = MathUtils.Min(_lieDownFactorModel + 3f * dt, 1f);
        }
        else
        {
            _lieDownFactorModel = MathUtils.Max(_lieDownFactorModel - 3f * dt, 0f);
        }

        var settleWalkPose = true;
        var resetFootsteps = true;
        var footstepsPhase = _footstepsPhase;
        if (ComponentCreature.ComponentLocomotion.LadderValue.HasValue)
        {
            _footstepsPhase += 1.5f * _walkAnimationSpeed * ComponentCreature.ComponentBody.Velocity.Length() * dt;
            resetFootsteps = false;
        }
        else if (!ComponentCreature.ComponentLocomotion.IsCreativeFlyEnabled)
        {
            var walkSpeed = ComponentCreature.ComponentLocomotion.SlipSpeed ??
                            (ComponentCreature.ComponentBody.Velocity.XZ -
                             ComponentCreature.ComponentBody.StandingOnVelocity.XZ).Length();
            if (walkSpeed > 0.5f)
            {
                MovementAnimationPhase += walkSpeed * dt * _walkAnimationSpeed;
                _footstepsPhase += 1f * _walkAnimationSpeed * walkSpeed * dt;
                settleWalkPose = false;
                resetFootsteps = false;
            }
        }

        if (settleWalkPose)
        {
            var halfStepStart = 0.5f * MathUtils.Floor(2f * MovementAnimationPhase);
            if (MovementAnimationPhase.UncloseTo(halfStepStart))
            {
                MovementAnimationPhase = MovementAnimationPhase - halfStepStart > 0.25f
                    ? MathUtils.Min(MovementAnimationPhase + 2f * dt, halfStepStart + 0.5f)
                    : MathUtils.Max(MovementAnimationPhase - 2f * dt, halfStepStart);
            }
        }

        if (resetFootsteps)
        {
            _footstepsPhase = 0f;
        }

        var targetBob = 0f;
        var componentMount = _componentRider != null ? _componentRider.Mount : null;
        if (componentMount != null)
        {
            var componentCreatureModel = componentMount.Entity.FindComponent<ComponentCreatureModel>();
            if (componentCreatureModel != null)
            {
                Bob = componentCreatureModel.Bob;
                targetBob = Bob;
            }

            _headingOffset = 0f;
        }
        else
        {
            var x = MathUtils.Sin((float)Math.PI * 2f * MovementAnimationPhase);
            targetBob = _walkBobHeight * MathUtils.Sqr(x);
            var targetHeading = 0f;
            if (ComponentCreature.ComponentLocomotion.LastWalkOrder.HasValue &&
                ComponentCreature.ComponentLocomotion.LastWalkOrder != Vector2.Zero)
            {
                targetHeading = Vector2.Angle(Vector2.UnitY, ComponentCreature.ComponentLocomotion.LastWalkOrder.Value);
            }

            _headingOffset += MathUtils.NormalizeAngle(targetHeading - _headingOffset) *
                              MathUtils.Saturate(8f * subsystemTime.GameTimeDelta);
            _headingOffset = MathUtils.NormalizeAngle(_headingOffset);
        }

        var bobBlend = MathUtils.Min(12f * subsystemTime.GameTimeDelta, 1f);
        Bob += bobBlend * (targetBob - Bob);
        _punchFactor = creaturePose.LastAttackOrder
            ? MathUtils.Min(_punchFactor + 4f * dt, 1f)
            : MathUtils.Max(_punchFactor - 4f * dt, 0f);
        _punchCounter = creaturePose.AttackCounter;

        if (!HasData)
        {
            RowLeft = creaturePose.RowLeftOrder;
            RowRight = creaturePose.RowRightOrder;
        }
        //保持一段时间
        else if (Time.PeriodicEvent(1.0, 0.9))
        {
            HasData = false;
        }

        if ((RowLeft || RowRight) &&
            componentMount is { ComponentBody.ImmersionFactor: > 0f } &&
            MathUtils.Floor(1.1000000238418579 * subsystemTime.GameTime) !=
            MathUtils.Floor(1.1000000238418579 * (subsystemTime.GameTime - subsystemTime.GameTimeDelta)))
        {
            if (Entity.FindComponent<ComponentCharacter>()?.CharacterData.IsLocalCharacter == true)
            {
                NetworkSender.SendToServer(
                    WorldRuntimeContext.For(Project),
                    BehaviorPackageBuilder.CaptureRowing(Entity.EntityId, RowLeft, RowRight)
                );
            }

            if (runtime.HasPresentation)
            {
                _subsystemAudio.PlayRandomSound(
                    "Audio/Rowing",
                    random.Float(0.4f, 0.6f),
                    random.Float(-0.3f, 0.2f),
                    ComponentCreature.ComponentBody.Position,
                    3f,
                    true
                );
            }
        }

        var footstepBoundary = MathUtils.Floor(_footstepsPhase);
        if (_footstepsPhase > footstepBoundary && footstepsPhase <= footstepBoundary)
        {
            if (runtime.HasPresentation &&
                !ComponentCreature.Entity.FindComponent<ComponentCreatureSoundsPresentation>(true)!.PlayFootstepSound(
                    1f
                ))
            {
                _footstepsPhase = 0f;
            }
        }

        _aimHandAngle = creaturePose.AimHandAngleOrder;
        _inHandItemOffset = Vector3.Lerp(_inHandItemOffset, creaturePose.InHandItemOffsetOrder, 10f * dt);
        _inHandItemRotation = Vector3.Lerp(_inHandItemRotation, creaturePose.InHandItemRotationOrder, 10f * dt);
        creaturePose.RowLeftOrder = false;
        creaturePose.RowRightOrder = false;
        creaturePose.AimHandAngleOrder = 0f;
        creaturePose.InHandItemOffsetOrder = Vector3.Zero;
        creaturePose.InHandItemRotationOrder = Vector3.Zero;
        base.Update(dt);
    }

    public override void Animate()
    {
        var position = ComponentCreature.ComponentBody.Position;
        var vector = ComponentCreature.ComponentBody.Rotation.ToYawPitchRoll();
        if (_lieDownFactorModel == 0f)
        {
            var componentMount = _componentRider != null ? _componentRider.Mount : null;
            var strideWave = MathUtils.Sin((float)Math.PI * 2f * MovementAnimationPhase);
            position.Y += Bob;
            vector.X += _headingOffset;
            var idleNoiseTime = (float)MathUtils.Remainder(
                0.75 * subsystemGameInfo.TotalElapsedGameTime + (GetHashCode() & 0xFFFF),
                10000.0
            );
            var x = MathUtils.Clamp(
                MathUtils.Lerp(-0.3f, 0.3f, SimplexNoise.Noise(1.02f * idleNoiseTime - 100f)) +
                ComponentCreature.ComponentLocomotion.LookAngles.X +
                1f * ComponentCreature.ComponentLocomotion.LastTurnOrder.X +
                _headingOffset,
                0f - MathUtils.DegToRad(80f),
                MathUtils.DegToRad(80f)
            );
            var y = MathUtils.Clamp(
                MathUtils.Lerp(-0.3f, 0.3f, SimplexNoise.Noise(0.96f * idleNoiseTime - 200f)) +
                ComponentCreature.ComponentLocomotion.LookAngles.Y,
                0f - MathUtils.DegToRad(45f),
                MathUtils.DegToRad(45f)
            );
            var firstLegPitch = 0f;
            var firstLegYaw = 0f;
            var secondLegPitch = 0f;
            var secondLegYaw = 0f;
            var leftHandPitch = 0f;
            var leftHandYaw = 0f;
            var rightHandPitch = 0f;
            var rightHandYaw = 0f;
            if (componentMount != null)
            {
                if (componentMount.Entity.ValuesDictionary.DatabaseObject.Name == "Boat")
                {
                    position.Y -= 0.2f;
                    vector.X += (float)Math.PI;
                    leftHandPitch = 0.4f;
                    rightHandPitch = 0.4f;
                    leftHandYaw = 0.2f;
                    rightHandYaw = -0.2f;
                    firstLegPitch = 1.1f;
                    secondLegPitch = 1.1f;
                    firstLegYaw = 0.2f;
                    secondLegYaw = -0.2f;
                }
                else
                {
                    leftHandPitch = 0.5f;
                    rightHandPitch = 0.5f;
                    leftHandYaw = 0.15f;
                    rightHandYaw = -0.15f;
                    firstLegYaw = 0.55f;
                    secondLegYaw = -0.55f;
                }
            }
            else if (ComponentCreature.ComponentLocomotion.IsCreativeFlyEnabled)
            {
                var flightLegOffset = ComponentCreature.ComponentLocomotion.LastWalkOrder.HasValue
                    ? MathUtils.Min(0.03f * ComponentCreature.ComponentBody.Velocity.XZ.LengthSquared(), 0.5f)
                    : 0f;
                firstLegPitch = -0.1f - flightLegOffset;
                secondLegPitch = firstLegPitch;
                firstLegYaw = MathUtils.Lerp(0f, 0.25f, SimplexNoise.Noise(1.07f * idleNoiseTime + 400f));
                secondLegYaw = 0f - MathUtils.Lerp(0f, 0.25f, SimplexNoise.Noise(0.93f * idleNoiseTime + 500f));
            }
            else if (MovementAnimationPhase != 0f)
            {
                leftHandPitch = -0.5f * strideWave;
                rightHandPitch = 0.5f * strideWave;
                firstLegPitch = _walkLegsAngle * strideWave;
                secondLegPitch = 0f - firstLegPitch;
            }

            var toolSwingPitch = 0f;
            if (_componentMiner != null)
            {
                var pokeWave = MathUtils.Sin(MathUtils.Sqrt(_componentMiner.PokingPhase) * (float)Math.PI);
                toolSwingPitch = _componentMiner.ActiveBlockValue == 0 ? 1f * pokeWave : 0.3f + 1f * pokeWave;
            }

            var punchPitch = creaturePose.AttackPhase != 0f
                ? (0f - MathUtils.DegToRad(90f)) *
                  MathUtils.Sin((float)Math.PI * 2f * MathUtils.Sigmoid(creaturePose.AttackPhase, 4f))
                : 0f;
            var leftPunchPitch = (_punchCounter & 1) == 0 ? punchPitch : 0f;
            var rightPunchPitch = (_punchCounter & 1) != 0 ? punchPitch : 0f;
            var leftRowPitch = 0f;
            var leftRowYaw = 0f;
            var rightRowPitch = 0f;
            var rightRowYaw = 0f;
            if (RowLeft || RowRight)
            {
                var rowPitch = 0.6f * (float)MathUtils.Sin(6.91150426864624 * subsystemTime.GameTime);
                var rowYaw = 0.2f + 0.2f * (float)MathUtils.Cos(6.91150426864624 * (subsystemTime.GameTime + 0.5));
                if (RowLeft)
                {
                    leftRowPitch = rowPitch;
                    leftRowYaw = rowYaw;
                }

                if (RowRight)
                {
                    rightRowPitch = rowPitch;
                    rightRowYaw = 0f - rowYaw;
                }
            }

            var leftAimPitch = 0f;
            var leftAimYaw = 0f;
            var rightAimPitch = 0f;
            var rightAimYaw = 0f;
            if (_aimHandAngle != 0f)
            {
                leftAimPitch = 1.5f;
                leftAimYaw = -0.7f;
                rightAimPitch = _aimHandAngle * 1f;
                rightAimYaw = 0f;
            }

            float idleArmScale = !ComponentCreature.ComponentLocomotion.IsCreativeFlyEnabled ? 1 : 4;
            leftHandPitch += MathUtils.Lerp(-0.1f, 0.1f, SimplexNoise.Noise(idleNoiseTime)) +
                             leftPunchPitch +
                             leftRowPitch +
                             leftAimPitch;
            leftHandYaw += MathUtils.Lerp(0f, idleArmScale * 0.15f, SimplexNoise.Noise(1.1f * idleNoiseTime + 100f)) +
                           leftRowYaw +
                           leftAimYaw;
            rightHandPitch += toolSwingPitch +
                              MathUtils.Lerp(-0.1f, 0.1f, SimplexNoise.Noise(0.9f * idleNoiseTime + 200f)) +
                              rightPunchPitch +
                              rightRowPitch +
                              rightAimPitch;
            rightHandYaw +=
                0f -
                MathUtils.Lerp(0f, idleArmScale * 0.15f, SimplexNoise.Noise(1.05f * idleNoiseTime + 300f)) +
                rightRowYaw +
                rightAimYaw;
            var s = MathUtils.Min(12f * subsystemTime.GameTimeDelta, 1f);
            _headAngles += s * (new Vector2(x, y) - _headAngles);
            _handAngles1 += s * (new Vector2(leftHandPitch, leftHandYaw) - _handAngles1);
            _handAngles2 += s * (new Vector2(rightHandPitch, rightHandYaw) - _handAngles2);
            _legAngles1 += s * (new Vector2(firstLegPitch, firstLegYaw) - _legAngles1);
            _legAngles2 += s * (new Vector2(secondLegPitch, secondLegYaw) - _legAngles2);
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
            SetNodeTransform(_bodyNode.Index, Matrix.CreateRotationY(vector.X) * Matrix.CreateTranslation(position3));
            SetNodeTransform(
                _headNode.Index,
                Matrix.CreateRotationX(_headAngles.Y) * Matrix.CreateRotationZ(0f - _headAngles.X)
            );
            SetNodeTransform(
                _hand1Node.Index,
                Matrix.CreateRotationY(_handAngles1.Y) * Matrix.CreateRotationX(_handAngles1.X)
            );
            SetNodeTransform(
                _hand2Node.Index,
                Matrix.CreateRotationY(_handAngles2.Y) * Matrix.CreateRotationX(_handAngles2.X)
            );
            SetNodeTransform(
                _leg1Node.Index,
                Matrix.CreateRotationY(_legAngles1.Y) *
                Matrix.CreateRotationX(_legAngles1.X) *
                Matrix.CreateTranslation(position4) *
                Matrix.CreateScale(scale)
            );
            SetNodeTransform(
                _leg2Node.Index,
                Matrix.CreateRotationY(_legAngles2.Y) *
                Matrix.CreateRotationX(_legAngles2.X) *
                Matrix.CreateTranslation(position4) *
                Matrix.CreateScale(scale)
            );
        }
        else
        {
            var lieDownBlend = MathUtils.Max(DeathPhase, _lieDownFactorModel);
            var uprightBlend = 1f - lieDownBlend;
            var position2 = position +
                            lieDownBlend *
                            0.5f *
                            ComponentCreature.ComponentBody.BoxSize.Y *
                            Vector3.Normalize(
                                ComponentCreature.ComponentBody.Matrix.Forward * new Vector3(1f, 0f, 1f)
                            ) +
                            lieDownBlend * Vector3.UnitY * ComponentCreature.ComponentBody.BoxSize.Z * 0.1f;
            SetNodeTransform(
                _bodyNode.Index,
                Matrix.CreateFromYawPitchRoll(vector.X, (float)Math.PI / 2f * lieDownBlend, 0f) *
                Matrix.CreateTranslation(position2)
            );
            SetNodeTransform(_headNode.Index, Matrix.Identity);
            SetNodeTransform(
                _hand1Node.Index,
                Matrix.CreateRotationY(_handAngles1.Y * uprightBlend) *
                Matrix.CreateRotationX(_handAngles1.X * uprightBlend)
            );
            SetNodeTransform(
                _hand2Node.Index,
                Matrix.CreateRotationY(_handAngles2.Y * uprightBlend) *
                Matrix.CreateRotationX(_handAngles2.X * uprightBlend)
            );
            SetNodeTransform(
                _leg1Node.Index,
                Matrix.CreateRotationY(_legAngles1.Y * uprightBlend) *
                Matrix.CreateRotationX(_legAngles1.X * uprightBlend)
            );
            SetNodeTransform(
                _leg2Node.Index,
                Matrix.CreateRotationY(_legAngles2.Y * uprightBlend) *
                Matrix.CreateRotationX(_legAngles2.X * uprightBlend)
            );
        }

        base.Animate();
    }

    public override void DrawExtras(Camera camera)
    {
        if (ComponentCreature.ComponentHealth.Health > 0f &&
            _componentMiner != null &&
            _componentMiner.ActiveBlockValue != 0)
        {
            var heldContents = Terrain.ExtractContents(_componentMiner.ActiveBlockValue);
            var block = Project.FindSubsystem<SubsystemBlockPresentation>(true)!.Catalog.Get(heldContents);
            var m = AbsoluteNodeTransformsForCamera[_hand2Node.Index];
            m *= camera.InvertedViewMatrix;
            m.Right = Vector3.Normalize(m.Right);
            m.Up = Vector3.Normalize(m.Up);
            m.Forward = Vector3.Normalize(m.Forward);
            var matrix = Matrix.CreateRotationY(MathUtils.DegToRad(block.InHandRotation.Y) + _inHandItemRotation.Y) *
                         Matrix.CreateRotationZ(MathUtils.DegToRad(block.InHandRotation.Z) + _inHandItemRotation.Z) *
                         Matrix.CreateRotationX(MathUtils.DegToRad(block.InHandRotation.X) + _inHandItemRotation.X) *
                         Matrix.CreateTranslation(block.InHandOffset + _inHandItemOffset) *
                         Matrix.CreateTranslation(
                             new Vector3(0.05f, 0.05f, -0.56f) *
                             (ComponentCreature.ComponentBody.BoxSize.Y / 1.77f)
                         ) *
                         m;
            var x = Terrain.ToCell(matrix.Translation.X);
            var y = Terrain.ToCell(matrix.Translation.Y);
            var z = Terrain.ToCell(matrix.Translation.Z);
            _drawBlockEnvironmentData.DrawBlockMode = DrawBlockMode.ThirdPerson;
            _drawBlockEnvironmentData.InWorldMatrix = matrix;
            _drawBlockEnvironmentData.Humidity = _subsystemTerrain.Terrain.GetSeasonalHumidity(x, z);
            _drawBlockEnvironmentData.Temperature = _subsystemTerrain.Terrain.GetSeasonalTemperature(x, z) +
                                                    SubsystemWeather.GetTemperatureAdjustmentAtHeight(y);
            _drawBlockEnvironmentData.Light = (LightingManager.SampleEffectiveLight(_subsystemTerrain, x, y, z) ?? 0);
            _drawBlockEnvironmentData.BillboardDirection = -Vector3.UnitZ;
            _drawBlockEnvironmentData.SubsystemTerrain = _subsystemTerrain;
            var matrix2 = matrix * camera.ViewMatrix;
            block.DrawBlock(
                _subsystemModelsRenderer.PrimitivesRenderer,
                _componentMiner.ActiveBlockValue,
                Color.White,
                block.InHandScale,
                ref matrix2,
                _drawBlockEnvironmentData
            );
        }

        if (_componentCharacter != null && camera.GameWidget.CharacterData != _componentCharacter.CharacterData)
        {
            var position =
                Vector3.Transform(
                    ComponentCreature.ComponentBody.Position +
                    1.02f * Vector3.UnitY * ComponentCreature.ComponentBody.BoxSize.Y,
                    camera.ViewMatrix
                );
            if (position.Z < 0f)
            {
                var color = Color.Lerp(
                    Color.White,
                    Color.Transparent,
                    MathUtils.Saturate((position.Length() - 4f) / 3f)
                );
                if (color.A > 8)
                {
                    const float nameTagScale = 0.007f;
                    var right = Vector3.TransformNormal(
                        nameTagScale * Vector3.Normalize(Vector3.Cross(camera.ViewDirection, Vector3.UnitY)),
                        camera.ViewMatrix
                    );
                    var down = Vector3.TransformNormal(-nameTagScale * Vector3.UnitY, camera.ViewMatrix);
                    var font = AssetManager.Get<BitmapFont>("Fonts/Pericles");
                    _subsystemModelsRenderer.PrimitivesRenderer
                        .FontBatch(
                            font,
                            1,
                            DepthStencilState.DepthRead,
                            RasterizerState.CullNoneScissor,
                            BlendState.AlphaBlend,
                            SamplerState.LinearClamp
                        ).QueueText(
                            _componentCharacter.CharacterData.Name,
                            position,
                            right,
                            down,
                            color,
                            TextAnchor.HorizontalCenter | TextAnchor.Bottom
                        );
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
        _subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true)!;
        _componentMiner = Entity.FindComponent<ComponentMiner>();
        _componentRider = Entity.FindComponent<ComponentRider>();
        _componentSleep = Entity.FindComponent<ComponentSleep>();
        _componentCharacter = Entity.FindComponent<ComponentCharacter>();
        _walkAnimationSpeed = valuesDictionary.GetValue<float>("WalkAnimationSpeed");
        _walkBobHeight = valuesDictionary.GetValue<float>("WalkBobHeight");
        _walkLegsAngle = valuesDictionary.GetValue<float>("WalkLegsAngle");
    }

    public override void SetModel(Model model)
    {
        base.SetModel(model);
        _bodyNode = Model.FindNode("Body")!;
        _headNode = Model.FindNode("Head")!;
        _leg1Node = Model.FindNode("Leg1")!;
        _leg2Node = Model.FindNode("Leg2")!;
        _hand1Node = Model.FindNode("Hand1")!;
        _hand2Node = Model.FindNode("Hand2")!;
    }
}
