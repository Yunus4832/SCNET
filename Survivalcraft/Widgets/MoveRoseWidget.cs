using Engine.Input;

namespace Game.Widgets;

public class MoveRoseWidget : Widget
{
    private Vector3 _direction;

    private bool _jump;

    private int? _jumpTouchId;

    public Vector3 Direction
    {
        get
        {
            if (IsEnabledGlobal && IsVisibleGlobal)
            {
                return _direction;
            }

            return Vector3.Zero;
        }
    }

    public bool Jump
    {
        get
        {
            if (IsEnabledGlobal && IsVisibleGlobal)
            {
                return _jump;
            }

            return false;
        }
    }

    public override void Update()
    {
        _direction = Vector3.Zero;
        _jump = false;
        var v = ActualSize / 2f;
        var num = ActualSize.X / 2f;
        var num2 = num / 3.5f;
        var num3 = MathUtils.DegToRad(35f);
        foreach (var touchLocation in Input.TouchLocations)
        {
            if (HitTestGlobal(touchLocation.Position) == this)
            {
                if (touchLocation.State == TouchLocationState.Pressed &&
                    Vector2.Distance(ScreenToWidget(touchLocation.Position), v) <= num2)
                {
                    _jump = true;
                    _jumpTouchId = touchLocation.Id;
                }

                if (touchLocation.State == TouchLocationState.Released && _jumpTouchId.HasValue &&
                    touchLocation.Id == _jumpTouchId.Value)
                {
                    _jumpTouchId = null;
                }

                if (touchLocation.State is TouchLocationState.Moved or TouchLocationState.Pressed)
                {
                    var v2 = ScreenToWidget(touchLocation.Position);
                    var num4 = Vector2.Distance(v2, v);
                    if (!(num4 > num2) || !(num4 <= num))
                    {
                        continue;
                    }

                    var num5 = Vector2.Angle(v2 - v, -Vector2.UnitY);
                    if (MathUtils.Abs(MathUtils.NormalizeAngle(num5 - 0f)) < num3)
                    {
                        _direction = _jumpTouchId.HasValue ? new Vector3(0f, 1f, 0f) : new Vector3(0f, 0f, 1f);
                    }
                    else if (MathUtils.Abs(MathUtils.NormalizeAngle(num5 - (float)Math.PI / 2f)) < num3)
                    {
                        _direction = new Vector3(-1f, 0f, 0f);
                    }
                    else if (MathUtils.Abs(MathUtils.NormalizeAngle(num5 - (float)Math.PI)) < num3)
                    {
                        _direction = _jumpTouchId.HasValue ? new Vector3(0f, -1f, 0f) : new Vector3(0f, 0f, -1f);
                    }
                    else if (MathUtils.Abs(MathUtils.NormalizeAngle(num5 - 4.712389f)) < num3)
                    {
                        _direction = new Vector3(1f, 0f, 0f);
                    }
                }
            }
        }
    }

    protected override void MeasureOverride(Vector2 parentAvailableSize)
    {
        IsDrawRequired = true;
    }

