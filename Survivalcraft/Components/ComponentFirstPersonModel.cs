using Engine.Graphics;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Components;

public class ComponentFirstPersonModel : Component, IDrawable, IUpdateable
{
    private static LitShader? _litShader;

    private static readonly int[] _drawOrders = [1];

    private ComponentMiner _componentMiner = null!;

    private ComponentPlayer _componentPlayer = null!;

    private ComponentRider _componentRider = null!;

    private readonly DrawBlockEnvironmentData _drawBlockEnvironmentData = new();

    private float _handLight;

    private Model _handModel = null!;

    private int _itemLight;

    private Vector3 _itemOffset;

    private Vector3 _itemRotation;

    private Vector2 _lagAngles;

    private Vector3? _lastYpr;

    private double _nextHandLightTime;

    private float _pokeAnimationTime;

    private readonly PrimitivesRenderer3D _primitivesRenderer = new();

    private SubsystemTerrain _subsystemTerrain = null!;

    private float _swapAnimationTime;

    private int _value;

    public Vector3 ItemOffsetOrder { get; set; }

    public Vector3 ItemRotationOrder { get; set; }

    public int[] DrawOrders => _drawOrders;

    private static LitShader LitShader =>
        _litShader ??= new LitShader(
            ShaderCodeManager.GetFast("Shaders/Lit.vsh"),
            ShaderCodeManager.GetFast("Shaders/Lit.psh"),
            2,
            false,
            false,
            true,
            false,
            false
        );

