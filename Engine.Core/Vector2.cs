namespace Engine.Core;

public struct Vector2(float x, float y) : IEquatable<Vector2>
{
    public float X = x;

    public float Y = y;

    public static readonly Vector2 Zero = new(0f);

    public static readonly Vector2 One = new(1f);

    public static readonly Vector2 UnitX = new(1f, 0f);

    public static readonly Vector2 UnitY = new(0f, 1f);

    public Vector2 YX
    {
        get => new(Y, X);
        set
        {
            Y = value.X;
            X = value.Y;
        }
    }

    public Vector2(float v) : this(v, v)
    {
    }

    public Vector2(Point2 p) : this(p.X, p.Y)
    {
    }

    public static implicit operator Vector2((float X, float Y) v)
    {
        return new Vector2(v.X, v.Y);
    }

    public override bool Equals(object? obj)
    {
        return obj is Vector2 vector2 && Equals(vector2);
    }

    public override int GetHashCode()
    {
        return X.GetHashCode() + Y.GetHashCode();
    }

    public override string ToString()
    {
        return $"{X},{Y}";
    }

    public bool Equals(Vector2 other)
    {
        return X.CloseTo(other.X) && Y.CloseTo(other.Y);
    }

    public static Vector2 CreateFromAngle(float angle)
    {
        var y = MathUtils.Cos(angle);
        return new Vector2(0f - MathUtils.Sin(angle), y);
    }

    public static float Distance(Vector2 v1, Vector2 v2)
    {
        return MathUtils.Sqrt(DistanceSquared(v1, v2));
    }

    public static float DistanceSquared(Vector2 v1, Vector2 v2)
    {
        return MathUtils.Sqr(v1.X - v2.X) + MathUtils.Sqr(v1.Y - v2.Y);
    }

    public static float Dot(Vector2 v1, Vector2 v2)
    {
        return v1.X * v2.X + v1.Y * v2.Y;
    }

    public static float Cross(Vector2 v1, Vector2 v2)
    {
        return v1.X * v2.Y - v1.Y * v2.X;
    }

    public static Vector2 Perpendicular(Vector2 v)
    {
        return new Vector2(0f - v.Y, v.X);
    }

    public static Vector2 Rotate(Vector2 v, float angle)
    {
        var num = MathUtils.Cos(angle);
        var num2 = MathUtils.Sin(angle);
        return new Vector2(num * v.X + num2 * v.Y, (0f - num2) * v.X + num * v.Y);
    }

    public float Length()
    {
        return MathUtils.Sqrt(X * X + Y * Y);
    }

    public float LengthSquared()
    {
        return X * X + Y * Y;
    }

    public static Vector2 Floor(Vector2 v)
    {
        return new Vector2(MathUtils.Floor(v.X), MathUtils.Floor(v.Y));
    }

    public static Vector2 Ceiling(Vector2 v)
    {
        return new Vector2(MathUtils.Ceiling(v.X), MathUtils.Ceiling(v.Y));
    }

    public static Vector2 Round(Vector2 v)
    {
        return new Vector2(MathUtils.Round(v.X), MathUtils.Round(v.Y));
    }

    public static Vector2 Min(Vector2 v, float f)
    {
        return new Vector2(MathUtils.Min(v.X, f), MathUtils.Min(v.Y, f));
    }

    public static Vector2 Min(Vector2 v1, Vector2 v2)
    {
        return new Vector2(MathUtils.Min(v1.X, v2.X), MathUtils.Min(v1.Y, v2.Y));
    }

    public static Vector2 Max(Vector2 v, float f)
    {
        return new Vector2(MathUtils.Max(v.X, f), MathUtils.Max(v.Y, f));
    }

    public static Vector2 Max(Vector2 v1, Vector2 v2)
    {
        return new Vector2(MathUtils.Max(v1.X, v2.X), MathUtils.Max(v1.Y, v2.Y));
    }

    public static Vector2 Clamp(Vector2 v, float min, float max)
    {
        return new Vector2(MathUtils.Clamp(v.X, min, max), MathUtils.Clamp(v.Y, min, max));
    }

    public static Vector2 Saturate(Vector2 v)
    {
        return new Vector2(MathUtils.Saturate(v.X), MathUtils.Saturate(v.Y));
    }

    public static Vector2 Lerp(Vector2 v1, Vector2 v2, float f)
    {
        return new Vector2(MathUtils.Lerp(v1.X, v2.X, f), MathUtils.Lerp(v1.Y, v2.Y, f));
    }

    public static Vector2 CatmullRom(Vector2 v1, Vector2 v2, Vector2 v3, Vector2 v4, float f)
    {
        return new Vector2(MathUtils.CatmullRom(v1.X, v2.X, v3.X, v4.X, f),
            MathUtils.CatmullRom(v1.Y, v2.Y, v3.Y, v4.Y, f));
    }

    public static Vector2 Normalize(Vector2 v)
    {
        var num = v.Length();
        if (!(num > 0f))
        {
            return UnitX;
        }

        return v / num;
    }

