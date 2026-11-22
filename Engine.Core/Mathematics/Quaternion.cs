namespace Engine.Core.Mathematics;

public struct Quaternion(float x, float y, float z, float w)
    : IEquatable<Quaternion>
{
    public float X = x;

    public float Y = y;

    public float Z = z;

    public float W = w;

    public static readonly Quaternion Identity = new(0f, 0f, 0f, 1f);

    public Quaternion(Vector3 v, float s) : this(v.X, v.Y, v.Z, s)
    {
    }

    public static implicit operator Quaternion((float X, float Y, float Z, float W) v)
    {
        return new Quaternion(v.X, v.Y, v.Z, v.W);
    }

    public override bool Equals(object? obj)
    {
        return obj is Quaternion quaternion && Equals(quaternion);
    }

    public override int GetHashCode()
    {
        return X.GetHashCode() + Y.GetHashCode() + Z.GetHashCode() + W.GetHashCode();
    }

    public override string ToString()
    {
        return $"{X},{Y},{Z},{W}";
    }

    public bool Equals(Quaternion other)
    {
        return X.CloseTo(other.X) &&
               Y.CloseTo(other.Y) &&
               Z.CloseTo(other.Z) &&
               W.CloseTo(other.W);
    }

    public static Quaternion Conjugate(Quaternion q)
    {
        return new Quaternion(0f - q.X, 0f - q.Y, 0f - q.Z, q.W);
    }

    public static Quaternion CreateFromAxisAngle(Vector3 axis, float angle)
    {
        var halfAngle = angle * 0.5f;
        var sinHalfAngle = MathUtils.Sin(halfAngle);
        var w = MathUtils.Cos(halfAngle);
        var result = default(Quaternion);
        result.X = axis.X * sinHalfAngle;
        result.Y = axis.Y * sinHalfAngle;
        result.Z = axis.Z * sinHalfAngle;
        result.W = w;
        return result;
    }

    public static Quaternion CreateFromRotationMatrix(Matrix m)
    {
        var trace = m.M11 + m.M22 + m.M33;
        var result = default(Quaternion);
        if (trace > 0f)
        {
            var componentScale = MathUtils.Sqrt(trace + 1f);
            result.W = componentScale * 0.5f;
            componentScale = 0.5f / componentScale;
            result.X = (m.M23 - m.M32) * componentScale;
            result.Y = (m.M31 - m.M13) * componentScale;
            result.Z = (m.M12 - m.M21) * componentScale;
            return result;
        }

        if (m.M11 >= m.M22 && m.M11 >= m.M33)
        {
            var twiceX = MathUtils.Sqrt(1f + m.M11 - m.M22 - m.M33);
            var inverseFourX = 0.5f / twiceX;
            result.X = 0.5f * twiceX;
            result.Y = (m.M12 + m.M21) * inverseFourX;
            result.Z = (m.M13 + m.M31) * inverseFourX;
            result.W = (m.M23 - m.M32) * inverseFourX;
            return result;
        }

        if (m.M22 > m.M33)
        {
            var twiceY = MathUtils.Sqrt(1f + m.M22 - m.M11 - m.M33);
            var inverseFourY = 0.5f / twiceY;
            result.X = (m.M21 + m.M12) * inverseFourY;
            result.Y = 0.5f * twiceY;
            result.Z = (m.M32 + m.M23) * inverseFourY;
            result.W = (m.M31 - m.M13) * inverseFourY;
            return result;
        }

        var twiceZ = MathUtils.Sqrt(1f + m.M33 - m.M11 - m.M22);
        var inverseFourZ = 0.5f / twiceZ;
        result.X = (m.M31 + m.M13) * inverseFourZ;
        result.Y = (m.M32 + m.M23) * inverseFourZ;
        result.Z = 0.5f * twiceZ;
        result.W = (m.M12 - m.M21) * inverseFourZ;
        return result;
    }

    public static Quaternion CreateFromYawPitchRoll(float yaw, float pitch, float roll)
    {
        var halfRoll = roll * 0.5f;
        var halfPitch = pitch * 0.5f;
        var halfYaw = yaw * 0.5f;
        var sinHalfRoll = MathUtils.Sin(halfRoll);
        var cosHalfRoll = MathUtils.Cos(halfRoll);
        var sinHalfPitch = MathUtils.Sin(halfPitch);
        var cosHalfPitch = MathUtils.Cos(halfPitch);
        var sinHalfYaw = MathUtils.Sin(halfYaw);
        var cosHalfYaw = MathUtils.Cos(halfYaw);
        return new Quaternion(
            cosHalfYaw * sinHalfPitch * cosHalfRoll + sinHalfYaw * cosHalfPitch * sinHalfRoll,
            sinHalfYaw * cosHalfPitch * cosHalfRoll - cosHalfYaw * sinHalfPitch * sinHalfRoll,
            cosHalfYaw * cosHalfPitch * sinHalfRoll - sinHalfYaw * sinHalfPitch * cosHalfRoll,
            cosHalfYaw * cosHalfPitch * cosHalfRoll + sinHalfYaw * sinHalfPitch * sinHalfRoll
        );
    }

    public static float Dot(Quaternion q1, Quaternion q2)
    {
        return q1.X * q2.X + q1.Y * q2.Y + q1.Z * q2.Z + q1.W * q2.W;
    }

    public static Quaternion Inverse(Quaternion q)
    {
        var lengthSquared = q.X * q.X + q.Y * q.Y + q.Z * q.Z + q.W * q.W;
        var inverseLengthSquared = 1f / lengthSquared;
        var result = default(Quaternion);
        result.X = (0f - q.X) * inverseLengthSquared;
        result.Y = (0f - q.Y) * inverseLengthSquared;
        result.Z = (0f - q.Z) * inverseLengthSquared;
        result.W = q.W * inverseLengthSquared;
        return result;
    }

    public float Length()
    {
        return MathUtils.Sqrt(LengthSquared());
    }

    public float LengthSquared()
    {
        return X * X + Y * Y + Z * Z + W * W;
    }

    public static Quaternion Lerp(Quaternion q1, Quaternion q2, float f)
    {
        var firstWeight = 1f - f;
        var result = default(Quaternion);
        if (q1.X * q2.X + q1.Y * q2.Y + q1.Z * q2.Z + q1.W * q2.W >= 0f)
        {
            result.X = firstWeight * q1.X + f * q2.X;
            result.Y = firstWeight * q1.Y + f * q2.Y;
            result.Z = firstWeight * q1.Z + f * q2.Z;
            result.W = firstWeight * q1.W + f * q2.W;
        }
        else
        {
            result.X = firstWeight * q1.X - f * q2.X;
            result.Y = firstWeight * q1.Y - f * q2.Y;
            result.Z = firstWeight * q1.Z - f * q2.Z;
            result.W = firstWeight * q1.W - f * q2.W;
        }

        var inverseLength = 1f / result.Length();
        result.X *= inverseLength;
        result.Y *= inverseLength;
        result.Z *= inverseLength;
        result.W *= inverseLength;
        return result;
    }

    public static Quaternion Slerp(Quaternion q1, Quaternion q2, float f)
    {
        var dot = q1.X * q2.X + q1.Y * q2.Y + q1.Z * q2.Z + q1.W * q2.W;
        var negateSecond = false;
        if (dot < 0f)
        {
            negateSecond = true;
            dot = 0f - dot;
        }

        float firstWeight;
        float secondWeight;
        if (dot > 0.999999f)
        {
            firstWeight = 1f - f;
            secondWeight = negateSecond ? 0f - f : f;
        }
        else
        {
            var angle = MathUtils.Acos(dot);
            var inverseSinAngle = 1f / MathUtils.Sin(angle);
            firstWeight = MathUtils.Sin((1f - f) * angle) * inverseSinAngle;
            secondWeight = negateSecond
                ? (0f - MathUtils.Sin(f * angle)) * inverseSinAngle
                : MathUtils.Sin(f * angle) * inverseSinAngle;
        }

        var result = default(Quaternion);
        result.X = firstWeight * q1.X + secondWeight * q2.X;
        result.Y = firstWeight * q1.Y + secondWeight * q2.Y;
        result.Z = firstWeight * q1.Z + secondWeight * q2.Z;
        result.W = firstWeight * q1.W + secondWeight * q2.W;
        return result;
    }

    public static Quaternion Normalize(Quaternion q)
    {
        var length = q.Length();
        if (length == 0f)
        {
            return Identity;
        }

        return q / length;
    }

    public Matrix ToMatrix()
    {
        var xx = X * X;
        var yy = Y * Y;
        var zz = Z * Z;
        var xy = X * Y;
        var zw = Z * W;
        var xz = X * Z;
        var yw = Y * W;
        var yz = Y * Z;
        var xw = X * W;
        var result = default(Matrix);
        result.M11 = 1f - 2f * (yy + zz);
        result.M12 = 2f * (xy + zw);
        result.M13 = 2f * (xz - yw);
        result.M14 = 0f;
        result.M21 = 2f * (xy - zw);
        result.M22 = 1f - 2f * (zz + xx);
        result.M23 = 2f * (yz + xw);
        result.M24 = 0f;
        result.M31 = 2f * (xz + yw);
        result.M32 = 2f * (yz - xw);
        result.M33 = 1f - 2f * (yy + xx);
        result.M34 = 0f;
        result.M41 = 0f;
        result.M42 = 0f;
        result.M43 = 0f;
        result.M44 = 1f;
        return result;
    }

    public Vector3 GetRightVector()
    {
        return new Vector3(1f - 2f * (Y * Y + Z * Z), 2f * (X * Y + Z * W), 2f * (X * Z - W * Y));
    }

    public Vector3 GetUpVector()
    {
        return new Vector3(2f * (X * Y - Z * W), 1f - 2f * (X * X + Z * Z), 2f * (Y * Z + X * W));
    }

    public Vector3 GetForwardVector()
    {
        return new Vector3(-2f * (Y * W + X * Z), 2f * (X * W - Y * Z), 2f * (X * X + Y * Y) - 1f);
    }

    public Vector3 ToYawPitchRoll()
    {
        var forwardX = -2f * (Y * W + X * Z);
        var forwardY = 2f * (X * W - Y * Z);
        var forwardZ = 2f * (X * X + Y * Y) - 1f;
        var rollNumerator = 2f * (X * Y + Z * W);
        var rollDenominator = 1f - 2f * (X * X + Z * Z);
        var yaw = MathUtils.Atan2(0f - forwardX, 0f - forwardZ);
        var pitch = MathUtils.Asin(forwardY);
        var roll = MathUtils.Atan2(rollNumerator, rollDenominator);
        return new Vector3(yaw, pitch, roll);
    }

    public static bool operator ==(Quaternion q1, Quaternion q2)
    {
        return q1.Equals(q2);
    }

    public static bool operator !=(Quaternion q1, Quaternion q2)
    {
        return !q1.Equals(q2);
    }

    public static Quaternion operator +(Quaternion q)
    {
        return q;
    }

    public static Quaternion operator -(Quaternion q)
    {
        return new Quaternion(0f - q.X, 0f - q.Y, 0f - q.Z, 0f - q.W);
    }

    public static Quaternion operator +(Quaternion q1, Quaternion q2)
    {
        return new Quaternion(q1.X + q2.X, q1.Y + q2.Y, q1.Z + q2.Z, q1.W + q2.W);
    }

    public static Quaternion operator -(Quaternion q1, Quaternion q2)
    {
        return new Quaternion(q1.X - q2.X, q1.Y - q2.Y, q1.Z - q2.Z, q1.W - q2.W);
    }

    public static Quaternion operator *(Quaternion q1, Quaternion q2)
    {
        var x = q1.X;
        var y = q1.Y;
        var z = q1.Z;
        var w = q1.W;
        var x2 = q2.X;
        var y2 = q2.Y;
        var z2 = q2.Z;
        var w2 = q2.W;
        var crossX = y * z2 - z * y2;
        var crossY = z * x2 - x * z2;
        var crossZ = x * y2 - y * x2;
        var vectorDot = x * x2 + y * y2 + z * z2;
        var result = default(Quaternion);
        result.X = x * w2 + x2 * w + crossX;
        result.Y = y * w2 + y2 * w + crossY;
        result.Z = z * w2 + z2 * w + crossZ;
        result.W = w * w2 - vectorDot;
        return result;
    }

    public static Quaternion operator *(Quaternion q, float s)
    {
        return new Quaternion(q.X * s, q.Y * s, q.Z * s, q.W * s);
    }

    public static Quaternion operator /(Quaternion q1, Quaternion q2)
    {
        var x = q1.X;
        var y = q1.Y;
        var z = q1.Z;
        var w = q1.W;
        var lengthSquared = q2.X * q2.X + q2.Y * q2.Y + q2.Z * q2.Z + q2.W * q2.W;
        var inverseLengthSquared = 1f / lengthSquared;
        var inverseX = (0f - q2.X) * inverseLengthSquared;
        var inverseY = (0f - q2.Y) * inverseLengthSquared;
        var inverseZ = (0f - q2.Z) * inverseLengthSquared;
        var inverseW = q2.W * inverseLengthSquared;
        var crossX = y * inverseZ - z * inverseY;
        var crossY = z * inverseX - x * inverseZ;
        var crossZ = x * inverseY - y * inverseX;
        var vectorDot = x * inverseX + y * inverseY + z * inverseZ;
        var result = default(Quaternion);
        result.X = x * inverseW + inverseX * w + crossX;
        result.Y = y * inverseW + inverseY * w + crossY;
        result.Z = z * inverseW + inverseZ * w + crossZ;
        result.W = w * inverseW - vectorDot;
        return result;
    }

    public static Quaternion operator /(Quaternion q, float d)
    {
        var inverseDivisor = 1f / d;
        return new Quaternion(q.X * inverseDivisor, q.Y * inverseDivisor, q.Z * inverseDivisor, q.W * inverseDivisor);
    }
}
