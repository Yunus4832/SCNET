using System.Globalization;

using Engine.Graphics;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Subsystems;

public class SubsystemSkyPresentation : Subsystem, IDrawable, IUpdateable
{
    public UpdateExecutionSide UpdateSide => UpdateExecutionSide.Presentation;

    public const int StarsCount = 250;

    private UnlitShader _shaderFlat = null!;

    private UnlitShader _shaderTextured = null!;

    public SkyShader? Shader;

    public SkyShader? ShaderAlphaTest;

    public bool DrawGalaxyEnabled = true;

    public bool DrawCloudsWireframe;

    public bool DrawSkyEnabled = true;

    private RenderTarget2D? _skyBackground;

    public Texture2D? FogBackground => FogEnabled &&
                                       _viewIsSkyVisible &&
                                       DrawSkyEnabled &&
                                       Configuration.ClientConfiguration.Preferences.SkyRenderingMode !=
                                       SkyRenderingMode.Disabled
        ? _skyBackground
        : null;

    public bool FogEnabled = true;

    private readonly Color[] _cloudsLayerColors = new Color[5];

    private readonly float[] _cloudsLayerRadii =
    [
        0f,
        0.8f,
        0.95f,
        1f
    ];

    private Texture2D _cloudsTexture = null!;

    private readonly int[] _drawOrders =
    [
        -100,
        5,
        105
    ];

    private readonly Random _fogSeedRandom = new();

    private Texture2D _glowTexture = null!;

    private float _lightningStrikeBrightness;

    private Vector3? _lightningStrikePosition;

    private readonly Texture2D[] _moonTextures = new Texture2D[8];

    private SkyPrimitiveRender _primitiveRender = null!;

    private PrimitivesRenderer2D PrimitivesRenderer2D => field ??= new PrimitivesRenderer2D();

    private PrimitivesRenderer3D PrimitivesRenderer3D => field ??= new PrimitivesRenderer3D();

    private readonly Random _random = new();

    private readonly Dictionary<GameWidget, SkyDome> _skyDomes = new();

    private readonly VertexDeclaration _skyVertexDeclaration = new(
        new VertexElement(0, VertexElementFormat.Vector3, VertexElementSemantic.Position),
        new VertexElement(12, VertexElementFormat.NormalizedByte4, VertexElementSemantic.Color)
    );

    private IndexBuffer? _starsIndexBuffer;

    private VertexBuffer? _starsVertexBuffer;

    private readonly VertexDeclaration _starsVertexDeclaration = new(
        new VertexElement(0, VertexElementFormat.Vector3, VertexElementSemantic.Position),
        new VertexElement(12, VertexElementFormat.Vector2, VertexElementSemantic.TextureCoordinate),
        new VertexElement(20, VertexElementFormat.NormalizedByte4, VertexElementSemantic.Color)
    );

    private SubsystemAudio _subsystemAudio = null!;

    private SubsystemFluidBlockBehavior _subsystemFluidBlockBehavior = null!;

    private SubsystemGameInfo _subsystemGameInfo = null!;

    private SubsystemTerrain _subsystemTerrain = null!;

    private SubsystemTime _subsystemTime = null!;

    private SubsystemTimeOfDay _subsystemTimeOfDay = null!;

    private SubsystemWeather _subsystemWeather = null!;

    private Texture2D _sunTexture = null!;

    private Color _viewFogColor;

    private Vector2 _viewFogRange;

    private bool _viewIsSkyVisible;

    public float ViewFogBottom { get; private set; }

    public float ViewFogTop { get; private set; }

    internal Vector2 ViewVisibilityStartEnd { get; set; } = new(float.MaxValue);

    public float ViewHazeStart { get; private set; }

    public float ViewHazeDensity { get; private set; }

    public float ViewFogDensity { get; private set; }

    public float VisibilityRange { get; set; }

    public float VisibilityRangeYMultiplier { get; set; }

    public float ViewUnderWaterDepth { get; set; }

    public float ViewUnderMagmaDepth { get; set; }

    public Color ViewFogColor => _viewFogColor;

    public Vector2 ViewFogRange => _viewFogRange;

    public int[] DrawOrders => _drawOrders;

    private SubsystemSky _sky = null!;

