using System.Runtime.Intrinsics;

namespace Engine.Core.Mathematics;

public struct Matrix(
    float m11,
    float m12,
    float m13,
    float m14,
    float m21,
    float m22,
    float m23,
    float m24,
    float m31,
    float m32,
    float m33,
    float m34,
    float m41,
    float m42,
    float m43,
    float m44
)
    : IEquatable<Matrix>
{
    public float M11 = m11;

    public float M21 = m21;

    public float M31 = m31;

    public float M41 = m41;

    public float M12 = m12;

    public float M22 = m22;

    public float M32 = m32;

    public float M42 = m42;

    public float M13 = m13;

    public float M23 = m23;

    public float M33 = m33;

    public float M43 = m43;

    public float M14 = m14;

    public float M24 = m24;

    public float M34 = m34;

    public float M44 = m44;

    public static readonly Matrix Zero = default;

    public static readonly Matrix Identity = new(1f, 0f, 0f, 0f, 0f, 1f, 0f, 0f, 0f, 0f, 1f, 0f, 0f, 0f, 0f, 1f);

    public Vector3 Right
    {
        get => new(M11, M12, M13);
        set
        {
            M11 = value.X;
            M12 = value.Y;
            M13 = value.Z;
        }
    }

    public Vector3 Up
    {
        get => new(M21, M22, M23);
        set
        {
            M21 = value.X;
            M22 = value.Y;
            M23 = value.Z;
        }
    }

    public Vector3 Forward
    {
        get => new(0f - M31, 0f - M32, 0f - M33);
        set
        {
            M31 = 0f - value.X;
            M32 = 0f - value.Y;
            M33 = 0f - value.Z;
        }
    }

    public Vector3 Translation
    {
        get => new(M41, M42, M43);
        set
        {
            M41 = value.X;
            M42 = value.Y;
            M43 = value.Z;
        }
    }

    public Matrix TranslationMatrix
    {
        get => new(1f, 0f, 0f, 0f, 0f, 1f, 0f, 0f, 0f, 0f, 1f, 0f, M41, M42, M43, 1f);
        set
        {
            M41 = value.M41;
            M42 = value.M42;
            M43 = value.M43;
        }
    }

    public Matrix OrientationMatrix
    {
        get => new(M11, M12, M13, 0f, M21, M22, M23, 0f, M31, M32, M33, 0f, 0f, 0f, 0f, 1f);
        set
        {
            M11 = value.M11;
            M12 = value.M12;
            M13 = value.M13;
            M21 = value.M21;
            M22 = value.M22;
            M23 = value.M23;
            M31 = value.M31;
            M32 = value.M32;
            M33 = value.M33;
        }
    }

    public override bool Equals(object? obj)
    {
        return obj is Matrix matrix && Equals(matrix);
    }

    public override int GetHashCode()
    {
        return M11.GetHashCode() +
               M12.GetHashCode() +
               M13.GetHashCode() +
               M14.GetHashCode() +
               M21.GetHashCode() +
               M22.GetHashCode() +
               M23.GetHashCode() +
               M24.GetHashCode() +
               M31.GetHashCode() +
               M32.GetHashCode() +
               M33.GetHashCode() +
               M34.GetHashCode() +
               M41.GetHashCode() +
               M42.GetHashCode() +
               M43.GetHashCode() +
               M44.GetHashCode();
    }

    public override string ToString()
    {
        return $"{M11},{M12},{M13},{M14}, {M21},{M22},{M23},{M24}, {M31},{M32},{M33},{M34}, {M41},{M42},{M43},{M44}";
    }

    public bool Equals(Matrix other)
    {
        return M11.CloseTo(other.M11) &&
               M22.CloseTo(other.M22) &&
               M33.CloseTo(other.M33) &&
               M44.CloseTo(other.M44) &&
               M12.CloseTo(other.M12) &&
               M13.CloseTo(other.M13) &&
               M14.CloseTo(other.M14) &&
               M21.CloseTo(other.M21) &&
               M23.CloseTo(other.M23) &&
               M24.CloseTo(other.M24) &&
               M31.CloseTo(other.M31) &&
               M32.CloseTo(other.M32) &&
               M34.CloseTo(other.M34) &&
               M41.CloseTo(other.M41) &&
               M42.CloseTo(other.M42) &&
               M43.CloseTo(other.M43);
    }

    public float Determinant()
    {
        var minorR34C34 = M33 * M44 - M34 * M43;
        var minorR34C24 = M32 * M44 - M34 * M42;
        var minorR34C23 = M32 * M43 - M33 * M42;
        var minorR34C14 = M31 * M44 - M34 * M41;
        var minorR34C13 = M31 * M43 - M33 * M41;
        var minorR34C12 = M31 * M42 - M32 * M41;
        return M11 * (M22 * minorR34C34 - M23 * minorR34C24 + M24 * minorR34C23) -
               M12 * (M21 * minorR34C34 - M23 * minorR34C14 + M24 * minorR34C13) +
               M13 * (M21 * minorR34C24 - M22 * minorR34C14 + M24 * minorR34C12) -
               M14 * (M21 * minorR34C23 - M22 * minorR34C13 + M23 * minorR34C12);
    }

    public Vector3 ToYawPitchRoll()
    {
        Decompose(out _, out var rotation, out _);
        return rotation.ToYawPitchRoll();
    }

    public bool Decompose(out Vector3 scale, out Quaternion rotation, out Vector3 translation)
    {
        translation.X = M41;
        translation.Y = M42;
        translation.Z = M43;
        float scaleSignX = !(M11 * M12 * M13 * M14 < 0f) ? 1 : -1;
        float scaleSignY = !(M21 * M22 * M23 * M24 < 0f) ? 1 : -1;
        float scaleSignZ = !(M31 * M32 * M33 * M34 < 0f) ? 1 : -1;
        scale.X = scaleSignX * MathUtils.Sqrt(M11 * M11 + M12 * M12 + M13 * M13);
        scale.Y = scaleSignY * MathUtils.Sqrt(M21 * M21 + M22 * M22 + M23 * M23);
        scale.Z = scaleSignZ * MathUtils.Sqrt(M31 * M31 + M32 * M32 + M33 * M33);
        if (scale.X == 0f || scale.Y == 0f || scale.Z == 0f)
        {
            rotation = Quaternion.Identity;
            return false;
        }

        rotation = Quaternion.CreateFromRotationMatrix(
            new Matrix(
                M11 / scale.X,
                M12 / scale.X,
                M13 / scale.X,
                0f,
                M21 / scale.Y,
                M22 / scale.Y,
                M23 / scale.Y,
                0f,
                M31 / scale.Z,
                M32 / scale.Z,
                M33 / scale.Z,
                0f,
                0f,
                0f,
                0f,
                1f
            )
        );
        return true;
    }

    public static Matrix CreateFromAxisAngle(Vector3 axis, float angle)
    {
        var x = axis.X;
        var y = axis.Y;
        var z = axis.Z;
        var sinAngle = MathUtils.Sin(angle);
        var cosAngle = MathUtils.Cos(angle);
        var xx = x * x;
        var yy = y * y;
        var zz = z * z;
        var xy = x * y;
        var xz = x * z;
        var yz = y * z;
        var result = default(Matrix);
        result.M11 = xx + cosAngle * (1f - xx);
        result.M12 = xy - cosAngle * xy + sinAngle * z;
        result.M13 = xz - cosAngle * xz - sinAngle * y;
        result.M14 = 0f;
        result.M21 = xy - cosAngle * xy - sinAngle * z;
        result.M22 = yy + cosAngle * (1f - yy);
        result.M23 = yz - cosAngle * yz + sinAngle * x;
        result.M24 = 0f;
        result.M31 = xz - cosAngle * xz + sinAngle * y;
        result.M32 = yz - cosAngle * yz - sinAngle * x;
        result.M33 = zz + cosAngle * (1f - zz);
        result.M34 = 0f;
        result.M41 = 0f;
        result.M42 = 0f;
        result.M43 = 0f;
        result.M44 = 1f;
        return result;
    }

    public static Matrix CreateFromQuaternion(Quaternion quaternion)
    {
        return quaternion.ToMatrix();
    }

    public static Matrix CreateFromYawPitchRoll(float yaw, float pitch, float roll)
    {
        return Quaternion.CreateFromYawPitchRoll(yaw, pitch, roll).ToMatrix();
    }

    public static Matrix CreateLookAt(Vector3 position, Vector3 target, Vector3 up)
    {
        var backward = Vector3.Normalize(position - target);
        var right = Vector3.Normalize(Vector3.Cross(up, backward));
        up = Vector3.Normalize(Vector3.Cross(backward, right));
        var result = default(Matrix);
        result.M11 = right.X;
        result.M12 = up.X;
        result.M13 = backward.X;
        result.M14 = 0f;
        result.M21 = right.Y;
        result.M22 = up.Y;
        result.M23 = backward.Y;
        result.M24 = 0f;
        result.M31 = right.Z;
        result.M32 = up.Z;
        result.M33 = backward.Z;
        result.M34 = 0f;
        result.M41 = 0f - Vector3.Dot(right, position);
        result.M42 = 0f - Vector3.Dot(up, position);
        result.M43 = 0f - Vector3.Dot(backward, position);
        result.M44 = 1f;
        return result;
    }

    public static Matrix CreateOrthographic(float width, float height, float nearPlane, float farPlane)
    {
        var result = default(Matrix);
        result.M11 = 2f / width;
        result.M12 = result.M13 = result.M14 = 0f;
        result.M22 = 2f / height;
        result.M21 = result.M23 = result.M24 = 0f;
        result.M33 = 1f / (nearPlane - farPlane);
        result.M31 = result.M32 = result.M34 = 0f;
        result.M41 = result.M42 = 0f;
        result.M43 = nearPlane / (nearPlane - farPlane);
        result.M44 = 1f;
        return result;
    }

    public static Matrix CreateOrthographicOffCenter(
        float left,
        float right,
        float bottom,
        float top,
        float nearPlane,
        float farPlane
    )
    {
        var result = default(Matrix);
        result.M11 = 2f / (right - left);
        result.M12 = 0f;
        result.M13 = 0f;
        result.M14 = 0f;
        result.M21 = 0f;
        result.M22 = 2f / (top - bottom);
        result.M23 = 0f;
        result.M24 = 0f;
        result.M31 = 0f;
        result.M32 = 0f;
        result.M33 = 1f / (nearPlane - farPlane);
        result.M34 = 0f;
        result.M41 = (left + right) / (left - right);
        result.M42 = (top + bottom) / (bottom - top);
        result.M43 = nearPlane / (nearPlane - farPlane);
        result.M44 = 1f;
        return result;
    }

    public static Matrix CreatePerspective(float width, float height, float nearPlane, float farPlane)
    {
        var result = default(Matrix);
        result.M11 = 2f * nearPlane / width;
        result.M12 = result.M13 = result.M14 = 0f;
        result.M22 = 2f * nearPlane / height;
        result.M21 = result.M23 = result.M24 = 0f;
        result.M33 = farPlane / (nearPlane - farPlane);
        result.M31 = result.M32 = 0f;
        result.M34 = -1f;
        result.M41 = result.M42 = result.M44 = 0f;
        result.M43 = nearPlane * farPlane / (nearPlane - farPlane);
        return result;
    }

    public static Matrix CreatePerspectiveFieldOfView(
        float fieldOfViewY,
        float aspectRatio,
        float nearPlane,
        float farPlane
    )
    {
        var verticalScale = 1f / MathUtils.Tan(fieldOfViewY * 0.5f);
        var result = default(Matrix);
        result.M11 = verticalScale / aspectRatio;
        result.M12 = result.M13 = result.M14 = 0f;
        result.M22 = verticalScale;
        result.M21 = result.M23 = result.M24 = 0f;
        result.M31 = result.M32 = 0f;
        result.M33 = farPlane / (nearPlane - farPlane);
        result.M34 = -1f;
        result.M41 = result.M42 = result.M44 = 0f;
        result.M43 = nearPlane * farPlane / (nearPlane - farPlane);
        return result;
    }

    public static Matrix CreatePerspectiveOffCenter(
        float left,
        float right,
        float bottom,
        float top,
        float nearPlane,
        float farPlane
    )
    {
        var result = default(Matrix);
        result.M11 = 2f * nearPlane / (right - left);
        result.M12 = result.M13 = result.M14 = 0f;
        result.M22 = 2f * nearPlane / (top - bottom);
        result.M21 = result.M23 = result.M24 = 0f;
        result.M31 = (left + right) / (right - left);
        result.M32 = (top + bottom) / (top - bottom);
        result.M33 = farPlane / (nearPlane - farPlane);
        result.M34 = -1f;
        result.M43 = nearPlane * farPlane / (nearPlane - farPlane);
        result.M41 = result.M42 = result.M44 = 0f;
        return result;
    }

    public static Matrix CreateRotationX(float radians)
    {
        var identity = Identity;
        var cosAngle = MathUtils.Cos(radians);
        var sinAngle = MathUtils.Sin(radians);
        identity.M22 = cosAngle;
        identity.M23 = sinAngle;
        identity.M32 = 0f - sinAngle;
        identity.M33 = cosAngle;
        return identity;
    }

    public static Matrix CreateRotationY(float radians)
    {
        var identity = Identity;
        var cosAngle = MathUtils.Cos(radians);
        var sinAngle = MathUtils.Sin(radians);
        identity.M11 = cosAngle;
        identity.M13 = 0f - sinAngle;
        identity.M31 = sinAngle;
        identity.M33 = cosAngle;
        return identity;
    }

    public static Matrix CreateRotationZ(float radians)
    {
        var identity = Identity;
        var cosAngle = MathUtils.Cos(radians);
        var sinAngle = MathUtils.Sin(radians);
        identity.M11 = cosAngle;
        identity.M12 = sinAngle;
        identity.M21 = 0f - sinAngle;
        identity.M22 = cosAngle;
        return identity;
    }

    public static Matrix CreateScale(float scale)
    {
        return new Matrix(scale, 0f, 0f, 0f, 0f, scale, 0f, 0f, 0f, 0f, scale, 0f, 0f, 0f, 0f, 1f);
    }

    public static Matrix CreateScale(float x, float y, float z)
    {
        return new Matrix(x, 0f, 0f, 0f, 0f, y, 0f, 0f, 0f, 0f, z, 0f, 0f, 0f, 0f, 1f);
    }

    public static Matrix CreateScale(Vector3 scale)
    {
        return new Matrix(scale.X, 0f, 0f, 0f, 0f, scale.Y, 0f, 0f, 0f, 0f, scale.Z, 0f, 0f, 0f, 0f, 1f);
    }

    public static Matrix CreateTranslation(float x, float y, float z)
    {
        return new Matrix(1f, 0f, 0f, 0f, 0f, 1f, 0f, 0f, 0f, 0f, 1f, 0f, x, y, z, 1f);
    }

    public static Matrix CreateTranslation(Vector3 position)
    {
        return new Matrix(1f, 0f, 0f, 0f, 0f, 1f, 0f, 0f, 0f, 0f, 1f, 0f, position.X, position.Y, position.Z, 1f);
    }

    public static Matrix CreateWorld(Vector3 position, Vector3 forward, Vector3 up)
    {
        forward = Vector3.Normalize(forward);
        var vector = Vector3.Normalize(Vector3.Cross(forward, Vector3.Normalize(up)));
        up = Vector3.Normalize(Vector3.Cross(vector, forward));
        var result = default(Matrix);
        result.Right = vector;
        result.Up = up;
        result.Forward = forward;
        result.Translation = position;
        result.M44 = 1f;
        return result;
    }

    public static Matrix CreateShadow(Vector4 lightDirection, Plane plane)
    {
        var normalDotLight = plane.Normal.X * lightDirection.X +
                             plane.Normal.Y * lightDirection.Y +
                             plane.Normal.Z * lightDirection.Z;
        var negativeNormalX = 0f - plane.Normal.X;
        var negativeNormalY = 0f - plane.Normal.Y;
        var negativeNormalZ = 0f - plane.Normal.Z;
        var negativePlaneD = 0f - plane.D;
        var result = default(Matrix);
        result.M11 = negativeNormalX * lightDirection.X + normalDotLight;
        result.M21 = negativeNormalY * lightDirection.X;
        result.M31 = negativeNormalZ * lightDirection.X;
        result.M41 = negativePlaneD * lightDirection.X;
        result.M12 = negativeNormalX * lightDirection.Y;
        result.M22 = negativeNormalY * lightDirection.Y + normalDotLight;
        result.M32 = negativeNormalZ * lightDirection.Y;
        result.M42 = negativePlaneD * lightDirection.Y;
        result.M13 = negativeNormalX * lightDirection.Z;
        result.M23 = negativeNormalY * lightDirection.Z;
        result.M33 = negativeNormalZ * lightDirection.Z + normalDotLight;
        result.M43 = negativePlaneD * lightDirection.Z;
        result.M14 = negativeNormalX * lightDirection.W;
        result.M24 = negativeNormalY * lightDirection.W;
        result.M34 = negativeNormalZ * lightDirection.W;
        result.M44 = negativePlaneD * lightDirection.W + normalDotLight;
        return result;
    }

    public static Matrix Transpose(Matrix m)
    {
        return new Matrix(
            m.M11,
            m.M21,
            m.M31,
            m.M41,
            m.M12,
            m.M22,
            m.M32,
            m.M42,
            m.M13,
            m.M23,
            m.M33,
            m.M43,
            m.M14,
            m.M24,
            m.M34,
            m.M44
        );
    }

    public static Matrix Invert(Matrix m)
    {
        var element11 = m.M11;
        var element12 = m.M12;
        var element13 = m.M13;
        var element14 = m.M14;
        var element21 = m.M21;
        var element22 = m.M22;
        var element23 = m.M23;
        var element24 = m.M24;
        var element31 = m.M31;
        var element32 = m.M32;
        var element33 = m.M33;
        var element34 = m.M34;
        var element41 = m.M41;
        var element42 = m.M42;
        var element43 = m.M43;
        var element44 = m.M44;
        var minorR34C34 = element33 * element44 - element34 * element43;
        var minorR34C24 = element32 * element44 - element34 * element42;
        var minorR34C23 = element32 * element43 - element33 * element42;
        var minorR34C14 = element31 * element44 - element34 * element41;
        var minorR34C13 = element31 * element43 - element33 * element41;
        var minorR34C12 = element31 * element42 - element32 * element41;
        var cofactor11 = element22 * minorR34C34 - element23 * minorR34C24 + element24 * minorR34C23;
        var cofactor12 = 0f - (element21 * minorR34C34 - element23 * minorR34C14 + element24 * minorR34C13);
        var cofactor13 = element21 * minorR34C24 - element22 * minorR34C14 + element24 * minorR34C12;
        var cofactor14 = 0f - (element21 * minorR34C23 - element22 * minorR34C13 + element23 * minorR34C12);
        var inverseDeterminant =
            1f / (element11 * cofactor11 + element12 * cofactor12 + element13 * cofactor13 + element14 * cofactor14);
        var result = default(Matrix);
        result.M11 = cofactor11 * inverseDeterminant;
        result.M21 = cofactor12 * inverseDeterminant;
        result.M31 = cofactor13 * inverseDeterminant;
        result.M41 = cofactor14 * inverseDeterminant;
        result.M12 = (0f - (element12 * minorR34C34 - element13 * minorR34C24 + element14 * minorR34C23)) *
                     inverseDeterminant;
        result.M22 = (element11 * minorR34C34 - element13 * minorR34C14 + element14 * minorR34C13) * inverseDeterminant;
        result.M32 = (0f - (element11 * minorR34C24 - element12 * minorR34C14 + element14 * minorR34C12)) *
                     inverseDeterminant;
        result.M42 = (element11 * minorR34C23 - element12 * minorR34C13 + element13 * minorR34C12) * inverseDeterminant;
        var minorR24C34 = element23 * element44 - element24 * element43;
        var minorR24C24 = element22 * element44 - element24 * element42;
        var minorR24C23 = element22 * element43 - element23 * element42;
        var minorR24C14 = element21 * element44 - element24 * element41;
        var minorR24C13 = element21 * element43 - element23 * element41;
        var minorR24C12 = element21 * element42 - element22 * element41;
        result.M13 = (element12 * minorR24C34 - element13 * minorR24C24 + element14 * minorR24C23) * inverseDeterminant;
        result.M23 = (0f - (element11 * minorR24C34 - element13 * minorR24C14 + element14 * minorR24C13)) *
                     inverseDeterminant;
        result.M33 = (element11 * minorR24C24 - element12 * minorR24C14 + element14 * minorR24C12) * inverseDeterminant;
        result.M43 = (0f - (element11 * minorR24C23 - element12 * minorR24C13 + element13 * minorR24C12)) *
                     inverseDeterminant;
        var minorR23C34 = element23 * element34 - element24 * element33;
        var minorR23C24 = element22 * element34 - element24 * element32;
        var minorR23C23 = element22 * element33 - element23 * element32;
        var minorR23C14 = element21 * element34 - element24 * element31;
        var minorR23C13 = element21 * element33 - element23 * element31;
        var minorR23C12 = element21 * element32 - element22 * element31;
        result.M14 = (0f - (element12 * minorR23C34 - element13 * minorR23C24 + element14 * minorR23C23)) *
                     inverseDeterminant;
        result.M24 = (element11 * minorR23C34 - element13 * minorR23C14 + element14 * minorR23C13) * inverseDeterminant;
        result.M34 = (0f - (element11 * minorR23C24 - element12 * minorR23C14 + element14 * minorR23C12)) *
                     inverseDeterminant;
        result.M44 = (element11 * minorR23C23 - element12 * minorR23C13 + element13 * minorR23C12) * inverseDeterminant;
        return result;
    }

    public static Matrix Lerp(Matrix m1, Matrix m2, float f)
    {
        var factor = Vector128.Create(f);
        LerpColumn(ref m1.M11, ref m2.M11, factor);
        LerpColumn(ref m1.M12, ref m2.M12, factor);
        LerpColumn(ref m1.M13, ref m2.M13, factor);
        LerpColumn(ref m1.M14, ref m2.M14, factor);
        return m1;
    }

    public static void MultiplyRestricted(ref Matrix m1, ref Matrix m2, out Matrix result)
    {
        var column1 = Vector128.LoadUnsafe(ref m1.M11);
        var column2 = Vector128.LoadUnsafe(ref m1.M12);
        var column3 = Vector128.LoadUnsafe(ref m1.M13);
        var column4 = Vector128.LoadUnsafe(ref m1.M14);
        result = default;
        MultiplyColumn(column1, column2, column3, column4, Vector128.LoadUnsafe(ref m2.M11), ref result.M11);
        MultiplyColumn(column1, column2, column3, column4, Vector128.LoadUnsafe(ref m2.M12), ref result.M12);
        MultiplyColumn(column1, column2, column3, column4, Vector128.LoadUnsafe(ref m2.M13), ref result.M13);
        MultiplyColumn(column1, column2, column3, column4, Vector128.LoadUnsafe(ref m2.M14), ref result.M14);
    }

    public static bool operator ==(Matrix m1, Matrix m2)
    {
        return m1.Equals(m2);
    }

    public static bool operator !=(Matrix m1, Matrix m2)
    {
        return !m1.Equals(m2);
    }

    public static Matrix operator +(Matrix m)
    {
        return m;
    }

    public static Matrix operator -(Matrix m)
    {
        var zero = Vector128<float>.Zero;
        NegateColumn(ref m.M11, zero);
        NegateColumn(ref m.M12, zero);
        NegateColumn(ref m.M13, zero);
        NegateColumn(ref m.M14, zero);
        return m;
    }

    public static Matrix operator +(Matrix m1, Matrix m2)
    {
        AddColumn(ref m1.M11, ref m2.M11);
        AddColumn(ref m1.M12, ref m2.M12);
        AddColumn(ref m1.M13, ref m2.M13);
        AddColumn(ref m1.M14, ref m2.M14);
        return m1;
    }

    public static Matrix operator -(Matrix m1, Matrix m2)
    {
        SubtractColumn(ref m1.M11, ref m2.M11);
        SubtractColumn(ref m1.M12, ref m2.M12);
        SubtractColumn(ref m1.M13, ref m2.M13);
        SubtractColumn(ref m1.M14, ref m2.M14);
        return m1;
    }

    public static Matrix operator *(Matrix m1, Matrix m2)
    {
        MultiplyRestricted(ref m1, ref m2, out var result);
        return result;
    }

    public static Matrix operator *(Matrix m, float s)
    {
        var factor = Vector128.Create(s);
        ScaleColumn(ref m.M11, factor);
        ScaleColumn(ref m.M12, factor);
        ScaleColumn(ref m.M13, factor);
        ScaleColumn(ref m.M14, factor);
        return m;
    }

    public static Matrix operator *(float s, Matrix m)
    {
        return m * s;
    }

    public static Matrix operator /(Matrix m1, Matrix m2)
    {
        DivideColumn(ref m1.M11, ref m2.M11);
        DivideColumn(ref m1.M12, ref m2.M12);
        DivideColumn(ref m1.M13, ref m2.M13);
        DivideColumn(ref m1.M14, ref m2.M14);
        return m1;
    }

    public static Matrix operator /(Matrix m, float d)
    {
        var inverseDivisor = 1f / d;
        return m * inverseDivisor;
    }

    private static void AddColumn(ref float left, ref float right)
    {
        (Vector128.LoadUnsafe(ref left) + Vector128.LoadUnsafe(ref right)).StoreUnsafe(ref left);
    }

    private static void DivideColumn(ref float left, ref float right)
    {
        (Vector128.LoadUnsafe(ref left) / Vector128.LoadUnsafe(ref right)).StoreUnsafe(ref left);
    }

    private static void LerpColumn(ref float left, ref float right, Vector128<float> factor)
    {
        var start = Vector128.LoadUnsafe(ref left);
        var end = Vector128.LoadUnsafe(ref right);
        (start + (end - start) * factor).StoreUnsafe(ref left);
    }

    private static void MultiplyColumn(
        Vector128<float> column1,
        Vector128<float> column2,
        Vector128<float> column3,
        Vector128<float> column4,
        Vector128<float> factors,
        ref float destination
    )
    {
        var result = column1 * Vector128.Create(factors[0]) +
                     column2 * Vector128.Create(factors[1]) +
                     column3 * Vector128.Create(factors[2]) +
                     column4 * Vector128.Create(factors[3]);
        result.StoreUnsafe(ref destination);
    }

    private static void NegateColumn(ref float destination, Vector128<float> zero)
    {
        (zero - Vector128.LoadUnsafe(ref destination)).StoreUnsafe(ref destination);
    }

    private static void ScaleColumn(ref float destination, Vector128<float> factor)
    {
        (Vector128.LoadUnsafe(ref destination) * factor).StoreUnsafe(ref destination);
    }

    private static void SubtractColumn(ref float left, ref float right)
    {
        (Vector128.LoadUnsafe(ref left) - Vector128.LoadUnsafe(ref right)).StoreUnsafe(ref left);
    }
}
