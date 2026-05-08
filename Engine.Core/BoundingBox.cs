namespace Engine.Core;

public struct BoundingBox : IEquatable<BoundingBox>
{
    private Vector3 _max;

    public Vector3 Max
    {
        get => _max;
        set
        {
            if (float.IsNaN(value.X) || float.IsNaN(value.Y) || float.IsNaN(value.Z))
            {
                throw new Exception("NaN");
            }

            _max = value;
        }
    }

    private Vector3 _min;

    public Vector3 Min
    {
        get => _min;
        set
        {
            if (float.IsNaN(value.X) || float.IsNaN(value.Y) || float.IsNaN(value.Z))
            {
                throw new Exception("NaN");
            }

            _min = value;
        }
    }


    public BoundingBox(float x1, float y1, float z1, float x2, float y2, float z2)
    {
        _min = new Vector3(x1, y1, z1);
        _max = new Vector3(x2, y2, z2);
        if (float.IsNaN(_min.X) || float.IsNaN(_min.Y) || float.IsNaN(_min.Z) || float.IsNaN(_max.X) ||
            float.IsNaN(_max.Y) || float.IsNaN(_max.Z))
        {
            throw new Exception("NaN");
        }
    }

    public BoundingBox(Vector3 min, Vector3 max)
    {
        _min = min;
        _max = max;
        if (float.IsNaN(_min.X) || float.IsNaN(_min.Y) || float.IsNaN(_min.Z) || float.IsNaN(_max.X) ||
            float.IsNaN(_max.Y) || float.IsNaN(_max.Z))
        {
            throw new Exception("NaN");
        }
    }

    public BoundingBox(IEnumerable<Vector3> points)
    {
        _min = new Vector3(float.MaxValue);
        _max = new Vector3(float.MinValue);
        if (float.IsNaN(_min.X) || float.IsNaN(_min.Y) || float.IsNaN(_min.Z) || float.IsNaN(_max.X) ||
            float.IsNaN(_max.Y) || float.IsNaN(_max.Z))
        {
            throw new Exception("NaN");
        }

        foreach (var point in points)
        {
            var currentMin = Min;
            var currentMax = Max;

            currentMin.X = MathUtils.Min(currentMin.X, point.X);
            currentMin.Y = MathUtils.Min(currentMin.Y, point.Y);
            currentMin.Z = MathUtils.Min(currentMin.Z, point.Z);

            currentMax.X = MathUtils.Max(currentMax.X, point.X);
            currentMax.Y = MathUtils.Max(currentMax.Y, point.Y);
            currentMax.Z = MathUtils.Max(currentMax.Z, point.Z);

            Min = currentMin;
            Max = currentMax;
        }

        if (Min.X.CloseTo(float.MaxValue))
        {
            throw new ArgumentException(null, nameof(points));
        }
    }

    public static implicit operator BoundingBox((float X1, float Y1, float Z1, float X2, float Y2, float Z2) v)
    {
        return new BoundingBox(v.X1, v.Y1, v.Z1, v.X2, v.Y2, v.Z2);
    }

    public override bool Equals(object? obj)
    {
        return obj is BoundingBox box && Equals(box);
    }

    public override int GetHashCode()
    {
        return Min.GetHashCode() + Max.GetHashCode();
    }

    public override string ToString()
    {
        return $"{Min},{Max}";
    }

    public bool Equals(BoundingBox other)
    {
        if (Min == other.Min)
        {
            return Max == other.Max;
        }

        return false;
    }

    public Vector3 Center()
    {
        return new Vector3(0.5f * (Min.X + Max.X), 0.5f * (Min.Y + Max.Y), 0.5f * (Min.Z + Max.Z));
    }

    public Vector3 Size()
    {
        return Max - Min;
    }

    public float Volume()
    {
        var vector = Size();
        return vector.X * vector.Y * vector.Z;
    }

    public bool Contains(Vector3 p)
    {
        if (p.X >= Min.X && p.X <= Max.X && p.Y >= Min.Y && p.Y <= Max.Y && p.Z >= Min.Z)
        {
            return p.Z <= Max.Z;
        }

        return false;
    }

    public bool Intersection(BoundingBox box)
    {
        if (box.Max.X >= Min.X && box.Min.X <= Max.X && box.Max.Y >= Min.Y && box.Min.Y <= Max.Y && box.Max.Z >= Min.Z)
        {
            return box.Min.Z <= Max.Z;
        }

        return false;
    }

