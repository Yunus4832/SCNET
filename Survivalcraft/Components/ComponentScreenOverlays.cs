using Engine.Graphics;
using Engine.Media;
using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Components;

public class ComponentScreenOverlays : Component, IDrawable, IUpdateable
{
    private static readonly int[] _drawOrders = [1101];

    private Point2 _cellsCount;

    private ComponentGui _componentGui = null!;

    private ComponentPlayer _componentPlayer = null!;

    private Vector2[] _iceVertices = [];

    private bool _isUnderWater;

    private float? _light;

    private readonly PrimitivesRenderer2D _primitivesRenderer2D = new();

    private readonly PrimitivesRenderer3D _primitivesRenderer3D = new();

    private readonly Random _random = new(0);

    private SubsystemSky _subsystemSky = null!;

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
        if (_componentPlayer.GameWidget != camera.GameWidget)
        {
            return;
        }

        if (_waterSurfaceCrossTime.HasValue)
        {
            var num = (float)(_subsystemTime.GameTime - _waterSurfaceCrossTime.Value);
            var num2 = 0.66f * MathUtils.Sqr(MathUtils.Saturate(1f - 0.75f * num));
            if (num2 > 0.01f)
            {
                Matrix matrix = default;
                matrix.Translation = Vector3.Zero;
                matrix.Forward = camera.ViewDirection;
                matrix.Right = Vector3.Normalize(Vector3.Cross(camera.ViewUp, matrix.Forward));
                matrix.Up = Vector3.Normalize(Vector3.Cross(matrix.Right, matrix.Forward));
                var vector = matrix.ToYawPitchRoll();
                var zero = Vector2.Zero;
                zero.X -= 2f * vector.X / (float)Math.PI + 0.05f * MathUtils.Sin(5f * num);
                zero.Y += 2f * vector.Y / (float)Math.PI + (_isUnderWater ? 0.75f * num : -0.75f * num);
                var texture = ContentManager.Get<Texture2D>("Textures/SplashOverlay");
                DrawTexturedOverlay(camera, texture, new Color(156, 206, 210), num2, num2, zero);
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
        var flag = _subsystemSky.ViewUnderWaterDepth > 0f;
        if (flag != _isUnderWater)
        {
            _isUnderWater = flag;
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
        _subsystemSky = Project.FindSubsystem<SubsystemSky>(true)!;
        _componentGui = Entity.FindComponent<ComponentGui>(true)!;
        _componentPlayer = Entity.FindComponent<ComponentPlayer>(true)!;
    }

    public void DrawOverlay(Camera camera, Color color, float innerFactor, float outerFactor)
    {
        var viewportSize = camera.ViewportSize;
        var vector = new Vector2(0f, 0f);
        var vector2 = new Vector2(viewportSize.X, 0f);
        var vector3 = new Vector2(viewportSize.X, viewportSize.Y);
        var vector4 = new Vector2(0f, viewportSize.Y);
        var p = new Vector2(viewportSize.X / 2f, viewportSize.Y / 2f);
        var color2 = color * outerFactor;
        var color3 = color * innerFactor;
        var flatBatch2D = _primitivesRenderer2D.FlatBatch(0, DepthStencilState.None, null, BlendState.AlphaBlend);
        var count = flatBatch2D.TriangleVertices.Count;
        flatBatch2D.QueueTriangle(vector, vector2, p, 0f, color2, color2, color3);
        flatBatch2D.QueueTriangle(vector2, vector3, p, 0f, color2, color2, color3);
        flatBatch2D.QueueTriangle(vector3, vector4, p, 0f, color2, color2, color3);
        flatBatch2D.QueueTriangle(vector4, vector, p, 0f, color2, color2, color3);
        flatBatch2D.TransformTriangles(camera.ViewportMatrix, count);
        flatBatch2D.Flush();
    }

    public void DrawTexturedOverlay(Camera camera, Texture2D texture, Color color, float innerFactor, float outerFactor,
        Vector2 offset)
    {
        var viewportSize = camera.ViewportSize;
        var num = viewportSize.X / viewportSize.Y;
        var vector = new Vector2(0f, 0f);
        var vector2 = new Vector2(viewportSize.X, 0f);
        var vector3 = new Vector2(viewportSize.X, viewportSize.Y);
        var vector4 = new Vector2(0f, viewportSize.Y);
        var p = new Vector2(viewportSize.X / 2f, viewportSize.Y / 2f);
        offset.X = MathUtils.Remainder(offset.X, 1f);
        offset.Y = MathUtils.Remainder(offset.Y, 1f);
        var vector5 = new Vector2(0f, 0f) + offset;
        var vector6 = new Vector2(num, 0f) + offset;
        var vector7 = new Vector2(num, 1f) + offset;
        var vector8 = new Vector2(0f, 1f) + offset;
        var texCoord = new Vector2(num / 2f, 0.5f) + offset;
        var color2 = color * outerFactor;
        var color3 = color * innerFactor;
        var texturedBatch2D = _primitivesRenderer2D.TexturedBatch(texture, false, 0, DepthStencilState.None, null,
            BlendState.Additive, SamplerState.PointWrap);
        var count = texturedBatch2D.TriangleVertices.Count;
        texturedBatch2D.QueueTriangle(vector, vector2, p, 0f, vector5, vector6, texCoord, color2, color2, color3);
        texturedBatch2D.QueueTriangle(vector2, vector3, p, 0f, vector6, vector7, texCoord, color2, color2, color3);
        texturedBatch2D.QueueTriangle(vector3, vector4, p, 0f, vector7, vector8, texCoord, color2, color2, color3);
        texturedBatch2D.QueueTriangle(vector4, vector, p, 0f, vector8, vector5, texCoord, color2, color2, color3);
        texturedBatch2D.TransformTriangles(camera.ViewportMatrix, count);
        texturedBatch2D.Flush();
    }

    public void DrawIceOverlay(Camera camera, float factor)
    {
        var viewportSize = camera.ViewportSize;
        var s = camera.Eye.HasValue ? 1.3f : 1f;
        var num = camera.Eye.HasValue ? MathUtils.Pow(factor, 0.4f) : factor;
        var v = camera.Eye.HasValue ? viewportSize : new Vector2(1f);
        var num2 = v.Length();
        var point = new Point2((int)MathUtils.Round(12f * viewportSize.X / viewportSize.Y), (int)MathUtils.Round(12f));
        if (_iceVertices.Length == 0 || _cellsCount != point)
        {
            _cellsCount = point;
            _random.Seed(0);
            _iceVertices = new Vector2[(point.X + 1) * (point.Y + 1)];
            for (var i = 0; i <= point.X; i++)
            for (var j = 0; j <= point.Y; j++)
            {
                float num3 = i;
                float num4 = j;
                if (i != 0 && i != point.X)
                {
                    num3 += _random.Float(-0.4f, 0.4f);
                }

                if (j != 0 && j != point.Y)
                {
                    num4 += _random.Float(-0.4f, 0.4f);
                }

                var x = num3 / point.X;
                var y = num4 / point.Y;
                _iceVertices[i + j * (point.X + 1)] = new Vector2(x, y);
            }
        }

        var vector = Vector3.UnitX / camera.ProjectionMatrix.M11 * 2f * 0.2f * s;
        var vector2 = Vector3.UnitY / camera.ProjectionMatrix.M22 * 2f * 0.2f * s;
        var v2 = -0.2f * Vector3.UnitZ - 0.5f * (vector + vector2);
        if (!_light.HasValue || Time.PeriodicEvent(0.05000000074505806, 0.0))
        {
            _light = LightingManager.CalculateSmoothLight(_subsystemTerrain, camera.ViewPosition) ?? _light ?? 1f;
        }

        var color = Color.MultiplyColorOnly(Color.White, _light.Value);
        _random.Seed(0);
        var texture = ContentManager.Get<Texture2D>("Textures/IceOverlay");
        var texturedBatch3D = _primitivesRenderer3D.TexturedBatch(texture, false, 0, DepthStencilState.None,
            RasterizerState.CullNoneScissor, BlendState.AlphaBlend, SamplerState.PointWrap);
        var v3 = new Vector2(viewportSize.X / viewportSize.Y, 1f);
        var vector3 = new Vector2(point.X - 1, point.Y - 1);
        for (var k = 0; k < point.X; k++)
        for (var l = 0; l < point.Y; l++)
        {
            var num5 = (new Vector2(2 * k / vector3.X - 1f, 2 * l / vector3.Y - 1f) * v).Length() / num2;
            if (1f - num5 + _random.Float(0f, 0.05f) < num)
            {
                var v4 = _iceVertices[k + l * (point.X + 1)];
                var v5 = _iceVertices[k + 1 + l * (point.X + 1)];
                var v6 = _iceVertices[k + 1 + (l + 1) * (point.X + 1)];
                var v7 = _iceVertices[k + (l + 1) * (point.X + 1)];
                var vector4 = v2 + v4.X * vector + v4.Y * vector2;
                var p = v2 + v5.X * vector + v5.Y * vector2;
                var vector5 = v2 + v6.X * vector + v6.Y * vector2;
                var p2 = v2 + v7.X * vector + v7.Y * vector2;
                var vector6 = v4 * v3;
                var texCoord = v5 * v3;
                var vector7 = v6 * v3;
                var texCoord2 = v7 * v3;
                texturedBatch3D.QueueTriangle(vector4, p, vector5, vector6, texCoord, vector7, color);
                texturedBatch3D.QueueTriangle(vector5, p2, vector4, vector7, texCoord2, vector6, color);
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
            position.X += 0.07f * camera.ViewportSize.X *
                          (float)MathUtils.Sin(1.7300000190734863 * Time.FrameStartTime);
            position.Y += 0.07f * camera.ViewportSize.Y *
                          (float)MathUtils.Cos(1.1200000047683716 * Time.FrameStartTime);
            var fontBatch2D =
                _primitivesRenderer2D.FontBatch(font, 1, DepthStencilState.None, null, BlendState.AlphaBlend);
            var count = fontBatch2D.TriangleVertices.Count;
            fontBatch2D.QueueText(message, position, 0f, Color.White * factor,
                TextAnchor.HorizontalCenter | TextAnchor.VerticalCenter, Vector2.One * camera.GameWidget.GlobalScale,
                Vector2.Zero);
            fontBatch2D.TransformTriangles(camera.ViewportMatrix, count);
            fontBatch2D.Flush();
        }
        else
        {
            var position2 = -4f * Vector3.UnitZ;
            position2.X += 0.28f * (float)MathUtils.Sin(1.7300000190734863 * Time.FrameStartTime);
            position2.Y += 0.28f * (float)MathUtils.Cos(1.1200000047683716 * Time.FrameStartTime);
            var fontBatch3D = _primitivesRenderer3D.FontBatch(font, 1, DepthStencilState.None,
                RasterizerState.CullNoneScissor, BlendState.AlphaBlend);
            fontBatch3D.QueueText(message, position2, 0.008f * Vector3.UnitX, -0.008f * Vector3.UnitY,
                Color.White * factor, TextAnchor.HorizontalCenter | TextAnchor.VerticalCenter, Vector2.Zero);
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
                _primitivesRenderer2D.FontBatch(font, 0, DepthStencilState.None, null, BlendState.AlphaBlend);
            var count = fontBatch2D.TriangleVertices.Count;
            fontBatch2D.QueueText(message, position, 0f, Color.Gray * factor,
                TextAnchor.HorizontalCenter | TextAnchor.Bottom, Vector2.One * camera.GameWidget.GlobalScale,
                Vector2.Zero);
            fontBatch2D.TransformTriangles(camera.ViewportMatrix, count);
            fontBatch2D.Flush();
        }
        else
        {
            var position2 = -4f * Vector3.UnitZ + -0.24f / camera.ProjectionMatrix.M22 * 2f * Vector3.UnitY;
            var fontBatch3D = _primitivesRenderer3D.FontBatch(font, 0, DepthStencilState.None,
                RasterizerState.CullNoneScissor, BlendState.AlphaBlend);
            fontBatch3D.QueueText(message, position2, 0.0104f * Vector3.UnitX, -0.0104f * Vector3.UnitY,
                Color.White * factor, TextAnchor.HorizontalCenter | TextAnchor.VerticalCenter, Vector2.Zero);
            fontBatch3D.Flush(camera.ProjectionMatrix);
        }
    }
}
