using Engine.Graphics;
using Engine.Media;

namespace Game.Widgets;

public class FurnitureDesignWidget : Widget
{
    public enum ViewMode
    {
        Side,
        Top,
        Front,
        Perspective
    }

    private const string _typeName = nameof(FurnitureDesignWidget);

    public static bool DrawDebugFurniture;

    private Vector3 _direction;

    private Vector2? _dragStartPoint;

    private readonly PrimitivesRenderer2D _primitivesRenderer2D = new();

    private readonly PrimitivesRenderer3D _primitivesRenderer3D = new();

    private Vector2 _rotationSpeed;

    public Vector2 Size { get; set; }

    public ViewMode Mode { get; set; }

    public FurnitureDesign? Design { get; set; }


    public FurnitureDesignWidget()
    {
        ClampToBounds = true;
        Size = new Vector2(float.PositiveInfinity);
        Mode = ViewMode.Perspective;
        _direction = Vector3.Normalize(new Vector3(1f, -0.5f, -1f));
        _rotationSpeed = new Vector2(2f, 0.5f);
    }

    public override void Draw(DrawContext dc)
    {
        if (Design == null)
        {
            return;
        }

        Matrix matrix;
        if (Mode == ViewMode.Perspective)
        {
            var viewport = Display.Viewport;
            var vector = new Vector3(0.5f, 0.5f, 0.5f);
            var m = Matrix.CreateLookAt(2.65f * _direction + vector, vector, Vector3.UnitY);
            var m2 = Matrix.CreatePerspectiveFieldOfView(1.2f, ActualSize.X / ActualSize.Y, 0.4f, 4f);
            var m3 =
                MatrixUtils.CreateScaleTranslation(ActualSize.X, 0f - ActualSize.Y, ActualSize.X / 2f,
                    ActualSize.Y / 2f) * GlobalTransform *
                MatrixUtils.CreateScaleTranslation(2f / viewport.Width, -2f / viewport.Height, -1f, 1f);
            matrix = m * m2 * m3;
            var flatBatch3D = _primitivesRenderer3D.FlatBatch(1, DepthStencilState.DepthRead);
            for (var i = 0; i <= Design.Resolution; i++)
            {
                var num = i / (float)Design.Resolution;
                var color = i % 2 == 0 ? new Color(56, 56, 56, 56) : new Color(28, 28, 28, 28);
                color *= GlobalColorTransform;
                flatBatch3D.QueueLine(new Vector3(num, 0f, 0f), new Vector3(num, 0f, 1f), color);
                flatBatch3D.QueueLine(new Vector3(0f, 0f, num), new Vector3(1f, 0f, num), color);
                flatBatch3D.QueueLine(new Vector3(0f, num, 0f), new Vector3(0f, num, 1f), color);
                flatBatch3D.QueueLine(new Vector3(0f, 0f, num), new Vector3(0f, 1f, num), color);
                flatBatch3D.QueueLine(new Vector3(0f, num, 1f), new Vector3(1f, num, 1f), color);
                flatBatch3D.QueueLine(new Vector3(num, 0f, 1f), new Vector3(num, 1f, 1f), color);
            }

            var color2 = new Color(64, 64, 64, 255) * GlobalColorTransform;
            var fontBatch3D = _primitivesRenderer3D.FontBatch(ContentManager.Get<BitmapFont>("Fonts/Pericles"), 1);
            fontBatch3D.QueueText("Front", new Vector3(0.5f, 0f, 0f), 0.004f * new Vector3(-1f, 0f, 0f),
                0.004f * new Vector3(0f, 0f, -1f), color2, TextAnchor.HorizontalCenter);
            fontBatch3D.QueueText("Side", new Vector3(1f, 0f, 0.5f), 0.004f * new Vector3(0f, 0f, -1f),
                0.004f * new Vector3(1f, 0f, 0f), color2, TextAnchor.HorizontalCenter);
            if (DrawDebugFurniture)
            {
                DebugDraw();
            }
        }
        else
        {
            Vector3 position;
            Vector3 up;
            if (Mode == ViewMode.Side)
            {
                position = new Vector3(1f, 0f, 0f);
                up = new Vector3(0f, 1f, 0f);
            }
            else if (Mode != ViewMode.Top)
            {
                position = new Vector3(0f, 0f, -10f);
                up = new Vector3(0f, 1f, 0f);
            }
            else
            {
                position = new Vector3(0f, 1f, 0f);
                up = new Vector3(0f, 0f, 1f);
            }

            var viewport2 = Display.Viewport;
            var num2 = MathUtils.Min(ActualSize.X, ActualSize.Y);
            var m4 = Matrix.CreateLookAt(position, new Vector3(0f, 0f, 0f), up);
            var m5 = Matrix.CreateOrthographic(2f, 2f, -10f, 10f);
            var m6 = MatrixUtils.CreateScaleTranslation(num2, 0f - num2, ActualSize.X / 2f, ActualSize.Y / 2f) *
                     GlobalTransform *
                     MatrixUtils.CreateScaleTranslation(2f / viewport2.Width, -2f / viewport2.Height, -1f, 1f);
            matrix = Matrix.CreateTranslation(-0.5f, -0.5f, -0.5f) * m4 * m5 * m6;
            var flatBatch2D = _primitivesRenderer2D.FlatBatch();
            var m7 = GlobalTransform;
            for (var j = 1; j < Design.Resolution; j++)
            {
                var num3 = j / (float)Design.Resolution;
                var v = new Vector2(ActualSize.X * num3, 0f);
                var v2 = new Vector2(ActualSize.X * num3, ActualSize.Y);
                var v3 = new Vector2(0f, ActualSize.Y * num3);
                var v4 = new Vector2(ActualSize.X, ActualSize.Y * num3);
                Vector2.Transform(ref v, ref m7, out v);
                Vector2.Transform(ref v2, ref m7, out v2);
                Vector2.Transform(ref v3, ref m7, out v3);
                Vector2.Transform(ref v4, ref m7, out v4);
                var color3 = j % 2 == 0 ? new Color(0, 0, 0, 56) : new Color(0, 0, 0, 28);
                var color4 = j % 2 == 0 ? new Color(56, 56, 56, 56) : new Color(28, 28, 28, 28);
                color3 *= GlobalColorTransform;
                color4 *= GlobalColorTransform;
                flatBatch2D.QueueLine(v, v2, 0f, j % 2 == 0 ? color3 : color3 * 0.75f);
                flatBatch2D.QueueLine(v + new Vector2(1f, 0f), v2 + new Vector2(1f, 0f), 0f, color4);
                flatBatch2D.QueueLine(v3, v4, 0f, color3);
                flatBatch2D.QueueLine(v3 + new Vector2(0f, 1f), v4 + new Vector2(0f, 1f), 0f, color4);
            }
        }

        var matrix2 = Matrix.Identity;
        var geometry = Design.Geometry;
        for (var k = 0; k < 6; k++)
        {
            var globalColorTransform = GlobalColorTransform;
            if (Mode == ViewMode.Perspective)
            {
                var num4 = LightingManager.LightIntensityByLightValueAndFace[15 + 16 * CellFace.OppositeFace(k)];
                globalColorTransform *= new Color(num4, num4, num4);
            }

            BlocksManager.DrawMeshBlock(
                _primitivesRenderer3D,
                geometry.SubsetOpaqueByFace[k],
                globalColorTransform,
                1f,
                ref matrix2,
                null
            );
            BlocksManager.DrawMeshBlock(
                _primitivesRenderer3D,
                geometry.SubsetAlphaTestByFace[k],
                globalColorTransform,
                1f,
                ref matrix2,
                null
            );
        }

        _primitivesRenderer3D.Flush(matrix);
        _primitivesRenderer2D.Flush();
    }

