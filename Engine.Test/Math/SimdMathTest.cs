using Engine.Core;

namespace Engine.Test.Math;

public class SimdMathTest
{
    private static readonly Matrix _left = new(
        1.25f, -2.5f, 3.75f, -4.125f,
        5.5f, -6.25f, 7.125f, -8.75f,
        9.5f, -10.25f, 11.75f, -12.5f,
        13.125f, -14.75f, 15.5f, -16.25f);

    private static readonly Matrix _right = new(
        -0.75f, 1.5f, -2.25f, 3.125f,
        -4.5f, 5.25f, -6.125f, 7.75f,
        -8.5f, 9.25f, -10.75f, 11.5f,
        -12.125f, 13.75f, -14.5f, 15.25f);

    [Fact]
    public void MatrixOperationsMatchScalarResultsBitwise()
    {
        AssertMatrixBitsEqual(AddScalar(_left, _right), _left + _right);
        AssertMatrixBitsEqual(SubtractScalar(_left, _right), _left - _right);
        AssertMatrixBitsEqual(NegateScalar(_left), -_left);
        AssertMatrixBitsEqual(ScaleScalar(_left, 1.375f), _left * 1.375f);
        AssertMatrixBitsEqual(DivideScalar(_left, _right), _left / _right);
        AssertMatrixBitsEqual(ScaleScalar(_left, 1f / 1.375f), _left / 1.375f);
        AssertMatrixBitsEqual(LerpScalar(_left, _right, 0.375f), Matrix.Lerp(_left, _right, 0.375f));
        AssertMatrixBitsEqual(MultiplyScalar(_left, _right), _left * _right);
    }

    [Fact]
    public void BatchTransformsMatchScalarResultsBitwise()
    {
        var source2 = new[] { new Vector2(1.25f, -2.5f), new Vector2(-3.75f, 4.125f) };
        var destination2 = new Vector2[source2.Length];
        var matrix = _left;
        Vector2.Transform(source2, 0, ref matrix, destination2, 0, source2.Length);
        for (var i = 0; i < source2.Length; i++)
        {
            AssertVectorBitsEqual(TransformScalar(source2[i], matrix), destination2[i]);
        }

        Vector2.TransformNormal(source2, 0, ref matrix, destination2, 0, source2.Length);
        for (var i = 0; i < source2.Length; i++)
        {
            AssertVectorBitsEqual(TransformNormalScalar(source2[i], matrix), destination2[i]);
        }

        var source3 = new[] { new Vector3(1.25f, -2.5f, 3.75f), new Vector3(-4.125f, 5.5f, -6.25f) };
        var destination3 = new Vector3[source3.Length];
        Vector3.Transform(source3, 0, ref matrix, destination3, 0, source3.Length);
        for (var i = 0; i < source3.Length; i++)
        {
            AssertVectorBitsEqual(TransformScalar(source3[i], matrix), destination3[i]);
        }

        Vector3.TransformNormal(source3, 0, ref matrix, destination3, 0, source3.Length);
        for (var i = 0; i < source3.Length; i++)
        {
            AssertVectorBitsEqual(TransformNormalScalar(source3[i], matrix), destination3[i]);
        }

        var source4 = new[] { new Vector4(1.25f, -2.5f, 3.75f, 8f), new Vector4(-4.125f, 5.5f, -6.25f, 9f) };
        var destination4 = new Vector4[source4.Length];
        Vector4.Transform(source4, 0, ref matrix, destination4, 0, source4.Length);
        for (var i = 0; i < source4.Length; i++)
        {
            AssertVectorBitsEqual(TransformScalar(source4[i], matrix), destination4[i]);
        }
    }

