using Engine.Graphics;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Modding.Blocks;

namespace Game.Components;

public class ComponentVrHandsModel : Component, IDrawable, IUpdateable
{
    public UpdateExecutionSide UpdateSide => UpdateExecutionSide.Presentation;

    private static LitShader? _shader;

    private static readonly int[] _drawOrders = [1];

    private ComponentMiner _componentMiner = null!;

    private ComponentCharacter _componentCharacter = null!;

    private BlockPresentationCatalog _blockPresentations = null!;

    private readonly DrawBlockEnvironmentData _drawBlockEnvironmentData = new();

    private float _handLight;

    private int _itemLight;

    private Vector3 _itemOffset;

    private Vector3 _itemRotation;

    private double _nextHandLightTime;

    private float _pokeAnimationTime;

    private PrimitivesRenderer3D PrimitivesRenderer => field ??= new PrimitivesRenderer3D();

    private SubsystemTerrain _subsystemTerrain = null!;

    private Model _vrHandModel = null!;

    public Vector3 ItemOffsetOrder { get; set; }

    public Vector3 ItemRotationOrder { get; set; }

    public int[] DrawOrders => _drawOrders;

    private static LitShader Shader => _shader ??= new LitShader(2, false, false, true, false, false);

    public void Draw(Camera camera, int drawOrder)
    {
        if (!WorldRuntimeContext.For(Project).HasPresentation) { return; }

        if (!(_componentCharacter.ComponentHealth.Health > 0f) ||
            !camera.GameWidget.IsEntityFirstPersonTarget(Entity) ||
            !_componentCharacter.Entity.FindComponent<ComponentInput>(true)!.IsControlledByVr)
        {
            return;
        }

        var eyePosition = _componentCharacter.ComponentCreaturePose.EyePosition;
        var x = Terrain.ToCell(eyePosition.X);
        var num = Terrain.ToCell(eyePosition.Y);
        var z = Terrain.ToCell(eyePosition.Z);
        var activeBlockValue = _componentMiner.ActiveBlockValue;
        if (Time.FrameStartTime >= _nextHandLightTime)
        {
            var num2 = LightingManager.CalculateSmoothLight(_subsystemTerrain, eyePosition);
            if (num2.HasValue)
            {
                _nextHandLightTime = Time.FrameStartTime + 0.1;
                _handLight = num2.Value;
            }
        }

        var identity = Matrix.Identity;
        if (_pokeAnimationTime > 0f)
        {
            var num3 = MathUtils.Sin(MathUtils.Sqrt(_pokeAnimationTime) * (float)Math.PI);
            if (activeBlockValue != 0)
            {
                identity *= Matrix.CreateRotationX((0f - MathUtils.DegToRad(90f)) * num3);
            }
            else
            {
                identity *= Matrix.CreateRotationX((0f - MathUtils.DegToRad(45f)) * num3);
            }
        }

        if (!VrManager.IsControllerPresent(VrController.Right))
        {
            return;
        }

        var m = VrManager.HmdMatrixInverted *
                Matrix.CreateWorld(camera.ViewPosition, camera.ViewDirection, camera.ViewUp) *
                camera.ViewMatrix;
        var controllerMatrix = VrManager.GetControllerMatrix(VrController.Right);
        if (activeBlockValue == 0)
        {
            Display.DepthStencilState = DepthStencilState.Default;
            Display.RasterizerState = RasterizerState.CullCounterClockwiseScissor;
            if (_componentCharacter.Entity.FindComponent<ComponentCreatureModel>(true)!.TextureOverride is
                { } skinTexture)
            {
                Shader.Texture = skinTexture;
            }

            Shader.SamplerState = SamplerState.PointClamp;
            Shader.MaterialColor = Vector4.One;
            Shader.AmbientLightColor = new Vector3(_handLight * LightingManager.LightAmbient);
            Shader.DiffuseLightColor1 = new Vector3(_handLight);
            Shader.DiffuseLightColor2 = new Vector3(_handLight);
            Shader.LightDirection1 = -Vector3.TransformNormal(LightingManager.DirectionToLight1, camera.ViewMatrix);
            Shader.LightDirection2 = -Vector3.TransformNormal(LightingManager.DirectionToLight2, camera.ViewMatrix);
            Shader.Transforms.View = Matrix.Identity;
            Shader.Transforms.Projection = camera.ProjectionMatrix;
            Shader.Transforms.World[0] = Matrix.CreateScale(0.01f) * identity * controllerMatrix * m;
            foreach (var meshPart in _vrHandModel.MeshNodes.SelectMany(node => node.RequireMesh().Primitives))
            {
                Display.DrawIndexed(
                    PrimitiveType.TriangleList,
                    Shader,
                    meshPart.VertexBuffer,
                    meshPart.IndexBuffer,
                    meshPart.StartIndex,
                    meshPart.IndicesCount
                );
            }
        }
        else
        {
            if (num is >= 0 and <= 255)
            {
                var chunkAtCell = _subsystemTerrain.Terrain.GetChunkAtCell(x, z, false);
                if (chunkAtCell is { MainThreadState: >= TerrainChunkState.Ready })
                {
                    _itemLight = _subsystemTerrain.Terrain.GetCellLightFast(x, num, z);
                }
            }

            var num4 = Terrain.ExtractContents(activeBlockValue);
            var block = _blockPresentations.Get(num4);
            var vector = block.InHandRotation * ((float)Math.PI / 180f) + _itemRotation;
            var matrix = Matrix.CreateFromYawPitchRoll(vector.Y, vector.X, vector.Z) *
                         Matrix.CreateTranslation(block.InHandOffset) *
                         identity *
                         Matrix.CreateTranslation(_itemOffset) *
                         controllerMatrix *
                         m;
            _drawBlockEnvironmentData.SubsystemTerrain = _subsystemTerrain;
            _drawBlockEnvironmentData.InWorldMatrix = matrix;
            _drawBlockEnvironmentData.Light = _itemLight;
            _drawBlockEnvironmentData.Humidity = _subsystemTerrain.Terrain.GetHumidity(x, z);
            _drawBlockEnvironmentData.Temperature = _subsystemTerrain.Terrain.GetTemperature(x, z) +
                                                    SubsystemWeather.GetTemperatureAdjustmentAtHeight(num);
            block.DrawBlock(
                PrimitivesRenderer,
                activeBlockValue,
                Color.White,
                block.InHandScale,
                ref matrix,
                _drawBlockEnvironmentData
            );
        }

        PrimitivesRenderer.Flush(camera.ProjectionMatrix);
    }

    public UpdateOrder UpdateOrder => UpdateOrder.FirstPersonModels;

    public void Update(float dt)
    {
        _pokeAnimationTime = _componentMiner.PokingPhase;
        _itemOffset = Vector3.Lerp(_itemOffset, ItemOffsetOrder, MathUtils.Saturate(10f * dt));
        _itemRotation = Vector3.Lerp(_itemRotation, ItemRotationOrder, MathUtils.Saturate(10f * dt));
        ItemOffsetOrder = Vector3.Zero;
        ItemRotationOrder = Vector3.Zero;
    }

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        if (!WorldRuntimeContext.For(Project).HasPresentation) { return; }

        _subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true)!;
        _blockPresentations = Project.FindSubsystem<SubsystemBlockPresentation>(true)!.Catalog;
        _componentCharacter = Entity.FindComponent<ComponentCharacter>(true)!;
        _componentMiner = Entity.FindComponent<ComponentMiner>(true)!;
        _vrHandModel = ContentManager.Get<Model>(valuesDictionary.GetValue<string>("VrHandModelName"));
    }
}