    public override void Update()
    {
        if (Mode != ViewMode.Perspective)
        {
            return;
        }

        if (Input.Tap.HasValue && HitTestGlobal(Input.Tap.Value) == this)
        {
            _dragStartPoint = Input.Tap;
        }

        if (Input.Press.HasValue)
        {
            if (_dragStartPoint.HasValue)
            {
                var vector = ScreenToWidget(Input.Press.Value) - ScreenToWidget(_dragStartPoint.Value);
                Vector2 vector2 = default;
                vector2.Y = -0.01f * vector.X;
                vector2.X = 0.01f * vector.Y;
                if (Time.FrameDuration > 0f)
                {
                    _rotationSpeed = vector2 / Time.FrameDuration;
                }

                Rotate(vector2);
                _dragStartPoint = Input.Press;
            }
        }
        else
        {
            _dragStartPoint = null;
            Rotate(_rotationSpeed * Time.FrameDuration);
            _rotationSpeed *= MathUtils.Pow(0.1f, Time.FrameDuration);
        }
    }

    protected override void MeasureOverride(Vector2 parentAvailableSize)
    {
        IsDrawRequired = Design != null;
        DesiredSize = Size;
    }

    public void Rotate(Vector2 angles)
    {
        var num = MathUtils.DegToRad(1f);
        var axis = Vector3.Normalize(Vector3.Cross(_direction, Vector3.UnitY));
        _direction = Vector3.TransformNormal(_direction, Matrix.CreateRotationY(angles.Y));
        var num2 = MathUtils.Acos(Vector3.Dot(_direction, Vector3.UnitY));
        var num3 = MathUtils.Acos(Vector3.Dot(_direction, -Vector3.UnitY));
        angles.X = MathUtils.Min(angles.X, num2 - num);
        angles.X = MathUtils.Max(angles.X, 0f - (num3 - num));
        _direction = Vector3.TransformNormal(_direction, Matrix.CreateFromAxisAngle(axis, angles.X));
        _direction = Vector3.Normalize(_direction);
    }

    public void DebugDraw()
    {
    }
}
