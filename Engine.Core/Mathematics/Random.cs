using System.Diagnostics;

namespace Engine.Core.Mathematics;

/// <summary>
///     异或移位随机算法
/// </summary>
public class Random
{
    private static int _counter = (int)(Stopwatch.GetTimestamp() + DateTime.Now.Ticks);

    private uint _s0;

    private uint _s1;

    public Random()
    {
        Seed();
    }

    public Random(int seed)
    {
        Seed(seed);
    }

    public ulong State
    {
        get => _s0 + ((ulong)_s1 << 32);
        set
        {
            _s0 = (uint)value;
            _s1 = (uint)(value >> 32);
        }
    }

    public void Seed()
    {
        Seed(_counter++);
    }

    public void Seed(int seed)
    {
        _s0 = MathUtils.Hash((uint)seed);
        _s1 = MathUtils.Hash((uint)(seed + 1));
    }

    public int Sign()
    {
        return Int() % 2 * 2 - 1;
    }

    public bool Bool()
    {
        return (Int() & 1) != 0;
    }

    public bool Bool(float probability)
    {
        return Int() / 2.147484E+09f < probability;
    }

    public uint UInt()
    {
        var s = _s0;
        var s2 = _s1;
        s2 ^= s;
        _s0 = RotateLeft(s, 26) ^ s2 ^ (s2 << 9);
        _s1 = RotateLeft(s2, 13);
        return RotateLeft((uint)((int)s * -1640531525), 5) * 5;
    }

    public int Int()
    {
        var bits = UInt();
        return (int)(bits & 0x7FFFFFFF);
    }

    public int Int(int bound)
    {
        return (int)(Int() * (long)bound / 2147483648L);
    }

    public int Int(int min, int max)
    {
        return (int)(min + Int() * (long)(max - min + 1) / 2147483648L);
    }

    public float Float()
    {
        return Int() / 2.147484E+09f;
    }

    public float Float(float min, float max)
    {
        return min + Float() * (max - min);
    }

    public float NormalFloat(float mean, float stdDev)
    {
        var probability = Float();
        if (probability < 0.5)
        {
            var lowerTailScale = MathUtils.Sqrt(-2f * MathUtils.Log(probability));
            var lowerNumerator = 0.322232425f +
                                 lowerTailScale *
                                 (1f +
                                  lowerTailScale *
                                  (0.3422421f + lowerTailScale * (0.0204231218f + lowerTailScale * 4.536422E-05f)));
            var lowerDenominator = 0.09934846f +
                                   lowerTailScale *
                                   (0.588581562f +
                                    lowerTailScale *
                                    (0.5311035f + lowerTailScale * (0.103537753f + lowerTailScale * 0.00385607f)));
            return mean + stdDev * (lowerNumerator / lowerDenominator - lowerTailScale);
        }

        var upperTailScale = MathUtils.Sqrt(-2f * MathUtils.Log(1f - probability));
        var upperNumerator = 0.322232425f +
                             upperTailScale *
                             (1f +
                              upperTailScale *
                              (0.3422421f + upperTailScale * (0.0204231218f + upperTailScale * 4.536422E-05f)));
        var upperDenominator = 0.09934846f +
                               upperTailScale *
                               (0.588581562f +
                                upperTailScale *
                                (0.5311035f + upperTailScale * (0.103537753f + upperTailScale * 0.00385607f)));
        return mean - stdDev * (upperNumerator / upperDenominator - upperTailScale);
    }

    public Vector2 Vector2()
    {
        float sampleX;
        float sampleY;
        float xSquared;
        float ySquared;
        float radiusSquared;
        do
        {
            sampleX = 2f * Float() - 1f;
            sampleY = 2f * Float() - 1f;
            xSquared = sampleX * sampleX;
            ySquared = sampleY * sampleY;
            radiusSquared = xSquared + ySquared;
        } while (!(radiusSquared < 1f));

        var inverseRadiusSquared = 1f / radiusSquared;
        return new Vector2((xSquared - ySquared) * inverseRadiusSquared, 2f * sampleX * sampleY * inverseRadiusSquared);
    }

    public Vector2 Vector2(float length)
    {
        return Mathematics.Vector2.Normalize(Vector2()) * length;
    }

    public Vector2 Vector2(float minLength, float maxLength)
    {
        return Mathematics.Vector2.Normalize(Vector2()) * Float(minLength, maxLength);
    }

    public Vector3 Vector3()
    {
        float sampleX;
        float sampleY;
        float radiusSquared;
        do
        {
            sampleX = 2f * Float() - 1f;
            sampleY = 2f * Float() - 1f;
            radiusSquared = sampleX * sampleX + sampleY * sampleY;
        } while (!(radiusSquared < 1f));

        var projectionScale = MathUtils.Sqrt(1f - radiusSquared);
        return new Vector3(2f * sampleX * projectionScale, 2f * sampleY * projectionScale, 1f - 2f * radiusSquared);
    }

    public Vector3 Vector3(float length)
    {
        return Mathematics.Vector3.Normalize(Vector3()) * length;
    }

    public Vector3 Vector3(float minLength, float maxLength)
    {
        return Mathematics.Vector3.Normalize(Vector3()) * Float(minLength, maxLength);
    }

    public static uint RotateLeft(uint x, int k)
    {
        return (x << k) | (x >> (32 - k));
    }
}
