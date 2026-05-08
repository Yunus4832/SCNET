using Engine.Graphics;
using Engine.Serialization;

namespace Game.Widgets;

public class ArrowLineWidget : Widget
{
    private bool _parsingPending;

    private string _pointsString = string.Empty;

    private Vector2 _startOffset;

    private readonly List<Vector2> _vertices = [];

    public override bool IsHitTestVisible { get; set; } = false;

    public ArrowLineWidget()
    {
        Width = 6f;
        ArrowWidth = 0f;
        Color = Color.White;
        PointsString = "0, 0; 50, 0";
    }

    public float Width
    {
        get;
        set
        {
            field = value;
            _parsingPending = true;
        }
    }

    public float ArrowWidth
    {
        get;
        set
        {
            field = value;
            _parsingPending = true;
        }
    }

    public Color Color { get; set; }

    public string PointsString
    {
        get => _pointsString;
        set
        {
            _pointsString = value;
            _parsingPending = true;
        }
    }

    public bool AbsoluteCoordinates
    {
        get;
        set
        {
            field = value;
            _parsingPending = true;
        }
    }

    public override void Draw(DrawContext dc)
    {
        if (_parsingPending)
        {
            ParsePoints();
        }

        var color = Color * GlobalColorTransform;
        var flatBatch2D = dc.PrimitivesRenderer2D.FlatBatch(1, DepthStencilState.None);
        var count = flatBatch2D.TriangleVertices.Count;
        for (var i = 0; i < _vertices.Count; i += 3)
        {
            var p = _startOffset + _vertices[i];
            var p2 = _startOffset + _vertices[i + 1];
            var p3 = _startOffset + _vertices[i + 2];
            flatBatch2D.QueueTriangle(p, p2, p3, 0f, color);
        }

        flatBatch2D.TransformTriangles(GlobalTransform, count);
    }

    protected override void MeasureOverride(Vector2 parentAvailableSize)
    {
        if (_parsingPending)
        {
            ParsePoints();
        }

        IsDrawRequired = Color.A > 0 && Width > 0f;
    }

    private void ParsePoints()
    {
        _parsingPending = false;
        var array = _pointsString.Split([";"], StringSplitOptions.None);
        var list = array.Select(HumanReadableConverter.ConvertFromString<Vector2>).ToList();
        _vertices.Clear();
        for (var j = 0; j < list.Count; j++)
        {
            if (j >= 1)
            {
                var vector = list[j - 1];
                var vector2 = list[j];
                var vector3 = Vector2.Normalize(vector2 - vector);
                var vector4 = vector3;
                var v = vector3;
                if (j >= 2)
                {
                    vector4 = Vector2.Normalize(vector - list[j - 2]);
                }

                if (j <= list.Count - 2)
                {
                    v = Vector2.Normalize(list[j + 1] - vector2);
                }

                var v2 = Vector2.Perpendicular(vector4);
                var v3 = Vector2.Perpendicular(vector3);
                var num = (float)Math.PI - Vector2.Angle(vector4, vector3);
                var s = 0.5f * Width / MathUtils.Tan(num / 2f);
                var v4 = 0.5f * v2 * Width - vector4 * s;
                var num2 = (float)Math.PI - Vector2.Angle(vector3, v);
                var s2 = 0.5f * Width / MathUtils.Tan(num2 / 2f);
                var v5 = 0.5f * v3 * Width - vector3 * s2;
                _vertices.Add(vector + v4);
                _vertices.Add(vector - v4);
                _vertices.Add(vector2 - v5);
                _vertices.Add(vector2 - v5);
                _vertices.Add(vector2 + v5);
                _vertices.Add(vector + v4);
                if (j != list.Count - 1)
                {
                    continue;
                }

                _vertices.Add(vector2 - 0.5f * ArrowWidth * v3);
                _vertices.Add(vector2 + 0.5f * ArrowWidth * v3);
                _vertices.Add(vector2 + 0.5f * ArrowWidth * vector3);
            }
        }

        if (_vertices.Count > 0)
        {
            var minX = _vertices[0].X;
            var minY = _vertices[0].Y;
            var maxX = _vertices[0].X;
            var maxY = _vertices[0].Y;
            foreach (var vertex in _vertices)
            {
                minX = Math.Min(minX, vertex.X);
                minY = Math.Min(minY, vertex.Y);
                maxX = Math.Max(maxX, vertex.X);
                maxY = Math.Max(maxY, vertex.Y);
            }

            if (AbsoluteCoordinates)
            {
                DesiredSize = new Vector2(maxX, maxY);
                _startOffset = Vector2.Zero;
            }
            else
            {
                DesiredSize = new Vector2(maxX - minX, maxY - minY);
                _startOffset = -new Vector2(minX, minY);
            }
        }
        else
        {
            DesiredSize = Vector2.Zero;
            _startOffset = Vector2.Zero;
        }
    }
}
