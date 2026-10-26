namespace Engine.Core.Mathematics;

public struct FloatCurve(params Vector2[] points)
{
    public Vector2[] Points = points.ToArray();

    public float Sample(float x)
    {
        if (Points == null || Points.Length == 0)
        {
            return 0f;
        }

        var num = -1;
        for (var i = 0; i < Points.Length; i++)
        {
            if (!(Points[i].X > x))
            {
                continue;
            }

            num = i;
            break;
        }

        if (num < 0)
        {
            return Points[^1].Y;
        }

        if (num == 0)
        {
            return Points[0].Y;
        }

        var vector = Points[num - 1];
        var vector2 = Points[num];
        return MathUtils.Lerp(vector.Y, vector2.Y, MathUtils.LinearStep(vector.X, vector2.X, x));
    }
}
