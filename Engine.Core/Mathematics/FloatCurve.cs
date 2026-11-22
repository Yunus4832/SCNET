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

        var upperPointIndex = -1;
        for (var i = 0; i < Points.Length; i++)
        {
            if (!(Points[i].X > x))
            {
                continue;
            }

            upperPointIndex = i;
            break;
        }

        if (upperPointIndex < 0)
        {
            return Points[^1].Y;
        }

        if (upperPointIndex == 0)
        {
            return Points[0].Y;
        }

        var vector = Points[upperPointIndex - 1];
        var upperPoint = Points[upperPointIndex];
        return MathUtils.Lerp(vector.Y, upperPoint.Y, MathUtils.LinearStep(vector.X, upperPoint.X, x));
    }
}