    public void Draw(Camera camera, int drawOrder)
    {
        if (RunMode.Value is RunModeType.HeadlessServer)
        {
            return;
        }

        if (!(_componentPlayer.ComponentHealth.Health > 0f) ||
            !camera.GameWidget.IsEntityFirstPersonTarget(Entity))
        {
            return;
        }

        var viewport = Display.Viewport;
        var viewport2 = viewport;
        viewport2.MaxDepth *= 0.1f;
        Display.Viewport = viewport2;
        try
        {
            var identity = Matrix.Identity;
            if (_swapAnimationTime > 0f)
            {
                var num = MathUtils.Pow(MathUtils.Sin(_swapAnimationTime * (float)Math.PI), 3f);
                identity *= Matrix.CreateTranslation(0f, -0.8f * num, 0.2f * num);
            }

            if (_pokeAnimationTime > 0f)
            {
                var num2 = MathUtils.Sin(MathUtils.Sqrt(_pokeAnimationTime) * (float)Math.PI);
                if (_value != 0)
                {
                    identity *= Matrix.CreateRotationX((0f - MathUtils.DegToRad(90f)) * num2);
                    identity *= Matrix.CreateTranslation(-0.5f * num2, 0.1f * num2, 0f * num2);
                }
                else
                {
                    identity *= Matrix.CreateRotationX((0f - MathUtils.DegToRad(45f)) * num2);
                    identity *= Matrix.CreateTranslation(-0.1f * num2, 0.2f * num2, -0.05f * num2);
                }
            }

            if (_componentRider.Mount != null)
            {
                var componentCreatureModel = _componentRider.Mount.Entity.FindComponent<ComponentCreatureModel>();
                if (componentCreatureModel != null)
                {
                    var num3 = componentCreatureModel.MovementAnimationPhase * (float)Math.PI * 2f + 0.5f;
                    Vector3 position = default;
                    position.Y = 0.02f * MathUtils.Sin(num3);
                    position.Z = 0.02f * MathUtils.Sin(num3);
                    identity *= Matrix.CreateRotationX(0.05f * MathUtils.Sin(num3 * 1f)) *
                                Matrix.CreateTranslation(position);
                }
            }
            else
            {
                var num4 = _componentPlayer.ComponentCreatureModel.MovementAnimationPhase * (float)Math.PI * 2f;
                Vector3 position2 = default;
                position2.X = 0.03f * MathUtils.Sin(num4 * 1f);
                position2.Y = 0.02f * MathUtils.Sin(num4 * 2f);
                position2.Z = 0.02f * MathUtils.Sin(num4 * 1f);
                identity *= Matrix.CreateRotationZ(1f * position2.X) * Matrix.CreateTranslation(position2);
            }

            var eyePosition = _componentPlayer.ComponentCreatureModel.EyePosition;
            var x = Terrain.ToCell(eyePosition.X);
            var num5 = Terrain.ToCell(eyePosition.Y);
            var z = Terrain.ToCell(eyePosition.Z);
            var m = Matrix.CreateFromQuaternion(_componentPlayer.ComponentCreatureModel.EyeRotation);
            m.Translation = _componentPlayer.ComponentCreatureModel.EyePosition;
            if (_value != 0)
            {
                if (num5 is >= 0 and <= 255)
                {
                    var chunkAtCell = _subsystemTerrain.Terrain.GetChunkAtCell(x, z, false);
                    if (chunkAtCell is { MainThreadState: >= TerrainChunkState.InvalidVertices1 })
                    {
                        _itemLight = _subsystemTerrain.Terrain.GetCellLightFast(x, num5, z);
                    }
                }

                var num6 = Terrain.ExtractContents(_value);
                var block = BlocksManager.Blocks[num6];
                var vector = block.GetFirstPersonRotation(_value) * ((float)Math.PI / 180f) + _itemRotation;
                var position3 = block.GetFirstPersonOffset(_value) + _itemOffset;
                position3 += _itemOffset;
                var matrix = Matrix.CreateFromYawPitchRoll(vector.Y, vector.X, vector.Z) * identity *
                             Matrix.CreateTranslation(position3) *
                             Matrix.CreateFromYawPitchRoll(_lagAngles.X, _lagAngles.Y, 0f) * m;
                _drawBlockEnvironmentData.DrawBlockMode = DrawBlockMode.FirstPerson;
                _drawBlockEnvironmentData.SubsystemTerrain = _subsystemTerrain;
                _drawBlockEnvironmentData.InWorldMatrix = matrix;
                _drawBlockEnvironmentData.Light = _itemLight;
                _drawBlockEnvironmentData.Humidity = _subsystemTerrain.Terrain.GetSeasonalHumidity(x, z);
                _drawBlockEnvironmentData.Temperature = _subsystemTerrain.Terrain.GetSeasonalTemperature(x, z) +
                                                        SubsystemWeather.GetTemperatureAdjustmentAtHeight(num5);
                block.DrawBlock(_primitivesRenderer, _value, Color.White, block.GetFirstPersonScale(_value),
                    ref matrix, _drawBlockEnvironmentData);
                _primitivesRenderer.Flush(camera.ViewProjectionMatrix);
            }
            else
            {
                if (Time.FrameStartTime >= _nextHandLightTime)
                {
                    var num7 = LightingManager.CalculateSmoothLight(_subsystemTerrain, eyePosition);
                    if (num7.HasValue)
                    {
                        _nextHandLightTime = Time.FrameStartTime + 0.1;
                        _handLight = num7.Value;
                    }
                }

                var position4 = new Vector3(0.25f, -0.3f, -0.05f);
                var matrix2 = Matrix.CreateScale(0.01f) * Matrix.CreateRotationX(0.8f) *
                              Matrix.CreateRotationY(0.4f) * identity * Matrix.CreateTranslation(position4) *
                              Matrix.CreateFromYawPitchRoll(_lagAngles.X, _lagAngles.Y, 0f) * m *
                              camera.ViewMatrix;
                Display.DepthStencilState = DepthStencilState.Default;
                Display.RasterizerState = RasterizerState.CullCounterClockwiseScissor;
                if (_componentPlayer.ComponentCreatureModel.TextureOverride != null)
                {
                    LitShader.Texture = _componentPlayer.ComponentCreatureModel.TextureOverride;
                }

                LitShader.SamplerState = SamplerState.PointClamp;
                LitShader.MaterialColor = Vector4.One;
                LitShader.AmbientLightColor = new Vector3(_handLight * LightingManager.LightAmbient);
                LitShader.DiffuseLightColor1 = new Vector3(_handLight);
                LitShader.DiffuseLightColor2 = new Vector3(_handLight);
                LitShader.LightDirection1 =
                    Vector3.TransformNormal(LightingManager.DirectionToLight1, camera.ViewMatrix);
                LitShader.LightDirection2 =
                    Vector3.TransformNormal(LightingManager.DirectionToLight2, camera.ViewMatrix);
                LitShader.Transforms.World[0] = matrix2;
                LitShader.Transforms.View = Matrix.Identity;
                LitShader.Transforms.Projection = camera.ProjectionMatrix;
                foreach (var meshPart in _handModel.Meshes.SelectMany(mesh => mesh.MeshParts))
                {
                    Display.DrawIndexed(
                        PrimitiveType.TriangleList,
                        LitShader,
                        meshPart.VertexBuffer,
                        meshPart.IndexBuffer,
                        meshPart.StartIndex,
                        meshPart.IndicesCount
                    );
                }
            }
        }
        finally
        {
            Display.Viewport = viewport;
        }
    }