    public bool Intersection(BoundingSphere sphere)
    {
        if (sphere.Center.X - Min.X > sphere.Radius && sphere.Center.Y - Min.Y > sphere.Radius &&
            sphere.Center.Z - Min.Z > sphere.Radius && Max.X - sphere.Center.X > sphere.Radius &&
            Max.Y - sphere.Center.Y > sphere.Radius && Max.Z - sphere.Center.Z > sphere.Radius)
        {
            return true;
        }

        var num = 0f;
        if (sphere.Center.X - Min.X <= sphere.Radius)
        {
            num += (sphere.Center.X - Min.X) * (sphere.Center.X - Min.X);
        }
        else if (Max.X - sphere.Center.X <= sphere.Radius)
        {
            num += (sphere.Center.X - Max.X) * (sphere.Center.X - Max.X);
        }

        if (sphere.Center.Y - Min.Y <= sphere.Radius)
        {
            num += (sphere.Center.Y - Min.Y) * (sphere.Center.Y - Min.Y);
        }
        else if (Max.Y - sphere.Center.Y <= sphere.Radius)
        {
            num += (sphere.Center.Y - Max.Y) * (sphere.Center.Y - Max.Y);
        }

        if (sphere.Center.Z - Min.Z <= sphere.Radius)
        {
            num += (sphere.Center.Z - Min.Z) * (sphere.Center.Z - Min.Z);
        }
        else if (Max.Z - sphere.Center.Z <= sphere.Radius)
        {
            num += (sphere.Center.Z - Max.Z) * (sphere.Center.Z - Max.Z);
        }

        if (num <= sphere.Radius * sphere.Radius)
        {
            return true;
        }

        return false;
    }

    public static BoundingBox Intersection(BoundingBox b1, BoundingBox b2)
    {
        var min = Vector3.Max(b1.Min, b2.Min);
        var max = Vector3.Min(b1.Max, b2.Max);
        if (!(max.X > min.X) || !(max.Y > min.Y) || !(max.Z > min.Z))
        {
            return default;
        }

        return new BoundingBox(min, max);
    }

    public static BoundingBox Union(BoundingBox b1, BoundingBox b2)
    {
        var min = Vector3.Min(b1.Min, b2.Min);
        var max = Vector3.Max(b1.Max, b2.Max);
        return new BoundingBox(min, max);
    }

    public static BoundingBox Union(BoundingBox b, Vector3 p)
    {
        var min = Vector3.Min(b.Min, p);
        var max = Vector3.Max(b.Max, p);
        return new BoundingBox(min, max);
    }

    public static float Distance(BoundingBox b, Vector3 p)
    {
        var num = MathUtils.Max(b.Min.X - p.X, 0f, p.X - b.Max.X);
        var num2 = MathUtils.Max(b.Min.Y - p.Y, 0f, p.Y - b.Max.Y);
        var num3 = MathUtils.Max(b.Min.Z - p.Z, 0f, p.Z - b.Max.Z);
        return MathUtils.Sqrt(num * num + num2 * num2 + num3 * num3);
    }

    public static BoundingBox Transform(BoundingBox b, Matrix m)
    {
        Transform(ref b, ref m, out var result);
        return result;
    }

    public static void Transform(ref BoundingBox b, ref Matrix m, out BoundingBox result)
    {
        var sourceArray = new Vector3[]
        {
            new(b.Min.X, b.Min.Y, b.Min.Z),
            new(b.Max.X, b.Min.Y, b.Min.Z),
            new(b.Min.X, b.Max.Y, b.Min.Z),
            new(b.Max.X, b.Max.Y, b.Min.Z),
            new(b.Min.X, b.Min.Y, b.Max.Z),
            new(b.Max.X, b.Min.Y, b.Max.Z),
            new(b.Min.X, b.Max.Y, b.Max.Z),
            new(b.Max.X, b.Max.Y, b.Max.Z)
        };
        var array = new Vector3[8];
        Vector3.Transform(sourceArray, 0, ref m, array, 0, 8);
        result = new BoundingBox(array);
    }

    public static bool operator ==(BoundingBox a, BoundingBox b)
    {
        return a.Equals(b);
    }

    public static bool operator !=(BoundingBox a, BoundingBox b)
    {
        return !a.Equals(b);
    }
}
