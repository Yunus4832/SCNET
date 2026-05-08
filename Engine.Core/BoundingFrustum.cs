namespace Engine.Core;

public class BoundingFrustum : IEquatable<BoundingFrustum>
{
    private readonly Plane[] _planes = new Plane[6];

    private Matrix _viewProjection;

    public BoundingFrustum(Matrix viewProjection)
    {
        Matrix = viewProjection;
    }

    public Plane Near => _planes[0];

    public Plane Far => _planes[1];

    public Plane Left => _planes[2];

    public Plane Right => _planes[3];

    public Plane Top => _planes[4];

    public Plane Bottom => _planes[5];

    public Matrix Matrix
    {
        get => _viewProjection;
        set
        {
            _viewProjection = value;
            _planes[0].Normal.X = 0f - value.M13;
            _planes[0].Normal.Y = 0f - value.M23;
            _planes[0].Normal.Z = 0f - value.M33;
            _planes[0].D = 0f - value.M43;
            _planes[1].Normal.X = 0f - value.M14 + value.M13;
            _planes[1].Normal.Y = 0f - value.M24 + value.M23;
            _planes[1].Normal.Z = 0f - value.M34 + value.M33;
            _planes[1].D = 0f - value.M44 + value.M43;
            _planes[2].Normal.X = 0f - value.M14 - value.M11;
            _planes[2].Normal.Y = 0f - value.M24 - value.M21;
            _planes[2].Normal.Z = 0f - value.M34 - value.M31;
            _planes[2].D = 0f - value.M44 - value.M41;
            _planes[3].Normal.X = 0f - value.M14 + value.M11;
            _planes[3].Normal.Y = 0f - value.M24 + value.M21;
            _planes[3].Normal.Z = 0f - value.M34 + value.M31;
            _planes[3].D = 0f - value.M44 + value.M41;
            _planes[4].Normal.X = 0f - value.M14 + value.M12;
            _planes[4].Normal.Y = 0f - value.M24 + value.M22;
            _planes[4].Normal.Z = 0f - value.M34 + value.M32;
            _planes[4].D = 0f - value.M44 + value.M42;
            _planes[5].Normal.X = 0f - value.M14 - value.M12;
            _planes[5].Normal.Y = 0f - value.M24 - value.M22;
            _planes[5].Normal.Z = 0f - value.M34 - value.M32;
            _planes[5].D = 0f - value.M44 - value.M42;
            for (var i = 0; i < 6; i++)
            {
                var num = _planes[i].Normal.Length();
                _planes[i].Normal /= num;
                _planes[i].D /= num;
            }
        }
    }

    public bool Equals(BoundingFrustum? other)
    {
        if (other is null)
        {
            return false;
        }

        return _viewProjection == other._viewProjection;
    }

    public override bool Equals(object? obj)
    {
        if (obj is not BoundingFrustum boundingFrustum)
        {
            return false;
        }

        return _viewProjection == boundingFrustum._viewProjection;
    }

    public override int GetHashCode() => _viewProjection.GetHashCode();

    public override string ToString()
    {
        return _viewProjection.ToString();
    }

    public Vector3[] FindCorners()
    {
        var array = new Vector3[8];
        var ray = ComputeIntersectionLine(_planes[0], _planes[2]);
        array[0] = ComputeIntersection(_planes[4], ray);
        array[3] = ComputeIntersection(_planes[5], ray);
        ray = ComputeIntersectionLine(_planes[3], _planes[0]);
        array[1] = ComputeIntersection(_planes[4], ray);
        array[2] = ComputeIntersection(_planes[5], ray);
        ray = ComputeIntersectionLine(_planes[2], _planes[1]);
        array[4] = ComputeIntersection(_planes[4], ray);
        array[7] = ComputeIntersection(_planes[5], ray);
        ray = ComputeIntersectionLine(_planes[1], _planes[3]);
        array[5] = ComputeIntersection(_planes[4], ray);
        array[6] = ComputeIntersection(_planes[5], ray);
        return array;
    }

    public bool Intersection(Vector3 point)
    {
        for (var i = 0; i < _planes.Length; i++)
        {
            var x = _planes[i].Normal.X;
            var y = _planes[i].Normal.Y;
            var z = _planes[i].Normal.Z;
            var d = _planes[i].D;
            if (x * point.X + y * point.Y + z * point.Z + d > 0f)
            {
                return false;
            }
        }

        return true;
    }

    public bool Intersection(BoundingSphere sphere)
    {
        for (var i = 0; i < _planes.Length; i++)
        {
            var x = _planes[i].Normal.X;
            var y = _planes[i].Normal.Y;
            var z = _planes[i].Normal.Z;
            var d = _planes[i].D;
            if (x * sphere.Center.X + y * sphere.Center.Y + z * sphere.Center.Z + d > sphere.Radius)
            {
                return false;
            }
        }

        return true;
    }

    public bool Intersection(BoundingBox box)
    {
        for (var i = 0; i < _planes.Length; i++)
        {
            var x = _planes[i].Normal.X;
            var y = _planes[i].Normal.Y;
            var z = _planes[i].Normal.Z;
            var d = _planes[i].D;
            var num = x > 0f ? box.Min.X : box.Max.X;
            var num2 = y > 0f ? box.Min.Y : box.Max.Y;
            var num3 = z > 0f ? box.Min.Z : box.Max.Z;
            if (x * num + y * num2 + z * num3 + d > 0f)
            {
                return false;
            }
        }

        return true;
    }

    public static bool operator ==(BoundingFrustum? f1, BoundingFrustum? f2)
    {
        return Equals(f1, f2);
    }

    public static bool operator !=(BoundingFrustum? f1, BoundingFrustum? f2)
    {
        return !Equals(f1, f2);
    }

    private static Vector3 ComputeIntersection(Plane plane, Ray3 ray)
    {
        var s = (0f - plane.D - Vector3.Dot(plane.Normal, ray.Position)) / Vector3.Dot(plane.Normal, ray.Direction);
        return ray.Position + ray.Direction * s;
    }

    private static Ray3 ComputeIntersectionLine(Plane p1, Plane p2)
    {
        var result = default(Ray3);
        result.Direction = Vector3.Cross(p1.Normal, p2.Normal);
        var d = result.Direction.LengthSquared();
        result.Position = Vector3.Cross((0f - p1.D) * p2.Normal + p2.D * p1.Normal, result.Direction) / d;
        return result;
    }
}