    public override void Load(ValuesDictionary valuesDictionary)
    {
        _sky = Project.FindSubsystem<SubsystemSky>(true)!;
        _subsystemTimeOfDay = Project.FindSubsystem<SubsystemTimeOfDay>(true)!;
        _subsystemTime = Project.FindSubsystem<SubsystemTime>(true)!;
        _subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true)!;
        _subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true)!;
        _subsystemWeather = Project.FindSubsystem<SubsystemWeather>(true)!;
        _subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true)!;
        _subsystemFluidBlockBehavior = Project.FindSubsystem<SubsystemFluidBlockBehavior>(true)!;
        _sunTexture = ContentManager.Get<Texture2D>("Textures/Sun");
        _glowTexture = ContentManager.Get<Texture2D>("Textures/SkyGlow");
        _cloudsTexture = ContentManager.Get<Texture2D>("Textures/Clouds");
        _primitiveRender = new SkyPrimitiveRender();
        for (var i = 0; i < 8; i++)
        {
            _moonTextures[i] =
                ContentManager.Get<Texture2D>("Textures/Moon" + (i + 1).ToString(CultureInfo.InvariantCulture));
        }

        VisibilityRange = Configuration.ClientConfiguration.Preferences.VisibilityRange;
        _sky.LightningStrike += OnLightningStrike;
        Display.DeviceReset += DisplayDeviceReset;
        _shaderFlat = new UnlitShader(true, false, true, false);
        _shaderTextured = new UnlitShader(true, true, false, false);
    }

    public void Update(float dt)
    {
        VisibilityRange = Configuration.ClientConfiguration.Preferences.VisibilityRange;
        if (!_lightningStrikePosition.HasValue) { return; }

        var num9 = MathUtils.Lerp(
            0.3f,
            0.75f,
            0.5f * (float)MathUtils.Sin(MathUtils.Remainder(1.0 * _subsystemTime.GameTime, 6.2831854820251465)) +
            0.5f
        );
        _lightningStrikeBrightness -= _subsystemTime.GameTimeDelta / num9;
        if (_lightningStrikeBrightness <= 0f)
        {
            _lightningStrikePosition = null;
            _lightningStrikeBrightness = 0f;
        }
    }

    private void OnLightningStrike(Vector3 targetPosition)
    {
        _lightningStrikePosition = targetPosition;
        _lightningStrikeBrightness = 1f;
        var num = float.MaxValue;
        foreach (var listenerPosition in _subsystemAudio.ListenerPositions)
        {
            var num2 = Vector2.Distance(
                new Vector2(listenerPosition.X, listenerPosition.Z),
                new Vector2(targetPosition.X, targetPosition.Z)
            );
            if (num2 < num)
            {
                num = num2;
            }
        }

        var delay = _subsystemAudio.CalculateDelay(num);
        if (num < 40f)
        {
            _subsystemAudio.PlayRandomSound("Audio/ThunderNear", 1f, _random.Float(-0.2f, 0.2f), 0f, delay);
        }
        else if (num < 200f)
        {
            _subsystemAudio.PlayRandomSound("Audio/ThunderFar", 0.8f, _random.Float(-0.2f, 0.2f), 0f, delay);
        }
    }

    public void Draw(Camera camera, int drawOrder)
    {
        if (!WorldRuntimeContext.For(Project).HasPresentation)
        {
            return;
        }

        if (drawOrder == _drawOrders[0])
        {
            ViewVisibilityStartEnd = new Vector2(VisibilityRange * 0.8f, VisibilityRange);
            ViewUnderWaterDepth = 0f;
            ViewUnderMagmaDepth = 0f;
            var viewPosition = camera.ViewPosition;
            var x = Terrain.ToCell(viewPosition.X);
            var y = Terrain.ToCell(viewPosition.Y);
            var z = Terrain.ToCell(viewPosition.Z);
            var surfaceHeight = _subsystemFluidBlockBehavior.GetSurfaceHeight(x, y, z, out var surfaceFluidBlock);
            if (surfaceHeight.HasValue)
            {
                if (surfaceFluidBlock is WaterBlock)
                {
                    ViewUnderWaterDepth = surfaceHeight.Value + 0.1f - viewPosition.Y;
                }
                else if (surfaceFluidBlock is MagmaBlock)
                {
                    ViewUnderMagmaDepth = surfaceHeight.Value + 1f - viewPosition.Y;
                }
            }

            if (ViewUnderWaterDepth > 0f)
            {
                var seasonalHumidity = _subsystemTerrain.Terrain.GetSeasonalHumidity(x, z);
                var temperature = _subsystemTerrain.Terrain.GetSeasonalTemperature(x, z) +
                                  SubsystemWeather.GetTemperatureAdjustmentAtHeight(y);
                var c = BlockColorsMap.WaterColorsMap.Lookup(temperature, seasonalHumidity);
                var num = MathUtils.Lerp(1f, 0.5f, seasonalHumidity / 15f);
                var num2 = MathUtils.Lerp(1f, 0.2f, MathUtils.Saturate(0.075f * (ViewUnderWaterDepth - 2f)));
                var num3 = MathUtils.Lerp(0.33f, 1f, _sky.SkyLightIntensity);
                ViewHazeStart = 10f;
                ViewHazeDensity = MathUtils.Lerp(0.25f, 0.1f, num * num2 * num3);
                ViewFogDensity = 0f;
                ViewFogBottom = 0f;
                ViewFogTop = 1f;
                _viewFogColor = Color.MultiplyColorOnly(c, 0.66f * num2 * num3); //在水中的视图雾颜色
                VisibilityRangeYMultiplier = 1f;
                _viewIsSkyVisible = false;
            }
            else if (ViewUnderMagmaDepth > 0f)
            {
                ViewHazeStart = 0f;
                ViewHazeDensity = 10f;
                ViewFogDensity = 0f;
                ViewFogBottom = 0f;
                ViewFogTop = 1f;
                _viewFogColor = new Color(255, 80, 0); //在岩浆中的视图雾颜色
                VisibilityRangeYMultiplier = 1f;
                _viewIsSkyVisible = false;
            }
            else
            {
                _fogSeedRandom.Seed(_subsystemWeather.FogSeed);
                var num4 = _fogSeedRandom.Bool(0.66f)
                    ? _fogSeedRandom.Float(62f, 82f)
                    : _fogSeedRandom.Float(62f, 180f);
                var x2 = MathUtils.Clamp(num4 + _fogSeedRandom.Float(-20f, 20f), 62f, 180f);
                var num5 = _fogSeedRandom.Bool(0.66f)
                    ? _fogSeedRandom.Float(12f, 22f)
                    : _fogSeedRandom.Float(12f, 80f);
                ViewFogBottom = MathUtils.Lerp(num4, x2, _subsystemWeather.FogProgress);

                ViewFogTop = ViewFogBottom + num5;
                ViewFogDensity = MathUtils.Pow(_subsystemWeather.FogIntensity, 2f) *
                                 _fogSeedRandom.Float(0.04f, 0.1f);
                const float num6 = 256f;
                var seasonalTemperature =
                    _subsystemTerrain.Terrain.GetSeasonalTemperature(
                        Terrain.ToCell(viewPosition.X),
                        Terrain.ToCell(viewPosition.Z)
                    );
                // Distance fog covers the visibility boundary independently of weather.
                ViewHazeStart = 0.8f * VisibilityRange;
                ViewHazeDensity = 1f / (VisibilityRange - ViewHazeStart);
                var color = CalculateSkyColor(
                    new Vector3(1f, 0f, 0f),
                    _subsystemTimeOfDay.TimeOfDay,
                    _subsystemWeather.PrecipitationIntensity,
                    seasonalTemperature
                ); //与2.4相比保留了降水效果，但是降水因素不再参与天空颜色计算，此处保留是为了和modmanager兼容
                var color2 = CalculateSkyColor(
                    new Vector3(0f, 0f, 1f),
                    _subsystemTimeOfDay.TimeOfDay,
                    _subsystemWeather.PrecipitationIntensity,
                    seasonalTemperature
                );
                var color3 = CalculateSkyColor(
                    new Vector3(-1f, 0f, 0f),
                    _subsystemTimeOfDay.TimeOfDay,
                    _subsystemWeather.PrecipitationIntensity,
                    seasonalTemperature
                );
                var color4 = CalculateSkyColor(
                    new Vector3(0f, 0f, -1f),
                    _subsystemTimeOfDay.TimeOfDay,
                    _subsystemWeather.PrecipitationIntensity,
                    seasonalTemperature
                );
                var c2 = 0.25f * color + 0.25f * color2 + 0.25f * color3 + 0.25f * color4;
                var c3 = CalculateSkyColor(
                    new Vector3(camera.ViewDirection.X, 0f, camera.ViewDirection.Z),
                    _subsystemTimeOfDay.TimeOfDay,
                    _subsystemWeather.PrecipitationIntensity,
                    seasonalTemperature
                );
                _viewFogColor = Color.Lerp(c3, c2, CalculateSkyFog(camera.ViewPosition));
                VisibilityRangeYMultiplier = VisibilityRange / num6;
                _viewIsSkyVisible = true;
            }

            if (!FogEnabled)
            {
                _viewFogRange = new Vector2(100000f, 100000f);
                ViewHazeDensity = 0f;
                ViewFogDensity = 0f;
            }

            if (DrawSkyEnabled &&
                _viewIsSkyVisible &&
                Configuration.ClientConfiguration.Preferences.SkyRenderingMode != SkyRenderingMode.Disabled)
            {
                CaptureSkyBackground(camera);
                return;
            }

            var flatBatch2D = PrimitivesRenderer2D.FlatBatch(
                -1,
                DepthStencilState.None,
                RasterizerState.CullNoneScissor,
                BlendState.Opaque
            );
            var count = flatBatch2D.TriangleVertices.Count;
            flatBatch2D.QueueQuad(Vector2.Zero, camera.ViewportSize, 0f, _viewFogColor);
            flatBatch2D.TransformTriangles(camera.ViewportMatrix, count);
            PrimitivesRenderer2D.Flush();
        }
        else if (drawOrder == _drawOrders[1])
        {
            if (!DrawSkyEnabled ||
                !_viewIsSkyVisible ||
                Configuration.ClientConfiguration.Preferences.SkyRenderingMode == SkyRenderingMode.Disabled)
            {
                return;
            }

            if (_skyBackground != null)
            {
                var batch = PrimitivesRenderer2D.TexturedBatch(
                    _skyBackground,
                    false,
                    0,
                    DepthStencilState.DepthRead,
                    RasterizerState.CullNoneScissor,
                    BlendState.Opaque,
                    SamplerState.PointClamp
                );
                batch.QueueQuad(
                    Vector2.Zero,
                    new Vector2(Display.Viewport.Width, Display.Viewport.Height),
                    1f,
                    Vector2.Zero,
                    Vector2.One,
                    Color.White
                );
                PrimitivesRenderer2D.Flush();
            }

            DrawSkyObjects(camera);
        }
        else
        {
            DrawLightning(camera);
            PrimitivesRenderer3D.Flush(camera.ViewProjectionMatrix);
        }
    }

    private void CaptureSkyBackground(Camera camera)
    {
        var target = Display.RenderTarget;
        var viewport = Display.Viewport;
        var scissor = Display.ScissorRectangle;
        _skyBackground = null;
        if (viewport.Width <= 0 || viewport.Height <= 0) { return; }

        var sky = GetSkyDome(camera);
        if (sky.Background == null ||
            sky.Background.Width != viewport.Width ||
            sky.Background.Height != viewport.Height)
        {
            sky.Background?.Dispose();
            sky.Background = null;
            sky.Background = new RenderTarget2D(
                viewport.Width,
                viewport.Height,
                1,
                ColorFormat.Rgba8888,
                DepthFormat.None
            );
        }

        try
        {
            Display.RenderTarget = sky.Background;
            Display.Clear(new Vector4(ViewFogColor));
            // Fog samples atmospheric radiance. Sky objects use the scene depth buffer.
            DrawSkydome(camera);
            _skyBackground = sky.Background;
        }
        finally
        {
            Display.RenderTarget = target;
            Display.Viewport = viewport;
            Display.ScissorRectangle = scissor;
        }
    }

    private void DrawSkyObjects(Camera camera)
    {
        if (DrawGalaxyEnabled)
        {
            DrawStars(camera);
            DrawSunAndMoon(camera);
        }

        DrawClouds(camera);
        if (Shader != null && ShaderAlphaTest != null)
        {
            if (_primitiveRender.Shader == null && _primitiveRender.ShaderAlphaTest == null)
            {
                _primitiveRender.Shader = Shader;
                _primitiveRender.ShaderAlphaTest = ShaderAlphaTest;
                _primitiveRender.Camera = camera;
            }

            _primitiveRender.Flush(PrimitivesRenderer3D, camera.ViewProjectionMatrix);
        }
        else
        {
            PrimitivesRenderer3D.Flush(camera.ViewProjectionMatrix);
        }
    }

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    internal ViewAtmosphere CaptureAtmosphere(Vector3 viewPosition)
    {
        return new ViewAtmosphere(
            new Vector2(ViewHazeStart, ViewHazeDensity),
            new Vector3(ViewFogBottom - viewPosition.Y, ViewFogTop - viewPosition.Y, ViewFogDensity),
            VisibilityRangeYMultiplier,
            ViewVisibilityStartEnd
        );
    }

    internal void BindAtmosphere(Shader shader, Camera camera)
    {
        var atmosphere = CaptureAtmosphere(camera.ViewPosition);
        shader.GetParameter("u_fogColor").SetValue(new Vector3(ViewFogColor));
        shader.GetParameter("u_fogBottomTopDensity").SetValue(atmosphere.WeatherBottomTopDensity);
        shader.GetParameter("u_hazeStartDensity").SetValue(atmosphere.DistanceStartDensity);
        shader.GetParameter("u_fogYMultiplier").SetValue(atmosphere.VerticalDistanceScale);
        shader.GetParameter("u_visibilityStartEnd").SetValue(atmosphere.VisibilityStartEnd);
        shader.GetParameter("u_fogColorContribution").SetValue(1f);
        shader.GetParameter("u_skyFogEnabled").SetValue(FogBackground != null ? 1f : 0f);
        shader.GetParameter("u_skyTexture").SetValue(FogBackground ?? _cloudsTexture);
        shader.GetParameter("u_skySampler").SetValue(SamplerState.PointClamp);
    }

    public float CalculateFog(Vector3 viewPosition, Vector3 position)
    {
        return CaptureAtmosphere(viewPosition).GetOpacity(position - viewPosition);
    }

    public float CalculateFogNoHaze(Vector3 viewPosition, Vector3 position)
    {
        return CaptureAtmosphere(viewPosition).GetWeatherOpacity(position - viewPosition);
    }

    public float CalculateHazeFactor()
    {
        return MathUtils.Saturate(_subsystemWeather.PrecipitationIntensity + 30f * ViewFogDensity);
    }

    public float CalculateSkyFog(Vector3 viewPosition)
    {
        return CalculateFogNoHaze(viewPosition, viewPosition + new Vector3(1000f, 150f, 0f));
    }

    public override void Dispose()
    {
        if (!WorldRuntimeContext.For(Project).HasPresentation)
        {
            return;
        }

        if (_sky is not null) { _sky.LightningStrike -= OnLightningStrike; }

        Display.DeviceReset -= DisplayDeviceReset;
        _skyBackground = null;
        _shaderFlat?.Dispose();
        _shaderTextured?.Dispose();
        _starsVertexBuffer?.Dispose();
        _starsVertexBuffer = null;
        _starsIndexBuffer?.Dispose();
        _starsIndexBuffer = null;
        foreach (var value in _skyDomes.Values)
        {
            value.Dispose();
        }

        _skyDomes.Clear();
    }

    public void DisplayDeviceReset()
    {
        _skyBackground = null;
        _starsVertexBuffer?.Dispose();
        _starsVertexBuffer = null;
        _starsIndexBuffer?.Dispose();
        _starsIndexBuffer = null;
        foreach (var value in _skyDomes.Values)
        {
            value.Dispose();
        }

        _skyDomes.Clear();
    }

    private SkyDome GetSkyDome(Camera camera)
    {
        if (!_skyDomes.TryGetValue(camera.GameWidget, out var sky))
        {
            sky = new SkyDome();
            _skyDomes.Add(camera.GameWidget, sky);
        }

        return sky;
    }

    public void DrawSkydome(Camera camera)
    {
        var value = GetSkyDome(camera);

        if (value.VertexBuffer == null || value.IndexBuffer == null)
        {
            value.VertexBuffer?.Dispose();
            value.VertexBuffer = null;
            value.IndexBuffer?.Dispose();
            value.IndexBuffer = null;
            value.VertexBuffer = new VertexBuffer(_skyVertexDeclaration, value.Vertices.Length);
            value.IndexBuffer = new IndexBuffer(IndexFormat.SixteenBits, value.Indices.Length);
            FillSkyIndexBuffer(value);
            value.LastUpdateTimeOfDay = null;
        }

        var x = Terrain.ToCell(camera.ViewPosition.X);
        var z = Terrain.ToCell(camera.ViewPosition.Z);
        var precipitationIntensity = _subsystemWeather.PrecipitationIntensity;
        var timeOfDay = _subsystemTimeOfDay.TimeOfDay;
        var seasonalTemperature = _subsystemTerrain.Terrain.GetSeasonalTemperature(x, z);
        var flag = true;
        if (value.LastUpdateTimeOfDay.HasValue &&
            !(MathUtils.Abs(timeOfDay - value.LastUpdateTimeOfDay.Value) > 0.0005f) &&
            value.LastUpdatePrecipitationIntensity.HasValue &&
            !(MathUtils.Abs(precipitationIntensity - value.LastUpdatePrecipitationIntensity.Value) > 0.02f) &&
            ((precipitationIntensity != 0f &&
              precipitationIntensity.UncloseTo(1f)) ||
             value.LastUpdatePrecipitationIntensity.Value.CloseTo(precipitationIntensity)) &&
            _lightningStrikeBrightness.CloseTo(value.LastUpdateLightningStrikeBrightness) &&
            value.LastUpdateTemperature.HasValue)
        {
            var lastUpdateTemperature = value.LastUpdateTemperature;
            if (seasonalTemperature == lastUpdateTemperature.GetValueOrDefault() &&
                lastUpdateTemperature.HasValue &&
                value.LastUpdateTemperature.HasValue &&
                !(MathUtils.Abs(ViewFogDensity - value.LastUpdateFogDensity!.Value) > 0.002f))
            {
                flag = false;
            }
        }

        if (flag)
        {
            value.LastUpdateTimeOfDay = timeOfDay;
            value.LastUpdatePrecipitationIntensity = precipitationIntensity;
            value.LastUpdateLightningStrikeBrightness = _lightningStrikeBrightness;
            value.LastUpdateTemperature = seasonalTemperature;
            value.LastUpdateFogDensity = ViewFogDensity;
            FillSkyVertexBuffer(value, timeOfDay, precipitationIntensity, seasonalTemperature);
        }

        Display.DepthStencilState = DepthStencilState.DepthRead;
        Display.RasterizerState = RasterizerState.CullNoneScissor;
        var num = CalculateSkyFog(camera.ViewPosition);
        Display.BlendState = BlendState.Opaque;
        _shaderFlat.Transforms.World[0] = Matrix.CreateTranslation(camera.ViewPosition) * camera.ViewProjectionMatrix;
        _shaderFlat.Color = new Vector4(1f - num);
        _shaderFlat.AdditiveColor = num * new Vector4(ViewFogColor);
        Display.DrawIndexed(
            PrimitiveType.TriangleList,
            _shaderFlat,
            value.VertexBuffer,
            value.IndexBuffer,
            0,
            value.IndexBuffer.IndicesCount
        );
    }

    private void DrawStars(Camera camera)
    {
        var precipitationIntensity = _subsystemWeather.PrecipitationIntensity;
        var timeOfDay = _subsystemTimeOfDay.TimeOfDay;
        if (_starsVertexBuffer == null || _starsIndexBuffer == null)
        {
            _starsVertexBuffer?.Dispose();
            _starsVertexBuffer = null;
            _starsIndexBuffer?.Dispose();
            _starsIndexBuffer = null;
            _starsVertexBuffer = new VertexBuffer(_starsVertexDeclaration, 1000);
            _starsIndexBuffer = new IndexBuffer(IndexFormat.SixteenBits, 1500);
            FillStarsBuffers();
        }

        Display.DepthStencilState = DepthStencilState.DepthRead;
        Display.RasterizerState = RasterizerState.CullNoneScissor;
        var num = MathUtils.Sqr((1f - _sky.CalculateLightIntensity(timeOfDay)) * (1f - precipitationIntensity));
        num *= 1f - CalculateSkyFog(camera.ViewPosition);
        if (!(num > 0.01f))
        {
            return;
        }

        Display.BlendState = BlendState.PremultipliedAdditive;
        _shaderTextured!.Transforms.World[0] = Matrix.CreateRotationZ(-2f * timeOfDay * (float)Math.PI) *
                                               Matrix.CreateRotationX(CalculateSeasonAngle()) *
                                               Matrix.CreateTranslation(camera.ViewPosition) *
                                               camera.ViewProjectionMatrix;

        _shaderTextured.Color = new Vector4(num);
        _shaderTextured.Texture = ContentManager.Get<Texture2D>("Textures/Star");
        _shaderTextured.SamplerState = SamplerState.LinearClamp;
        Display.DrawIndexed(
            PrimitiveType.TriangleList,
            _shaderTextured,
            _starsVertexBuffer,
            _starsIndexBuffer,
            0,
            _starsIndexBuffer.IndicesCount
        );
    }

    public void DrawSunAndMoon(Camera camera)
    {
        var precipitationIntensity = _subsystemWeather.PrecipitationIntensity;
        var timeOfDay = _subsystemTimeOfDay.TimeOfDay;
        var f = MathUtils.Max(CalculateDawnGlowIntensity(timeOfDay), CalculateDuskGlowIntensity(timeOfDay));
        //float num = 2f * timeOfDay * (float)Math.PI;
        var num = (float)Math.PI * 2f * (timeOfDay - _subsystemTimeOfDay.Midday);
        var angle = num + (float)Math.PI;
        var num2 = MathUtils.Lerp(90f, 160f, f);
        var num3 = MathUtils.Lerp(60f, 80f, f);
        var color = Color.Lerp(new Color(255, 255, 255), new Color(255, 255, 160), f);
        var white = Color.White;
        white *= 1f - _sky.SkyLightIntensity;
        color *= MathUtils.Lerp(1f, 0f, precipitationIntensity);
        white *= MathUtils.Lerp(1f, 0f, precipitationIntensity);
        var color2 = color * 0.6f * MathUtils.Lerp(1f, 0f, precipitationIntensity);
        var color3 = color * 0.2f * MathUtils.Lerp(1f, 0f, precipitationIntensity);
        var batch = PrimitivesRenderer3D.TexturedBatch(
            _glowTexture,
            false,
            0,
            DepthStencilState.DepthRead,
            null,
            BlendState.PremultipliedAdditive
        );
        var batch2 = PrimitivesRenderer3D.TexturedBatch(
            _sunTexture,
            false,
            1,
            DepthStencilState.DepthRead,
            null,
            BlendState.AlphaBlend
        );
        var batch3 = PrimitivesRenderer3D.TexturedBatch(
            _moonTextures[_sky.MoonPhase],
            false,
            1,
            DepthStencilState.DepthRead,
            null,
            BlendState.AlphaBlend
        );
        QueueCelestialBody(batch, camera.ViewPosition, color2, 900f, 3.5f * num2, num);
        QueueCelestialBody(batch, camera.ViewPosition, color3, 900f, 3.5f * num3, angle);
        QueueCelestialBody(batch2, camera.ViewPosition, color, 900f, num2, num);
        QueueCelestialBody(batch3, camera.ViewPosition, white, 900f, num3, angle);
    }

    public void DrawLightning(Camera camera)
    {
        if (!_lightningStrikePosition.HasValue)
        {
            return;
        }

        var flatBatch3D = PrimitivesRenderer3D.FlatBatch(0, DepthStencilState.DepthRead, null, BlendState.Additive);
        var color0 = (1f - CalculateSkyFog(camera.ViewPosition)) * Color.White;
        var value = _lightningStrikePosition.Value;
        var unitY = Vector3.UnitY;
        var v = Vector3.Normalize(Vector3.Cross(camera.ViewDirection, unitY));
        var viewport = Display.Viewport;
        var num = Vector4.Transform(new Vector4(value, 1f), camera.ViewProjectionMatrix).W *
                  2f /
                  (viewport.Width * camera.ProjectionMatrix.M11);
        for (var i = 0; i < (int)(_lightningStrikeBrightness * 30f); i++)
        {
            var s = _random.NormalFloat(0f, 1f * num);
            var s2 = _random.NormalFloat(0f, 1f * num);
            var v2 = s * v + s2 * unitY;
            var num2 = 260f;
            while (num2 > value.Y)
            {
                var num3 = MathUtils.Hash(
                    (uint)(_lightningStrikePosition.Value.X +
                           100f * _lightningStrikePosition.Value.Z +
                           200f * num2)
                );
                var num4 = MathUtils.Lerp(4f, 10f, (float)(double)(num3 & 0xFF) / 255f);
                float s3 = (num3 & 1) == 0 ? 1 : -1;
                var s4 = MathUtils.Lerp(0.05f, 0.2f, (float)(double)((num3 >> 8) & 0xFF) / 255f);
                var num5 = num2;
                var num6 = num5 - num4 * MathUtils.Lerp(0.45f, 0.55f, (float)(double)((num3 >> 16) & 0xFF) / 255f);
                var num7 = num5 - num4 * MathUtils.Lerp(0.45f, 0.55f, (float)(double)((num3 >> 24) & 0xFF) / 255f);
                var num8 = num5 - num4;
                var p = new Vector3(value.X, num5, value.Z) + v2;
                var vector = new Vector3(value.X, num6, value.Z) + v2 - num4 * v * s3 * s4;
                var vector2 = new Vector3(value.X, num7, value.Z) + v2 + num4 * v * s3 * s4;
                var p2 = new Vector3(value.X, num8, value.Z) + v2;
                var color = color0 * 0.2f * MathUtils.Saturate((260f - num5) * 0.2f);
                var color2 = color0 * 0.2f * MathUtils.Saturate((260f - num6) * 0.2f);
                var color3 = color0 * 0.2f * MathUtils.Saturate((260f - num7) * 0.2f);
                var color4 = color0 * 0.2f * MathUtils.Saturate((260f - num8) * 0.2f);
                flatBatch3D.QueueLine(p, vector, color, color2);
                flatBatch3D.QueueLine(vector, vector2, color2, color3);
                flatBatch3D.QueueLine(vector2, p2, color3, color4);
                num2 -= num4;
            }
        }
    }

    public void DrawClouds(Camera camera)
    {
        if (Configuration.ClientConfiguration.Preferences.SkyRenderingMode == SkyRenderingMode.NoClouds)
        {
            return;
        }

        var f = CalculateHazeFactor(); //new 大部分原来的降水参数都被替换成了雾气因素

        var num = MathUtils.Lerp(0.03f, 1f, MathUtils.Sqr(_sky.SkyLightIntensity)) * MathUtils.Lerp(1f, 0.2f, f);
        var f2 = CalculateSkyFog(camera.ViewPosition);
        _cloudsLayerColors[0] = Color.Lerp(Color.White * (num * 0.75f), ViewFogColor, f2); //
        _cloudsLayerColors[1] = Color.Lerp(Color.White * (num * 0.66f), ViewFogColor, f2); //

        _cloudsLayerColors[2] = ViewFogColor;
        _cloudsLayerColors[3] = Color.Transparent;
        var gameTime = _subsystemTime.GameTime;
        var viewPosition = camera.ViewPosition;
        var v = new Vector2(
            (float)MathUtils.Remainder(0.0020000000949949026 * gameTime - viewPosition.X / 1900f * 1.75f, 1.0) +
            viewPosition.X / 1900f * 1.75f,
            (float)MathUtils.Remainder(0.0020000000949949026 * gameTime - viewPosition.Z / 1900f * 1.75f, 1.0) +
            viewPosition.Z / 1900f * 1.75f
        );
        var texturedBatch3D = PrimitivesRenderer3D.TexturedBatch(
            _cloudsTexture,
            false,
            2,
            DepthStencilState.DepthRead,
            null,
            BlendState.AlphaBlend,
            SamplerState.LinearWrap
        );
        var triangleVertices = texturedBatch3D.TriangleVertices;
        var triangleIndices = texturedBatch3D.TriangleIndices;
        var vertexStart = triangleVertices.Count;
        var vertexIndex = vertexStart;
        var indexStart = triangleIndices.Count;
        var index = indexStart;
        triangleVertices.Count += 49;
        triangleIndices.Count += 216;
        for (var i = 0; i < 7; i++)
        {
            for (var j = 0; j < 7; j++)
            {
                var num2 = j - 3;
                var num3 = i - 3;
                var num4 = MathUtils.Max(MathUtils.Abs(num2), MathUtils.Abs(num3));
                var num5 = _cloudsLayerRadii[num4];
                var num6 = num4 > 0 ? num5 / MathUtils.Sqrt(num2 * num2 + num3 * num3) : 0f;
                var num7 = num2 * num6;
                var num8 = num3 * num6;
                var y = MathUtils.Lerp(600f, 60f, num5 * num5);
                var position = new Vector3(viewPosition.X + num7 * 1900f, y, viewPosition.Z + num8 * 1900f);
                var texCoord = new Vector2(position.X, position.Z) / 1900f * 1.75f - v;
                var color = _cloudsLayerColors[num4];
                texturedBatch3D.TriangleVertices.Array[vertexIndex++] =
                    new VertexPositionColorTexture(position, color, texCoord);
                if (j > 0 && i > 0)
                {
                    var num9 = (ushort)(vertexStart + j + i * 7);
                    var num10 = (ushort)(vertexStart + (j - 1) + i * 7);
                    var num11 = (ushort)(vertexStart + (j - 1) + (i - 1) * 7);
                    var num12 = (ushort)(vertexStart + j + (i - 1) * 7);
                    if ((num2 <= 0 && num3 <= 0) || (num2 > 0 && num3 > 0))
                    {
                        texturedBatch3D.TriangleIndices.Array[index++] = num9;
                        texturedBatch3D.TriangleIndices.Array[index++] = num10;
                        texturedBatch3D.TriangleIndices.Array[index++] = num11;
                        texturedBatch3D.TriangleIndices.Array[index++] = num11;
                        texturedBatch3D.TriangleIndices.Array[index++] = num12;
                        texturedBatch3D.TriangleIndices.Array[index++] = num9;
                    }
                    else
                    {
                        texturedBatch3D.TriangleIndices.Array[index++] = num9;
                        texturedBatch3D.TriangleIndices.Array[index++] = num10;
                        texturedBatch3D.TriangleIndices.Array[index++] = num12;
                        texturedBatch3D.TriangleIndices.Array[index++] = num10;
                        texturedBatch3D.TriangleIndices.Array[index++] = num11;
                        texturedBatch3D.TriangleIndices.Array[index++] = num12;
                    }
                }
            }
        }

        ClipCloudsAtNearPlane(camera.ViewFrustum.Near, texturedBatch3D, vertexStart, indexStart);
        _ = DrawCloudsWireframe;
    }

    internal static void ClipCloudsAtNearPlane(Plane plane, TexturedBatch3D batch, int vertexStart, int indexStart)
    {
        // Keep eye-plane vertices out of GPU perspective clipping, retaining interpolated UVs and colors.
        var vertexCount = batch.TriangleVertices.Count - vertexStart;
        var indexCount = batch.TriangleIndices.Count - indexStart;
        Span<VertexPositionColorTexture> vertices = stackalloc VertexPositionColorTexture[vertexCount];
        batch.TriangleVertices.Array.AsSpan(vertexStart, vertexCount).CopyTo(vertices);
        Span<int> indices = stackalloc int[indexCount];
        batch.TriangleIndices.Array.AsSpan(indexStart, indexCount).CopyTo(indices);
        batch.TriangleVertices.Count = vertexStart;
        batch.TriangleIndices.Count = indexStart;
        Span<VertexPositionColorTexture> input = stackalloc VertexPositionColorTexture[3];
        Span<VertexPositionColorTexture> output = stackalloc VertexPositionColorTexture[4];
        for (var i = 0; i < indices.Length; i += 3)
        {
            for (var j = 0; j < 3; j++) { input[j] = vertices[indices[i + j] - vertexStart]; }

            var count = 0;
            var previous = input[2];
            var previousDistance = Vector3.Dot(plane.Normal, previous.Position) + plane.D;
            for (var j = 0; j < 3; j++)
            {
                var current = input[j];
                var distance = Vector3.Dot(plane.Normal, current.Position) + plane.D;
                if ((previousDistance <= 0) != (distance <= 0))
                {
                    var fraction = previousDistance / (previousDistance - distance);
                    output[count++] = new VertexPositionColorTexture(
                        Vector3.Lerp(previous.Position, current.Position, fraction),
                        Color.Lerp(previous.Color, current.Color, fraction),
                        Vector2.Lerp(previous.TexCoord, current.TexCoord, fraction)
                    );
                }

                if (distance <= 0) { output[count++] = current; }

                previous = current;
                previousDistance = distance;
            }

            for (var j = 1; j < count - 1; j++)
            {
                batch.QueueTriangle(
                    output[0].Position,
                    output[j].Position,
                    output[j + 1].Position,
                    output[0].TexCoord,
                    output[j].TexCoord,
                    output[j + 1].TexCoord,
                    output[0].Color,
                    output[j].Color,
                    output[j + 1].Color
                );
            }
        }
    }

    public void QueueCelestialBody(
        TexturedBatch3D batch,
        Vector3 viewPosition,
        Color color,
        float distance,
        float radius,
        float angle
    )
    {
        color *= 1f - CalculateSkyFog(viewPosition); //new 这里方法貌似有大幅度改动，建议先进游戏测试再说。目前暂时不对下面的渲染方法进行改动

        if (color.A > 0)
        {
            //Vector3 vector = default(Vector3);
            //vector.X = 0f - MathUtils.Sin(angle);
            //vector.Y = 0f - MathUtils.Cos(angle);
            //vector.Z = 0f;
            //Vector3 vector2 = vector;
            //Vector3 unitZ = Vector3.UnitZ;
            //Vector3 v = Vector3.Cross(unitZ, vector2);
            //Vector3 p = viewPosition + vector2 * distance - radius * unitZ - radius * v;
            //Vector3 p2 = viewPosition + vector2 * distance + radius * unitZ - radius * v;
            //Vector3 p3 = viewPosition + vector2 * distance + radius * unitZ + radius * v;
            //Vector3 p4 = viewPosition + vector2 * distance - radius * unitZ + radius * v;
            //batch.QueueQuad(p, p2, p3, p4, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f), color);
            var m = Matrix.Identity;
            m *= Matrix.CreateTranslation(0f, distance, 0f);
            m *= Matrix.CreateRotationZ(0f - angle);
            m *= Matrix.CreateRotationX(CalculateSeasonAngle());
            m *= Matrix.CreateTranslation(viewPosition);
            var v = new Vector3(0f - radius, 0f, 0f - radius);
            var v2 = new Vector3(radius, 0f, 0f - radius);
            var v3 = new Vector3(radius, 0f, radius);
            var v4 = new Vector3(0f - radius, 0f, radius);
            Vector3.Transform(ref v, ref m, out v);
            Vector3.Transform(ref v2, ref m, out v2);
            Vector3.Transform(ref v3, ref m, out v3);
            Vector3.Transform(ref v4, ref m, out v4);
            batch.QueueQuad(
                v,
                v2,
                v3,
                v4,
                new Vector2(1f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 0f),
                color
            );
        }
    }

    public float CalculateSeasonAngle()
    {
        return -0.4f -
               0.7f *
               (0.5f -
                0.5f *
                MathUtils.Cos(
                    (_subsystemGameInfo.WorldSettings.TimeOfYear - SeasonCalendar.MidSummer) *
                    2f *
                    (float)Math.PI
                ));
    }

    public Color CalculateSkyColor(Vector3 direction, float timeOfDay, float precipitationIntensity, int temperature)
    {
        //降水因素不再参与天空颜色计算
        var f0 = CalculateHazeFactor();
        direction = Vector3.Normalize(direction);
        var vector = Vector2.Normalize(new Vector2(direction.X, direction.Z));
        var s = _sky.CalculateLightIntensity(timeOfDay);
        var f = MathUtils.Saturate(temperature / 15f); //new
        var v = new Vector3(0.65f, 0.68f, 0.7f) * s;
        var v2 = Vector3.Lerp(new Vector3(0.28f, 0.38f, 0.52f), new Vector3(0.15f, 0.3f, 0.56f), f);
        var v3 = Vector3.Lerp(new Vector3(0.7f, 0.79f, 0.88f), new Vector3(0.64f, 0.77f, 0.91f), f);
        var v4 = Vector3.Lerp(v2, v, f0) * s;
        var v5 = Vector3.Lerp(v3, v, f0) * s;
        var v6 = new Vector3(1f, 0.3f, -0.2f);
        var v7 = new Vector3(1f, 0.3f, -0.2f);
        if (_lightningStrikePosition.HasValue)
        {
            v4 = Vector3.Max(new Vector3(_lightningStrikeBrightness), v4);
        }

        var num = MathUtils.Lerp(CalculateDawnGlowIntensity(timeOfDay), 0f, f0);
        var num2 = MathUtils.Lerp(CalculateDuskGlowIntensity(timeOfDay), 0f, f0);
        var f2 = MathUtils.Saturate((direction.Y - 0.1f) / 0.4f);
        var s2 = num * MathUtils.Sqr(MathUtils.Saturate(0f - vector.X));
        var s3 = num2 * MathUtils.Sqr(MathUtils.Saturate(vector.X));
        var color = new Color(Vector3.Lerp(v5 + v6 * s2 + v7 * s3, v4, f2));
        return color;
    }

    public void FillSkyVertexBuffer(SkyDome skyDome, float timeOfDay, float precipitationIntensity, int temperature)
    {
        for (var i = 0; i < 8; i++)
        {
            var x = (float)Math.PI / 2f * MathUtils.Sqr(i / 7f);
            for (var j = 0; j < 16; j++)
            {
                var num = j + i * 16;
                var x2 = (float)Math.PI * 2f * j / 16f;
                var num2 = 1800f * MathUtils.Cos(x);
                skyDome.Vertices[num].Position.X = num2 * MathUtils.Sin(x2);
                skyDome.Vertices[num].Position.Z = num2 * MathUtils.Cos(x2);
                skyDome.Vertices[num].Position.Y = 1800f * MathUtils.Sin(x) - (i == 0 ? 450f : 0f);
                skyDome.Vertices[num].Color = CalculateSkyColor(
                    skyDome.Vertices[num].Position,
                    timeOfDay,
                    precipitationIntensity,
                    temperature
                );
            }
        }

        skyDome.VertexBuffer?.SetData(skyDome.Vertices, 0, skyDome.Vertices.Length);
    }

    public void FillSkyIndexBuffer(SkyDome skyDome)
    {
        var num = 0;
        for (var i = 0; i < 7; i++)
        {
            for (var j = 0; j < 16; j++)
            {
                var num2 = j;
                var num3 = (j + 1) % 16;
                var num4 = i;
                var num5 = i + 1;
                skyDome.Indices[num++] = (ushort)(num2 + num4 * 16);
                skyDome.Indices[num++] = (ushort)(num3 + num4 * 16);
                skyDome.Indices[num++] = (ushort)(num3 + num5 * 16);
                skyDome.Indices[num++] = (ushort)(num3 + num5 * 16);
                skyDome.Indices[num++] = (ushort)(num2 + num5 * 16);
                skyDome.Indices[num++] = (ushort)(num2 + num4 * 16);
            }
        }

        for (var k = 2; k < 16; k++)
        {
            skyDome.Indices[num++] = 0;
            skyDome.Indices[num++] = (ushort)(k - 1);
            skyDome.Indices[num++] = (ushort)k;
        }

        skyDome.IndexBuffer?.SetData(skyDome.Indices, 0, skyDome.Indices.Length);
    }

    private void FillStarsBuffers()
    {
        var random = new Random(10);
        var array = new StarVertex[1000];
        for (var i = 0; i < 250; i++)
        {
            Vector3 v;
            do
            {
                v = new Vector3(random.Float(-1f, 1f), random.Float(-1f, 1f), random.Float(-1f, 1f));
            } while (v.LengthSquared() > 1f);

            v = Vector3.Normalize(v);
            var num = 9f * random.NormalFloat(1f, 0.1f);
            var w = MathUtils.Saturate(random.NormalFloat(0.6f, 0.4f));
            var color = new Color(new Vector4(random.Float(0.6f, 1f) * w, 0.7f * w, random.Float(0.8f, 1f) * w, w));
            var vector = 900f * v;
            var vector2 = Vector3.Normalize(Vector3.Cross(v.X > v.Y ? Vector3.UnitY : Vector3.UnitX, v));
            var vector3 = Vector3.Normalize(Vector3.Cross(vector2, v));
            var position = vector + num * (-vector2 - vector3);
            var position2 = vector + num * (vector2 - vector3);
            var position3 = vector + num * (vector2 + vector3);
            var position4 = vector + num * (-vector2 + vector3);
            array[i * 4] = new StarVertex
            {
                Position = position,
                TextureCoordinate = new Vector2(0f, 0f),
                Color = color
            };
            array[i * 4 + 1] = new StarVertex
            {
                Position = position2,
                TextureCoordinate = new Vector2(1f, 0f),
                Color = color
            };
            array[i * 4 + 2] = new StarVertex
            {
                Position = position3,
                TextureCoordinate = new Vector2(1f, 1f),
                Color = color
            };
            array[i * 4 + 3] = new StarVertex
            {
                Position = position4,
                TextureCoordinate = new Vector2(0f, 1f),
                Color = color
            };
        }

        _starsVertexBuffer?.SetData(array, 0, array.Length);
        var array2 = new ushort[1500];
        for (var j = 0; j < 250; j++)
        {
            array2[j * 6] = (ushort)(j * 4);
            array2[j * 6 + 1] = (ushort)(j * 4 + 1);
            array2[j * 6 + 2] = (ushort)(j * 4 + 2);
            array2[j * 6 + 3] = (ushort)(j * 4 + 2);
            array2[j * 6 + 4] = (ushort)(j * 4 + 3);
            array2[j * 6 + 5] = (ushort)(j * 4);
        }

        _starsIndexBuffer?.SetData(array2, 0, array2.Length);
    }

    public float CalculateDawnGlowIntensity(float timeOfDay)
    {
        var num = MathUtils.Lerp(0.1f, 0.75f, MathUtils.LinearStep(-0.05f, 0.15f, CalculateWinterDistance()));
        var middawn = _subsystemTimeOfDay.MidDawn;
        var num2 = 1f * _subsystemTimeOfDay.DawnInterval;
        return num * MathUtils.Max(1f - IntervalUtils.Distance(timeOfDay, middawn) / num2 * 2f, 0f);
    }

    private float CalculateWinterDistance()
    {
        var t = IntervalUtils.Midpoint(SeasonCalendar.WinterStart, SeasonCalendar.SpringStart);
        var num = IntervalUtils.Interval(SeasonCalendar.WinterStart, SeasonCalendar.SpringStart);
        return IntervalUtils.Distance(_subsystemGameInfo.WorldSettings.TimeOfYear, t) - 0.5f * num;
    }

    public float CalculateDuskGlowIntensity(float timeOfDay)
    {
        var num = MathUtils.Lerp(0.2f, 1f, MathUtils.LinearStep(-0.05f, 0.15f, CalculateWinterDistance()));
        var middusk = _subsystemTimeOfDay.MidDusk;
        var num2 = 1f * _subsystemTimeOfDay.DuskInterval;
        return num * MathUtils.Max(1f - IntervalUtils.Distance(timeOfDay, middusk) / num2 * 2f, 0f);
    }

    public struct SkyVertex
    {
        public Vector3 Position;

        public Color Color;
    }

    public class SkyDome : IDisposable
    {
        public const int VerticesCountX = 16;

        public const int VerticesCountY = 8;

        public RenderTarget2D? Background;

        public IndexBuffer? IndexBuffer;

        public readonly ushort[] Indices = new ushort[714];

        public float? LastUpdateFogDensity; //new

        public float LastUpdateLightningStrikeBrightness;

        public float? LastUpdatePrecipitationIntensity;

        public int? LastUpdateTemperature;

        public float? LastUpdateTimeOfDay;

        public VertexBuffer? VertexBuffer;

        public readonly SkyVertex[] Vertices = new SkyVertex[128];

        public void Dispose()
        {
            Background?.Dispose();
            Background = null;
            VertexBuffer?.Dispose();
            VertexBuffer = null;
            IndexBuffer?.Dispose();
            IndexBuffer = null;
        }
    }

    private struct StarVertex
    {
        public Vector3 Position;

        public Vector2 TextureCoordinate;

        public Color Color;
    }
}
