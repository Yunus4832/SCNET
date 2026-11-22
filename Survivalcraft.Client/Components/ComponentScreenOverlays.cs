using Engine.Graphics;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Components;

public class ComponentScreenOverlays : Component, IDrawable, IUpdateable
{
    public UpdateExecutionSide UpdateSide => UpdateExecutionSide.Presentation;

    private static readonly int[] _drawOrders = [1101];

    private Point2 _cellsCount;

    private ComponentGui _componentGui = null!;

    private ComponentCharacter _componentCharacter = null!;

    private Vector2[] _iceVertices = [];

    private bool _isUnderWater;

    private float? _light;

    private PrimitivesRenderer2D PrimitivesRenderer2D => field ??= new PrimitivesRenderer2D();

    private PrimitivesRenderer3D PrimitivesRenderer3D => field ??= new PrimitivesRenderer3D();

    private readonly Random _random = new(0);

    private SubsystemSkyPresentation _subsystemSky = null!;

    private SubsystemTerrain _subsystemTerrain = null!;

    private SubsystemTime _subsystemTime = null!;

    private double? _waterSurfaceCrossTime;

    public float BlackoutFactor { get; set; }

    public float RedOutFactor { get; set; }

    public float GreenOutFactor { get; set; }

    public string FloatingMessage { get; set; } = string.Empty;

    public float FloatingMessageFactor { get; set; }

    public string Message { get; set; } = string.Empty;

    public float MessageFactor { get; set; }

    public float IceFactor { get; set; }

    public int[] DrawOrders => _drawOrders;

    public void Draw(Camera camera, int drawOrder)
    {
        if (!WorldRuntimeContext.For(Project).HasPresentation) { return; }

        if (_componentCharacter.Entity.FindComponent<ComponentCharacterPresentation>(true)!.GameWidget !=
            camera.GameWidget)
        {
            return;
        }

        if (_waterSurfaceCrossTime.HasValue)
        {
            var secondsSinceCrossing = (float)(_subsystemTime.GameTime - _waterSurfaceCrossTime.Value);
            var splashIntensity = 0.66f * MathUtils.Sqr(MathUtils.Saturate(1f - 0.75f * secondsSinceCrossing));
            if (splashIntensity > 0.01f)
            {
                Matrix matrix = default;
                matrix.Translation = Vector3.Zero;
                matrix.Forward = camera.ViewDirection;
                matrix.Right = Vector3.Normalize(Vector3.Cross(camera.ViewUp, matrix.Forward));
                matrix.Up = Vector3.Normalize(Vector3.Cross(matrix.Right, matrix.Forward));
                var vector = matrix.ToYawPitchRoll();
                var zero = Vector2.Zero;
                zero.X -= 2f * vector.X / (float)Math.PI + 0.05f * MathUtils.Sin(5f * secondsSinceCrossing);
                zero.Y += 2f * vector.Y / (float)Math.PI +
                          (_isUnderWater ? 0.75f * secondsSinceCrossing : -0.75f * secondsSinceCrossing);
                var texture = ContentManager.Get<Texture2D>("Textures/SplashOverlay");
                DrawTexturedOverlay(camera, texture, new Color(156, 206, 210), splashIntensity, splashIntensity, zero);
            }
        }

        if (IceFactor > 0f)
        {
            DrawIceOverlay(camera, IceFactor);
        }

        if (RedOutFactor > 0.01f)
        {
            DrawOverlay(camera, new Color(255, 64, 0), MathUtils.Saturate(2f * (RedOutFactor - 0.5f)), RedOutFactor);
        }

        if (BlackoutFactor > 0.01f)
        {
            DrawOverlay(camera, Color.Black, MathUtils.Saturate(2f * (BlackoutFactor - 0.5f)), BlackoutFactor);
        }

        if (GreenOutFactor > 0.01f)
        {
            DrawOverlay(camera, new Color(166, 175, 103), GreenOutFactor, MathUtils.Saturate(2f * GreenOutFactor));
        }

        if (!string.IsNullOrEmpty(FloatingMessage) && FloatingMessageFactor > 0.01f)
        {
            DrawFloatingMessage(camera, FloatingMessage, FloatingMessageFactor);
        }

        if (!string.IsNullOrEmpty(Message) && MessageFactor > 0.01f)
        {
            DrawMessage(camera, Message, MessageFactor);
        }
    }

