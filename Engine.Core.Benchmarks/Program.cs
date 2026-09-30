using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

using Engine.Core;

const int matrixCount = 4096;
const int vectorCount = 16384;
const int warmupRounds = 64;
const int measurementRounds = 15;

var random = new System.Random(12345);
var leftMatrices = CreateMatrices(random, matrixCount);
var rightMatrices = CreateMatrices(random, matrixCount);
var sourceVectors = CreateVectors(random, vectorCount);
var destinationVectors = new Vector3[vectorCount];
var transform = CreateMatrices(random, 1)[0];

Console.WriteLine($"Runtime: {RuntimeInformation.FrameworkDescription}");
Console.WriteLine($"Architecture: {RuntimeInformation.ProcessArchitecture}");
Console.WriteLine($"Vector128 accelerated: {Vector128.IsHardwareAccelerated}");
Console.WriteLine();

RunBenchmark("Matrix multiply", () => MultiplyScalar(leftMatrices, rightMatrices),
    () => MultiplySimd(leftMatrices, rightMatrices));
RunBenchmark("Matrix lerp", () => LerpScalar(leftMatrices, rightMatrices, 0.375f),
    () => LerpSimd(leftMatrices, rightMatrices, 0.375f));
RunBenchmark("Matrix add", () => AddScalar(leftMatrices, rightMatrices),
    () => AddSimd(leftMatrices, rightMatrices));
RunBenchmark("Matrix scale", () => ScaleScalar(leftMatrices, 1.375f),
    () => ScaleSimd(leftMatrices, 1.375f));
RunBenchmark("Vector3 batch transform", () => TransformScalar(sourceVectors, destinationVectors, transform),
    () => TransformSimd(sourceVectors, destinationVectors, transform));

return;

static Matrix[] CreateMatrices(System.Random random, int count)
{
    var result = new Matrix[count];
    for (var i = 0; i < result.Length; i++)
    {
        result[i] = new Matrix(
            NextFloat(random), NextFloat(random), NextFloat(random), NextFloat(random),
            NextFloat(random), NextFloat(random), NextFloat(random), NextFloat(random),
            NextFloat(random), NextFloat(random), NextFloat(random), NextFloat(random),
            NextFloat(random), NextFloat(random), NextFloat(random), NextFloat(random));
    }

    return result;
}

static Vector3[] CreateVectors(System.Random random, int count)
{
    var result = new Vector3[count];
    for (var i = 0; i < result.Length; i++)
    {
        result[i] = new Vector3(NextFloat(random), NextFloat(random), NextFloat(random));
    }

    return result;
}

[MethodImpl(MethodImplOptions.NoInlining)]
static float AddScalar(Matrix[] left, Matrix[] right)
{
    var checksum = 0f;
    for (var i = 0; i < left.Length; i++)
    {
        var x = left[i];
        var y = right[i];
        checksum += Checksum(new Matrix(
            x.M11 + y.M11, x.M12 + y.M12, x.M13 + y.M13, x.M14 + y.M14,
            x.M21 + y.M21, x.M22 + y.M22, x.M23 + y.M23, x.M24 + y.M24,
            x.M31 + y.M31, x.M32 + y.M32, x.M33 + y.M33, x.M34 + y.M34,
            x.M41 + y.M41, x.M42 + y.M42, x.M43 + y.M43, x.M44 + y.M44));
    }

    return checksum;
}

[MethodImpl(MethodImplOptions.NoInlining)]
static float AddSimd(Matrix[] left, Matrix[] right)
{
    var checksum = 0f;
    for (var i = 0; i < left.Length; i++)
    {
        checksum += Checksum(left[i] + right[i]);
    }

    return checksum;
}

[MethodImpl(MethodImplOptions.NoInlining)]
static float LerpScalar(Matrix[] left, Matrix[] right, float factor)
{
    var checksum = 0f;
    for (var i = 0; i < left.Length; i++)
    {
        var x = left[i];
        var y = right[i];
        var result = new Matrix(
            x.M11 + (y.M11 - x.M11) * factor, x.M12 + (y.M12 - x.M12) * factor,
            x.M13 + (y.M13 - x.M13) * factor, x.M14 + (y.M14 - x.M14) * factor,
            x.M21 + (y.M21 - x.M21) * factor, x.M22 + (y.M22 - x.M22) * factor,
            x.M23 + (y.M23 - x.M23) * factor, x.M24 + (y.M24 - x.M24) * factor,
            x.M31 + (y.M31 - x.M31) * factor, x.M32 + (y.M32 - x.M32) * factor,
            x.M33 + (y.M33 - x.M33) * factor, x.M34 + (y.M34 - x.M34) * factor,
            x.M41 + (y.M41 - x.M41) * factor, x.M42 + (y.M42 - x.M42) * factor,
            x.M43 + (y.M43 - x.M43) * factor, x.M44 + (y.M44 - x.M44) * factor);
        checksum += Checksum(result);
    }

    return checksum;
}

[MethodImpl(MethodImplOptions.NoInlining)]
static float LerpSimd(Matrix[] left, Matrix[] right, float factor)
{
    var checksum = 0f;
    for (var i = 0; i < left.Length; i++)
    {
        checksum += Checksum(Matrix.Lerp(left[i], right[i], factor));
    }

    return checksum;
}