    [Fact]
    public void Vector4RoundingMatchesScalarResultsBitwise()
    {
        var value = new Vector4(-2.75f, -1.5f, 1.5f, 2.75f);
        AssertVectorBitsEqual(new Vector4(MathF.Floor(value.X), MathF.Floor(value.Y), MathF.Floor(value.Z),
            MathF.Floor(value.W)), Vector4.Floor(value));
        AssertVectorBitsEqual(new Vector4(MathF.Ceiling(value.X), MathF.Ceiling(value.Y), MathF.Ceiling(value.Z),
            MathF.Ceiling(value.W)), Vector4.Ceiling(value));
        AssertVectorBitsEqual(new Vector4(MathF.Round(value.X), MathF.Round(value.Y), MathF.Round(value.Z),
            MathF.Round(value.W)), Vector4.Round(value));
    }

    private static Matrix AddScalar(Matrix left, Matrix right)
    {
        return FromValues(ToValues(left).Zip(ToValues(right), (x, y) => x + y).ToArray());
    }

    private static void AssertFloatBitsEqual(float expected, float actual)
    {
        Assert.Equal(BitConverter.SingleToInt32Bits(expected), BitConverter.SingleToInt32Bits(actual));
    }

    private static void AssertMatrixBitsEqual(Matrix expected, Matrix actual)
    {
        var expectedValues = ToValues(expected);
        var actualValues = ToValues(actual);
        for (var i = 0; i < expectedValues.Length; i++)
        {
            AssertFloatBitsEqual(expectedValues[i], actualValues[i]);
        }
    }

    private static void AssertVectorBitsEqual(Vector2 expected, Vector2 actual)
    {
        AssertFloatBitsEqual(expected.X, actual.X);
        AssertFloatBitsEqual(expected.Y, actual.Y);
    }

    private static void AssertVectorBitsEqual(Vector3 expected, Vector3 actual)
    {
        AssertFloatBitsEqual(expected.X, actual.X);
        AssertFloatBitsEqual(expected.Y, actual.Y);
        AssertFloatBitsEqual(expected.Z, actual.Z);
    }

    private static void AssertVectorBitsEqual(Vector4 expected, Vector4 actual)
    {
        AssertFloatBitsEqual(expected.X, actual.X);
        AssertFloatBitsEqual(expected.Y, actual.Y);
        AssertFloatBitsEqual(expected.Z, actual.Z);
        AssertFloatBitsEqual(expected.W, actual.W);
    }

    private static Matrix DivideScalar(Matrix left, Matrix right)
    {
        return FromValues(ToValues(left).Zip(ToValues(right), (x, y) => x / y).ToArray());
    }

    private static Matrix FromValues(float[] values)
    {
        return new Matrix(values[0], values[1], values[2], values[3], values[4], values[5], values[6], values[7],
            values[8], values[9], values[10], values[11], values[12], values[13], values[14], values[15]);
    }

    private static Matrix LerpScalar(Matrix left, Matrix right, float factor)
    {
        return FromValues(ToValues(left).Zip(ToValues(right), (x, y) => x + (y - x) * factor).ToArray());
    }

    private static Matrix MultiplyScalar(Matrix left, Matrix right)
    {
        return new Matrix(
            left.M11 * right.M11 + left.M12 * right.M21 + left.M13 * right.M31 + left.M14 * right.M41,
            left.M11 * right.M12 + left.M12 * right.M22 + left.M13 * right.M32 + left.M14 * right.M42,
            left.M11 * right.M13 + left.M12 * right.M23 + left.M13 * right.M33 + left.M14 * right.M43,
            left.M11 * right.M14 + left.M12 * right.M24 + left.M13 * right.M34 + left.M14 * right.M44,
            left.M21 * right.M11 + left.M22 * right.M21 + left.M23 * right.M31 + left.M24 * right.M41,
            left.M21 * right.M12 + left.M22 * right.M22 + left.M23 * right.M32 + left.M24 * right.M42,
            left.M21 * right.M13 + left.M22 * right.M23 + left.M23 * right.M33 + left.M24 * right.M43,
            left.M21 * right.M14 + left.M22 * right.M24 + left.M23 * right.M34 + left.M24 * right.M44,
            left.M31 * right.M11 + left.M32 * right.M21 + left.M33 * right.M31 + left.M34 * right.M41,
            left.M31 * right.M12 + left.M32 * right.M22 + left.M33 * right.M32 + left.M34 * right.M42,
            left.M31 * right.M13 + left.M32 * right.M23 + left.M33 * right.M33 + left.M34 * right.M43,
            left.M31 * right.M14 + left.M32 * right.M24 + left.M33 * right.M34 + left.M34 * right.M44,
            left.M41 * right.M11 + left.M42 * right.M21 + left.M43 * right.M31 + left.M44 * right.M41,
            left.M41 * right.M12 + left.M42 * right.M22 + left.M43 * right.M32 + left.M44 * right.M42,
            left.M41 * right.M13 + left.M42 * right.M23 + left.M43 * right.M33 + left.M44 * right.M43,
            left.M41 * right.M14 + left.M42 * right.M24 + left.M43 * right.M34 + left.M44 * right.M44);
    }

