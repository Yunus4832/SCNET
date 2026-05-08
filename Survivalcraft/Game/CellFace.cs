namespace Game;

public struct CellFace : IEquatable<CellFace>
{
    public int X;

    public int Y;

    public int Z;

    public int Face;

    private static readonly int[] _oppositeFaceArray =
    [
        2,
        3,
        0,
        1,
        5,
        4
    ];

    private static readonly Point3[] _faceToPoint3Array =
    [
        new(0, 0, 1),
        new(1, 0, 0),
        new(0, 0, -1),
        new(-1, 0, 0),
        new(0, 1, 0),
        new(0, -1, 0)
    ];

    private static readonly Vector3[] _faceToVector3Array =
    [
        new(0f, 0f, 1f),
        new(1f, 0f, 0f),
        new(0f, 0f, -1f),
        new(-1f, 0f, 0f),
        new(0f, 1f, 0f),
        new(0f, -1f, 0f)
    ];

    public Point3 Point
    {
        get => new(X, Y, Z);
        set
        {
            X = value.X;
            Y = value.Y;
            Z = value.Z;
        }
    }

    public CellFace(int x, int y, int z, int face)
    {
        X = x;
        Y = y;
        Z = z;
        Face = face;
    }

    public static int OppositeFace(int face)
    {
        return _oppositeFaceArray[face];
    }

    public static Point3 FaceToPoint3(int face)
    {
        return _faceToPoint3Array[face];
    }

    public static Vector3 FaceToVector3(int face)
    {
        return _faceToVector3Array[face];
    }

    public static int Point3ToFace(Point3 p, int maxFace = 5)
    {
        maxFace = MathUtils.Clamp(maxFace, 0, 5);
        for (var i = 0; i < maxFace; i++)
        {
            if (_faceToPoint3Array[i] == p)
            {
                return i;
            }
        }

        throw new InvalidOperationException("Invalid Point3.");
    }

    public static int Vector3ToFace(Vector3 v, int maxFace = 5)
    {
        maxFace = MathUtils.Clamp(maxFace, 0, 5);
        var num = -1f / 0f;
        var result = 0;
        for (var i = 0; i <= maxFace; i++)
        {
            var num2 = Vector3.Dot(_faceToVector3Array[i], v);
            if (!(num2 > num))
            {
                continue;
            }

            result = i;
            num = num2;
        }

        return result;
    }

    public static CellFace FromAxisAndDirection(int x, int y, int z, int axis, float direction)
    {
        CellFace result = default;
        result.X = x;
        result.Y = y;
        result.Z = z;
        result.Face = axis switch
        {
            0 => direction > 0f ? 1 : 3,
            1 => direction > 0f ? 4 : 5,
            2 => !(direction > 0f) ? 2 : 0,
            _ => result.Face
        };

        return result;
    }

    public Plane CalculatePlane()
    {
        return Face switch
        {
            0 => new Plane(new Vector3(0f, 0f, 1f), -(Z + 1)),
            1 => new Plane(new Vector3(-1f, 0f, 0f), X + 1),
            2 => new Plane(new Vector3(0f, 0f, -1f), Z),
            3 => new Plane(new Vector3(1f, 0f, 0f), -X),
            4 => new Plane(new Vector3(0f, 1f, 0f), -(Y + 1)),
            _ => new Plane(new Vector3(0f, -1f, 0f), Y)
        };
    }

    public override int GetHashCode()
    {
        return (X << 11) + (Y << 7) + (Z << 3) + Face;
    }

    public override bool Equals(object? obj)
    {
        return obj is CellFace face && Equals(face);
    }

    public bool Equals(CellFace other)
    {
        if (other.X == X && other.Y == Y && other.Z == Z)
        {
            return other.Face == Face;
        }

        return false;
    }

    public override string ToString()
    {
        return X + ", " + Y + ", " + Z + ", face " + Face;
    }

    public static bool operator ==(CellFace c1, CellFace c2)
    {
        return c1.Equals(c2);
    }

    public static bool operator !=(CellFace c1, CellFace c2)
    {
        return !c1.Equals(c2);
    }
}