    public override void Draw(DrawContext dc)
    {
        var subtexture = ContentManager.Get<Subtexture>("Textures/Atlas/MoveRose");
        var subtexture2 = ContentManager.Get<Subtexture>("Textures/Atlas/MoveRose_Pressed");
        var texturedBatch2D = dc.PrimitivesRenderer2D.TexturedBatch(subtexture.Texture);
        var texturedBatch2D2 = dc.PrimitivesRenderer2D.TexturedBatch(subtexture2.Texture);
        var count = texturedBatch2D.TriangleVertices.Count;
        var count2 = texturedBatch2D2.TriangleVertices.Count;
        var p = ActualSize / 2f;
        var vector = new Vector2(0f, 0f);
        var vector2 = new Vector2(ActualSize.X, 0f);
        var vector3 = new Vector2(ActualSize.X, ActualSize.Y);
        var vector4 = new Vector2(0f, ActualSize.Y);
        if (_direction.Z > 0f)
        {
            var subtextureCoords = GetSubtextureCoords(subtexture2, new Vector2(0f, 0f));
            var subtextureCoords2 = GetSubtextureCoords(subtexture2, new Vector2(1f, 0f));
            var subtextureCoords3 = GetSubtextureCoords(subtexture2, new Vector2(0.5f, 0.5f));
            texturedBatch2D2.QueueTriangle(vector, vector2, p, 0f, subtextureCoords, subtextureCoords2,
                subtextureCoords3, GlobalColorTransform);
        }
        else
        {
            var subtextureCoords4 = GetSubtextureCoords(subtexture, new Vector2(0f, 0f));
            var subtextureCoords5 = GetSubtextureCoords(subtexture, new Vector2(1f, 0f));
            var subtextureCoords6 = GetSubtextureCoords(subtexture, new Vector2(0.5f, 0.5f));
            texturedBatch2D.QueueTriangle(vector, vector2, p, 0f, subtextureCoords4, subtextureCoords5,
                subtextureCoords6, GlobalColorTransform);
        }

        if (_direction.X > 0f)
        {
            var subtextureCoords7 = GetSubtextureCoords(subtexture2, new Vector2(1f, 0f));
            var subtextureCoords8 = GetSubtextureCoords(subtexture2, new Vector2(1f, 1f));
            var subtextureCoords9 = GetSubtextureCoords(subtexture2, new Vector2(0.5f, 0.5f));
            texturedBatch2D2.QueueTriangle(vector2, vector3, p, 0f, subtextureCoords7, subtextureCoords8,
                subtextureCoords9, GlobalColorTransform);
        }
        else
        {
            var subtextureCoords10 = GetSubtextureCoords(subtexture, new Vector2(1f, 0f));
            var subtextureCoords11 = GetSubtextureCoords(subtexture, new Vector2(1f, 1f));
            var subtextureCoords12 = GetSubtextureCoords(subtexture, new Vector2(0.5f, 0.5f));
            texturedBatch2D.QueueTriangle(vector2, vector3, p, 0f, subtextureCoords10, subtextureCoords11,
                subtextureCoords12, GlobalColorTransform);
        }

        if (_direction.Z < 0f)
        {
            var subtextureCoords13 = GetSubtextureCoords(subtexture2, new Vector2(1f, 1f));
            var subtextureCoords14 = GetSubtextureCoords(subtexture2, new Vector2(0f, 1f));
            var subtextureCoords15 = GetSubtextureCoords(subtexture2, new Vector2(0.5f, 0.5f));
            texturedBatch2D2.QueueTriangle(vector3, vector4, p, 0f, subtextureCoords13, subtextureCoords14,
                subtextureCoords15, GlobalColorTransform);
        }
        else
        {
            var subtextureCoords16 = GetSubtextureCoords(subtexture, new Vector2(1f, 1f));
            var subtextureCoords17 = GetSubtextureCoords(subtexture, new Vector2(0f, 1f));
            var subtextureCoords18 = GetSubtextureCoords(subtexture, new Vector2(0.5f, 0.5f));
            texturedBatch2D.QueueTriangle(vector3, vector4, p, 0f, subtextureCoords16, subtextureCoords17,
                subtextureCoords18, GlobalColorTransform);
        }

        if (_direction.X < 0f)
        {
            var subtextureCoords19 = GetSubtextureCoords(subtexture2, new Vector2(0f, 1f));
            var subtextureCoords20 = GetSubtextureCoords(subtexture2, new Vector2(0f, 0f));
            var subtextureCoords21 = GetSubtextureCoords(subtexture2, new Vector2(0.5f, 0.5f));
            texturedBatch2D2.QueueTriangle(vector4, vector, p, 0f, subtextureCoords19, subtextureCoords20,
                subtextureCoords21, GlobalColorTransform);
        }
        else
        {
            var subtextureCoords22 = GetSubtextureCoords(subtexture, new Vector2(0f, 1f));
            var subtextureCoords23 = GetSubtextureCoords(subtexture, new Vector2(0f, 0f));
            var subtextureCoords24 = GetSubtextureCoords(subtexture, new Vector2(0.5f, 0.5f));
            texturedBatch2D.QueueTriangle(vector4, vector, p, 0f, subtextureCoords22, subtextureCoords23,
                subtextureCoords24, GlobalColorTransform);
        }

        if (texturedBatch2D == texturedBatch2D2)
        {
            texturedBatch2D.TransformTriangles(GlobalTransform, count);
            return;
        }

        texturedBatch2D.TransformTriangles(GlobalTransform, count);
        texturedBatch2D2.TransformTriangles(GlobalTransform, count2);
    }

    public static Vector2 GetSubtextureCoords(Subtexture subtexture, Vector2 texCoords)
    {
        return new Vector2(MathUtils.Lerp(subtexture.TopLeft.X, subtexture.BottomRight.X, texCoords.X),
            MathUtils.Lerp(subtexture.TopLeft.Y, subtexture.BottomRight.Y, texCoords.Y));
    }
}