    public static Vector2 LimitLength(Vector2 v, float maxLength)
    {
        var num = v.LengthSquared();
        if (num > maxLength * maxLength)
        {
            return v * (maxLength / MathUtils.Sqrt(num));
        }

        return v;
    }

    public static float Angle(Vector2 v1, Vector2 v2)
    {
        var num = MathUtils.Atan2(v1.Y, v1.X);
        var num2 = MathUtils.Atan2(v2.Y, v2.X) - num;
        if (num2 > (float)Math.PI)
        {
            num2 -= (float)Math.PI * 2f;
        }
        else if (num2 <= -(float)Math.PI)
        {
            num2 += (float)Math.PI * 2f;
        }

        return num2;
    }

    public static Vector2 Transform(Vector2 v, Matrix m)
    {
        return new Vector2(v.X * m.M11 + v.Y * m.M21 + m.M41, v.X * m.M12 + v.Y * m.M22 + m.M42);
    }

    public static void Transform(ref Vector2 v, ref Matrix m, out Vector2 result)
    {
        result = new Vector2(v.X * m.M11 + v.Y * m.M21 + m.M41, v.X * m.M12 + v.Y * m.M22 + m.M42);
    }

    public static Vector2 Transform(Vector2 v, Quaternion q)
    {
        var num = q.X + q.X;
        var num2 = q.Y + q.Y;
        var num3 = q.Z + q.Z;
        var num4 = q.W * num3;
        var num5 = q.X * num;
        var num6 = q.X * num2;
        var num7 = q.Y * num2;
        var num8 = q.Z * num3;
        return new Vector2(v.X * (1f - num7 - num8) + v.Y * (num6 - num4),
            v.X * (num6 + num4) + v.Y * (1f - num5 - num8));
    }

    public static void Transform(ref Vector2 v, ref Quaternion q, out Vector2 result)
    {
        var num = q.X + q.X;
        var num2 = q.Y + q.Y;
        var num3 = q.Z + q.Z;
        var num4 = q.W * num3;
        var num5 = q.X * num;
        var num6 = q.X * num2;
        var num7 = q.Y * num2;
        var num8 = q.Z * num3;
        result = new Vector2(v.X * (1f - num7 - num8) + v.Y * (num6 - num4),
            v.X * (num6 + num4) + v.Y * (1f - num5 - num8));
    }

    public static void Transform(Vector2[] sourceArray, int sourceIndex, ref Matrix m, Vector2[] destinationArray,
        int destinationIndex, int count)
    {
        for (var i = 0; i < count; i++)
        {
            var vector = sourceArray[sourceIndex + i];
            destinationArray[destinationIndex + i] = new Vector2(vector.X * m.M11 + vector.Y * m.M21 + m.M41,
                vector.X * m.M12 + vector.Y * m.M22 + m.M42);
        }
    }

    public static Vector2 TransformNormal(Vector2 v, Matrix m)
    {
        return new Vector2(v.X * m.M11 + v.Y * m.M21, v.X * m.M12 + v.Y * m.M22);
    }

    public static void TransformNormal(ref Vector2 v, ref Matrix m, out Vector2 result)
    {
        result = new Vector2(v.X * m.M11 + v.Y * m.M21, v.X * m.M12 + v.Y * m.M22);
    }

    public static void TransformNormal(Vector2[] sourceArray, int sourceIndex, ref Matrix m, Vector2[] destinationArray,
        int destinationIndex, int count)
    {
        for (var i = 0; i < count; i++)
        {
            var vector = sourceArray[sourceIndex + i];
            destinationArray[destinationIndex + i] = new Vector2(vector.X * m.M11 + vector.Y * m.M21,
                vector.X * m.M12 + vector.Y * m.M22);
        }
    }

    public static bool operator ==(Vector2 v1, Vector2 v2)
    {
        return v1.Equals(v2);
    }

    public static bool operator !=(Vector2 v1, Vector2 v2)
    {
        return !v1.Equals(v2);
    }

    public static Vector2 operator +(Vector2 v)
    {
        return v;
    }

    public static Vector2 operator -(Vector2 v)
    {
        return new Vector2(0f - v.X, 0f - v.Y);
    }

    public static Vector2 operator +(Vector2 v1, Vector2 v2)
    {
        return new Vector2(v1.X + v2.X, v1.Y + v2.Y);
    }

    public static Vector2 operator -(Vector2 v1, Vector2 v2)
    {
        return new Vector2(v1.X - v2.X, v1.Y - v2.Y);
    }

    public static Vector2 operator *(Vector2 v1, Vector2 v2)
    {
        return new Vector2(v1.X * v2.X, v1.Y * v2.Y);
    }

    public static Vector2 operator *(Vector2 v, float s)
    {
        return new Vector2(v.X * s, v.Y * s);
    }

    public static Vector2 operator *(float s, Vector2 v)
    {
        return new Vector2(v.X * s, v.Y * s);
    }

    public static Vector2 operator /(Vector2 v1, Vector2 v2)
    {
        return new Vector2(v1.X / v2.X, v1.Y / v2.Y);
    }

    public static Vector2 operator /(Vector2 v, float d)
    {
        var num = 1f / d;
        return new Vector2(v.X * num, v.Y * num);
    }
}