[MethodImpl(MethodImplOptions.NoInlining)]
static float MultiplyScalar(Matrix[] left, Matrix[] right)
{
    var checksum = 0f;
    for (var i = 0; i < left.Length; i++)
    {
        var x = left[i];
        var y = right[i];
        var result = new Matrix(
            x.M11 * y.M11 + x.M12 * y.M21 + x.M13 * y.M31 + x.M14 * y.M41,
            x.M11 * y.M12 + x.M12 * y.M22 + x.M13 * y.M32 + x.M14 * y.M42,
            x.M11 * y.M13 + x.M12 * y.M23 + x.M13 * y.M33 + x.M14 * y.M43,
            x.M11 * y.M14 + x.M12 * y.M24 + x.M13 * y.M34 + x.M14 * y.M44,
            x.M21 * y.M11 + x.M22 * y.M21 + x.M23 * y.M31 + x.M24 * y.M41,
            x.M21 * y.M12 + x.M22 * y.M22 + x.M23 * y.M32 + x.M24 * y.M42,
            x.M21 * y.M13 + x.M22 * y.M23 + x.M23 * y.M33 + x.M24 * y.M43,
            x.M21 * y.M14 + x.M22 * y.M24 + x.M23 * y.M34 + x.M24 * y.M44,
            x.M31 * y.M11 + x.M32 * y.M21 + x.M33 * y.M31 + x.M34 * y.M41,
            x.M31 * y.M12 + x.M32 * y.M22 + x.M33 * y.M32 + x.M34 * y.M42,
            x.M31 * y.M13 + x.M32 * y.M23 + x.M33 * y.M33 + x.M34 * y.M43,
            x.M31 * y.M14 + x.M32 * y.M24 + x.M33 * y.M34 + x.M34 * y.M44,
            x.M41 * y.M11 + x.M42 * y.M21 + x.M43 * y.M31 + x.M44 * y.M41,
            x.M41 * y.M12 + x.M42 * y.M22 + x.M43 * y.M32 + x.M44 * y.M42,
            x.M41 * y.M13 + x.M42 * y.M23 + x.M43 * y.M33 + x.M44 * y.M43,
            x.M41 * y.M14 + x.M42 * y.M24 + x.M43 * y.M34 + x.M44 * y.M44);
        checksum += Checksum(result);
    }

    return checksum;
}

[MethodImpl(MethodImplOptions.NoInlining)]
static float MultiplySimd(Matrix[] left, Matrix[] right)
{
    var checksum = 0f;
    for (var i = 0; i < left.Length; i++)
    {
        checksum += Checksum(left[i] * right[i]);
    }

    return checksum;
}

static float NextFloat(System.Random random)
{
    return random.NextSingle() * 20f - 10f;
}

static void RunBenchmark(string name, Func<float> scalar, Func<float> simd)
{
    for (var i = 0; i < warmupRounds; i++)
    {
        _ = scalar();
        _ = simd();
    }

    var scalarTicks = Measure(scalar);
    var simdTicks = Measure(simd);
    Console.WriteLine(name);
    Console.WriteLine($"  Scalar: {TicksToMicroseconds(scalarTicks):F2} us");
    Console.WriteLine($"  SIMD:   {TicksToMicroseconds(simdTicks):F2} us");
    Console.WriteLine($"  Ratio:  {(double)scalarTicks / simdTicks:F2}x");
}

[MethodImpl(MethodImplOptions.NoInlining)]
static float ScaleScalar(Matrix[] source, float factor)
{
    var checksum = 0f;
    for (var i = 0; i < source.Length; i++)
    {
        var value = source[i];
        checksum += Checksum(new Matrix(
            value.M11 * factor, value.M12 * factor, value.M13 * factor, value.M14 * factor,
            value.M21 * factor, value.M22 * factor, value.M23 * factor, value.M24 * factor,
            value.M31 * factor, value.M32 * factor, value.M33 * factor, value.M34 * factor,
            value.M41 * factor, value.M42 * factor, value.M43 * factor, value.M44 * factor));
    }

    return checksum;
}

[MethodImpl(MethodImplOptions.NoInlining)]
static float ScaleSimd(Matrix[] source, float factor)
{
    var checksum = 0f;
    for (var i = 0; i < source.Length; i++)
    {
        checksum += Checksum(source[i] * factor);
    }

    return checksum;
}

static long Measure(Func<float> action)
{
    var samples = new long[measurementRounds];
    for (var i = 0; i < samples.Length; i++)
    {
        var started = Stopwatch.GetTimestamp();
        var checksum = action();
        samples[i] = Stopwatch.GetTimestamp() - started;
        GC.KeepAlive(checksum);
    }

    Array.Sort(samples);
    return samples[samples.Length / 2];
}

static double TicksToMicroseconds(long ticks)
{
    return ticks * 1_000_000d / Stopwatch.Frequency;
}

[MethodImpl(MethodImplOptions.NoInlining)]
static float Checksum(Matrix value)
{
    return value.M11 + value.M12 + value.M13 + value.M14 +
           value.M21 + value.M22 + value.M23 + value.M24 +
           value.M31 + value.M32 + value.M33 + value.M34 +
           value.M41 + value.M42 + value.M43 + value.M44;
}

[MethodImpl(MethodImplOptions.NoInlining)]
static float TransformScalar(Vector3[] source, Vector3[] destination, Matrix matrix)
{
    for (var i = 0; i < source.Length; i++)
    {
        var vector = source[i];
        destination[i] = new Vector3(
            vector.X * matrix.M11 + vector.Y * matrix.M21 + vector.Z * matrix.M31 + matrix.M41,
            vector.X * matrix.M12 + vector.Y * matrix.M22 + vector.Z * matrix.M32 + matrix.M42,
            vector.X * matrix.M13 + vector.Y * matrix.M23 + vector.Z * matrix.M33 + matrix.M43);
    }

    return destination[^1].X;
}

[MethodImpl(MethodImplOptions.NoInlining)]
static float TransformSimd(Vector3[] source, Vector3[] destination, Matrix matrix)
{
    Vector3.Transform(source, 0, ref matrix, destination, 0, source.Length);
    return destination[^1].X;
}