    public UpdateOrder UpdateOrder => UpdateOrder.FirstPersonModels;

    public void Update(float dt)
    {
        if (RunMode.Value is RunModeType.HeadlessServer)
        {
            return;
        }

        var vector = _componentPlayer.ComponentCreatureModel.EyeRotation.ToYawPitchRoll();
        _lagAngles *= MathUtils.Pow(0.2f, dt);
        if (_lastYpr.HasValue)
        {
            var vector2 = vector - _lastYpr.Value;
            _lagAngles.X = MathUtils.Clamp(_lagAngles.X - 0.08f * MathUtils.NormalizeAngle(vector2.X), -0.1f, 0.1f);
            _lagAngles.Y = MathUtils.Clamp(_lagAngles.Y - 0.08f * MathUtils.NormalizeAngle(vector2.Y), -0.1f, 0.1f);
        }

        _lastYpr = vector;
        var activeBlockValue = _componentMiner.ActiveBlockValue;
        if (_swapAnimationTime == 0f && activeBlockValue != _value)
        {
            if (BlocksManager.Blocks[Terrain.ExtractContents(activeBlockValue)]
                .IsSwapAnimationNeeded(_value, activeBlockValue))
            {
                _swapAnimationTime = 0.0001f;
            }
            else
            {
                _value = activeBlockValue;
            }
        }

        if (_swapAnimationTime > 0f)
        {
            var swapAnimationTime = _swapAnimationTime;
            _swapAnimationTime += 2f * dt;
            if (swapAnimationTime < 0.5f && _swapAnimationTime >= 0.5f)
            {
                _value = activeBlockValue;
            }

            if (_swapAnimationTime > 1f)
            {
                _swapAnimationTime = 0f;
            }
        }

        _pokeAnimationTime = _componentMiner.PokingPhase;
        _itemOffset = Vector3.Lerp(_itemOffset, ItemOffsetOrder, MathUtils.Saturate(10f * dt));
        _itemRotation = Vector3.Lerp(_itemRotation, ItemRotationOrder, MathUtils.Saturate(10f * dt));
        ItemOffsetOrder = Vector3.Zero;
        ItemRotationOrder = Vector3.Zero;
    }

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        if (RunMode.Value is RunModeType.HeadlessServer)
        {
            return;
        }

        _subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true)!;
        _componentPlayer = Entity.FindComponent<ComponentPlayer>(true)!;
        _componentRider = Entity.FindComponent<ComponentRider>(true)!;
        _componentMiner = Entity.FindComponent<ComponentMiner>(true)!;
        _handModel = ContentManager.Get<Model>(valuesDictionary.GetValue<string>("HandModelName"));
    }
}