    public UpdateOrder UpdateOrder => UpdateOrder.Reset;

    public void Update(float dt)
    {
        var isUnderwater = _subsystemSky.ViewUnderWaterDepth > 0f;
        if (isUnderwater != _isUnderWater)
        {
            _isUnderWater = isUnderwater;
            _waterSurfaceCrossTime = _subsystemTime.GameTime;
        }

        BlackoutFactor = 0f;
        RedOutFactor = 0f;
        GreenOutFactor = 0f;
        IceFactor = 0f;
        FloatingMessage = string.Empty;
        FloatingMessageFactor = 0f;
        Message = string.Empty;
        MessageFactor = 0f;
    }

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        _subsystemTime = Project.FindSubsystem<SubsystemTime>(true)!;
        _subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true)!;
        _subsystemSky = Project.FindSubsystem<SubsystemSkyPresentation>(true)!;
        _componentGui = Entity.FindComponent<ComponentGui>(true)!;
        _componentCharacter = Entity.FindComponent<ComponentCharacter>(true)!;
    }

    public void DrawOverlay(Camera camera, Color color, float innerFactor, float outerFactor)
    {
        var viewportSize = camera.ViewportSize;
        var topLeft = new Vector2(0f, 0f);
        var topRight = new Vector2(viewportSize.X, 0f);
        var bottomRight = new Vector2(viewportSize.X, viewportSize.Y);
        var bottomLeft = new Vector2(0f, viewportSize.Y);
        var p = new Vector2(viewportSize.X / 2f, viewportSize.Y / 2f);
        var color2 = color * outerFactor;
        var color3 = color * innerFactor;
        var flatBatch2D = PrimitivesRenderer2D.FlatBatch(0, DepthStencilState.None, null, BlendState.AlphaBlend);
        var count = flatBatch2D.TriangleVertices.Count;
        flatBatch2D.QueueTriangle(topLeft, topRight, p, 0f, color2, color2, color3);
        flatBatch2D.QueueTriangle(topRight, bottomRight, p, 0f, color2, color2, color3);
        flatBatch2D.QueueTriangle(bottomRight, bottomLeft, p, 0f, color2, color2, color3);
        flatBatch2D.QueueTriangle(bottomLeft, topLeft, p, 0f, color2, color2, color3);
        flatBatch2D.TransformTriangles(camera.ViewportMatrix, count);
        flatBatch2D.Flush();
    }

    public void DrawTexturedOverlay(
        Camera camera,
        Texture2D texture,
        Color color,
        float innerFactor,
        float outerFactor,
        Vector2 offset
    )
    {
        var viewportSize = camera.ViewportSize;
        var aspectRatio = viewportSize.X / viewportSize.Y;
        var topLeft = new Vector2(0f, 0f);
        var topRight = new Vector2(viewportSize.X, 0f);
        var bottomRight = new Vector2(viewportSize.X, viewportSize.Y);
        var bottomLeft = new Vector2(0f, viewportSize.Y);
        var p = new Vector2(viewportSize.X / 2f, viewportSize.Y / 2f);
        offset.X = MathUtils.Remainder(offset.X, 1f);
        offset.Y = MathUtils.Remainder(offset.Y, 1f);
        var topLeftUv = new Vector2(0f, 0f) + offset;
        var topRightUv = new Vector2(aspectRatio, 0f) + offset;
        var bottomRightUv = new Vector2(aspectRatio, 1f) + offset;
        var bottomLeftUv = new Vector2(0f, 1f) + offset;
        var texCoord = new Vector2(aspectRatio / 2f, 0.5f) + offset;
        var color2 = color * outerFactor;
        var color3 = color * innerFactor;
        var texturedBatch2D = PrimitivesRenderer2D.TexturedBatch(
            texture,
            false,
            0,
            DepthStencilState.None,
            null,
            BlendState.PremultipliedAdditive,
            SamplerState.PointWrap
        );
        var count = texturedBatch2D.TriangleVertices.Count;
        texturedBatch2D.QueueTriangle(
            topLeft,
            topRight,
            p,
            0f,
            topLeftUv,
            topRightUv,
            texCoord,
            color2,
            color2,
            color3
        );
        texturedBatch2D.QueueTriangle(
            topRight,
            bottomRight,
            p,
            0f,
            topRightUv,
            bottomRightUv,
            texCoord,
            color2,
            color2,
            color3
        );
        texturedBatch2D.QueueTriangle(
            bottomRight,
            bottomLeft,
            p,
            0f,
            bottomRightUv,
            bottomLeftUv,
            texCoord,
            color2,
            color2,
            color3
        );
        texturedBatch2D.QueueTriangle(
            bottomLeft,
            topLeft,
            p,
            0f,
            bottomLeftUv,
            topLeftUv,
            texCoord,
            color2,
            color2,
            color3
        );
        texturedBatch2D.TransformTriangles(camera.ViewportMatrix, count);
        texturedBatch2D.Flush();
    }

    public void DrawIceOverlay(Camera camera, float factor)
    {
        var viewportSize = camera.ViewportSize;
        var s = camera.Eye.HasValue ? 1.3f : 1f;
        var iceCoverage = camera.Eye.HasValue ? MathUtils.Pow(factor, 0.4f) : factor;
        var v = camera.Eye.HasValue ? viewportSize : new Vector2(1f);
        var distanceScale = v.Length();
        var point = new Point2((int)MathUtils.Round(12f * viewportSize.X / viewportSize.Y), (int)MathUtils.Round(12f));
        if (_iceVertices.Length == 0 || _cellsCount != point)
        {
            _cellsCount = point;
            _random.Seed(0);
            _iceVertices = new Vector2[(point.X + 1) * (point.Y + 1)];
            for (var i = 0; i <= point.X; i++)
            {
                for (var j = 0; j <= point.Y; j++)
                {
                    float jitteredColumn = i;
                    float jitteredRow = j;
                    if (i != 0 && i != point.X)
                    {
                        jitteredColumn += _random.Float(-0.4f, 0.4f);
                    }

                    if (j != 0 && j != point.Y)
                    {
                        jitteredRow += _random.Float(-0.4f, 0.4f);
                    }

                    var x = jitteredColumn / point.X;
                    var y = jitteredRow / point.Y;
                    _iceVertices[i + j * (point.X + 1)] = new Vector2(x, y);
                }
            }
        }

        var overlayWidth = Vector3.UnitX / camera.ProjectionMatrix.M11 * 2f * 0.2f * s;
        var overlayHeight = Vector3.UnitY / camera.ProjectionMatrix.M22 * 2f * 0.2f * s;
        var v2 = -0.2f * Vector3.UnitZ - 0.5f * (overlayWidth + overlayHeight);
        _light = LightingManager.CalculateSmoothLight(_subsystemTerrain, camera.ViewPosition) ?? _light ?? 1f;

        var color = Color.MultiplyColorOnly(Color.White, _light.Value);
        _random.Seed(0);
        var texture = ContentManager.Get<Texture2D>("Textures/IceOverlay");
        var texturedBatch3D = PrimitivesRenderer3D.TexturedBatch(
            texture,
            false,
            0,
            DepthStencilState.None,
            RasterizerState.CullNoneScissor,
            BlendState.AlphaBlend,
            SamplerState.PointWrap
        );
        var v3 = new Vector2(viewportSize.X / viewportSize.Y, 1f);
        var lastCell = new Vector2(point.X - 1, point.Y - 1);
        for (var k = 0; k < point.X; k++)
        {
            for (var l = 0; l < point.Y; l++)
            {
                var normalizedDistance = (new Vector2(2 * k / lastCell.X - 1f, 2 * l / lastCell.Y - 1f) * v).Length() /
                                         distanceScale;
                if (1f - normalizedDistance + _random.Float(0f, 0.05f) < iceCoverage)
                {
                    var v4 = _iceVertices[k + l * (point.X + 1)];
                    var v5 = _iceVertices[k + 1 + l * (point.X + 1)];
                    var v6 = _iceVertices[k + 1 + (l + 1) * (point.X + 1)];
                    var v7 = _iceVertices[k + (l + 1) * (point.X + 1)];
                    var corner00 = v2 + v4.X * overlayWidth + v4.Y * overlayHeight;
                    var p = v2 + v5.X * overlayWidth + v5.Y * overlayHeight;
                    var corner11 = v2 + v6.X * overlayWidth + v6.Y * overlayHeight;
                    var p2 = v2 + v7.X * overlayWidth + v7.Y * overlayHeight;
                    var uv00 = v4 * v3;
                    var texCoord = v5 * v3;
                    var uv11 = v6 * v3;
                    var texCoord2 = v7 * v3;
                    texturedBatch3D.QueueTriangle(corner00, p, corner11, uv00, texCoord, uv11, color);
                    texturedBatch3D.QueueTriangle(corner11, p2, corner00, uv11, texCoord2, uv00, color);
                }
            }
        }

        texturedBatch3D.Flush(camera.ProjectionMatrix);
    }

    public void DrawFloatingMessage(Camera camera, string message, float factor)
    {
        var font = ContentManager.Get<BitmapFont>("Fonts/Pericles");
        if (!camera.Eye.HasValue)
        {
            var position = camera.ViewportSize / 2f;
            position.X += 0.07f *
                          camera.ViewportSize.X *
                          (float)MathUtils.Sin(1.7300000190734863 * Time.FrameStartTime);
            position.Y += 0.07f *
                          camera.ViewportSize.Y *
                          (float)MathUtils.Cos(1.1200000047683716 * Time.FrameStartTime);
            var fontBatch2D =
                PrimitivesRenderer2D.FontBatch(font, 1, DepthStencilState.None, null, BlendState.AlphaBlend);
            var count = fontBatch2D.TriangleVertices.Count;
            fontBatch2D.QueueText(
                message,
                position,
                0f,
                Color.White * factor,
                TextAnchor.HorizontalCenter | TextAnchor.VerticalCenter,
                Vector2.One * camera.GameWidget.GlobalScale,
                Vector2.Zero
            );
            fontBatch2D.TransformTriangles(camera.ViewportMatrix, count);
            fontBatch2D.Flush();
        }
        else
        {
            var position2 = -4f * Vector3.UnitZ;
            position2.X += 0.28f * (float)MathUtils.Sin(1.7300000190734863 * Time.FrameStartTime);
            position2.Y += 0.28f * (float)MathUtils.Cos(1.1200000047683716 * Time.FrameStartTime);
            var fontBatch3D = PrimitivesRenderer3D.FontBatch(
                font,
                1,
                DepthStencilState.None,
                RasterizerState.CullNoneScissor,
                BlendState.AlphaBlend
            );
            fontBatch3D.QueueText(
                message,
                position2,
                0.008f * Vector3.UnitX,
                -0.008f * Vector3.UnitY,
                Color.White * factor,
                TextAnchor.HorizontalCenter | TextAnchor.VerticalCenter,
                Vector2.Zero
            );
            fontBatch3D.Flush(camera.ProjectionMatrix);
        }
    }

    public void DrawMessage(Camera camera, string message, float factor)
    {
        var font = ContentManager.Get<BitmapFont>("Fonts/Pericles");
        if (!camera.Eye.HasValue)
        {
            var position = new Vector2(camera.ViewportSize.X / 2f, camera.ViewportSize.Y - 25f);
            var fontBatch2D =
                PrimitivesRenderer2D.FontBatch(font, 0, DepthStencilState.None, null, BlendState.AlphaBlend);
            var count = fontBatch2D.TriangleVertices.Count;
            fontBatch2D.QueueText(
                message,
                position,
                0f,
                Color.Gray * factor,
                TextAnchor.HorizontalCenter | TextAnchor.Bottom,
                Vector2.One * camera.GameWidget.GlobalScale,
                Vector2.Zero
            );
            fontBatch2D.TransformTriangles(camera.ViewportMatrix, count);
            fontBatch2D.Flush();
        }
        else
        {
            var position2 = -4f * Vector3.UnitZ + -0.24f / camera.ProjectionMatrix.M22 * 2f * Vector3.UnitY;
            var fontBatch3D = PrimitivesRenderer3D.FontBatch(
                font,
                0,
                DepthStencilState.None,
                RasterizerState.CullNoneScissor,
                BlendState.AlphaBlend
            );
            fontBatch3D.QueueText(
                message,
                position2,
                0.0104f * Vector3.UnitX,
                -0.0104f * Vector3.UnitY,
                Color.White * factor,
                TextAnchor.HorizontalCenter | TextAnchor.VerticalCenter,
                Vector2.Zero
            );
            fontBatch3D.Flush(camera.ProjectionMatrix);
        }
    }
}