    private static Matrix NegateScalar(Matrix value)
    {
        return FromValues(ToValues(value).Select(x => 0f - x).ToArray());
    }

    private static Matrix ScaleScalar(Matrix value, float factor)
    {
        return FromValues(ToValues(value).Select(x => x * factor).ToArray());
    }

    private static Matrix SubtractScalar(Matrix left, Matrix right)
    {
        return FromValues(ToValues(left).Zip(ToValues(right), (x, y) => x - y).ToArray());
    }

    private static float[] ToValues(Matrix value)
    {
        return
        [
            value.M11, value.M12, value.M13, value.M14,
            value.M21, value.M22, value.M23, value.M24,
            value.M31, value.M32, value.M33, value.M34,
            value.M41, value.M42, value.M43, value.M44
        ];
    }

    private static Vector2 TransformScalar(Vector2 vector, Matrix matrix)
    {
        return new Vector2(vector.X * matrix.M11 + vector.Y * matrix.M21 + matrix.M41,
            vector.X * matrix.M12 + vector.Y * matrix.M22 + matrix.M42);
    }

    private static Vector3 TransformScalar(Vector3 vector, Matrix matrix)
    {
        return new Vector3(vector.X * matrix.M11 + vector.Y * matrix.M21 + vector.Z * matrix.M31 + matrix.M41,
            vector.X * matrix.M12 + vector.Y * matrix.M22 + vector.Z * matrix.M32 + matrix.M42,
            vector.X * matrix.M13 + vector.Y * matrix.M23 + vector.Z * matrix.M33 + matrix.M43);
    }

    private static Vector4 TransformScalar(Vector4 vector, Matrix matrix)
    {
        return new Vector4(vector.X * matrix.M11 + vector.Y * matrix.M21 + vector.Z * matrix.M31 + matrix.M41,
            vector.X * matrix.M12 + vector.Y * matrix.M22 + vector.Z * matrix.M32 + matrix.M42,
            vector.X * matrix.M13 + vector.Y * matrix.M23 + vector.Z * matrix.M33 + matrix.M43,
            vector.X * matrix.M14 + vector.Y * matrix.M24 + vector.Z * matrix.M34 + matrix.M44);
    }

    private static Vector2 TransformNormalScalar(Vector2 vector, Matrix matrix)
    {
        return new Vector2(vector.X * matrix.M11 + vector.Y * matrix.M21,
            vector.X * matrix.M12 + vector.Y * matrix.M22);
    }

    private static Vector3 TransformNormalScalar(Vector3 vector, Matrix matrix)
    {
        return new Vector3(vector.X * matrix.M11 + vector.Y * matrix.M21 + vector.Z * matrix.M31,
            vector.X * matrix.M12 + vector.Y * matrix.M22 + vector.Z * matrix.M32,
            vector.X * matrix.M13 + vector.Y * matrix.M23 + vector.Z * matrix.M33);
    }
}
