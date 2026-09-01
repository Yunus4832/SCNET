using System.Diagnostics;

namespace Engine.Core;

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
        var num = UInt();
        return (int)(num & 0x7FFFFFFF);
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
        var num = Float();
        if (num < 0.5)
        {
            var num2 = MathUtils.Sqrt(-2f * MathUtils.Log(num));
            var num3 = 0.322232425f + num2 * (1f + num2 * (0.3422421f + num2 * (0.0204231218f + num2 * 4.536422E-05f)));
            var num4 = 0.09934846f +
                       num2 * (0.588581562f + num2 * (0.5311035f + num2 * (0.103537753f + num2 * 0.00385607f)));
            return mean + stdDev * (num3 / num4 - num2);
        }

        var num5 = MathUtils.Sqrt(-2f * MathUtils.Log(1f - num));
        var num6 = 0.322232425f + num5 * (1f + num5 * (0.3422421f + num5 * (0.0204231218f + num5 * 4.536422E-05f)));
        var num7 = 0.09934846f +
                   num5 * (0.588581562f + num5 * (0.5311035f + num5 * (0.103537753f + num5 * 0.00385607f)));
        return mean - stdDev * (num6 / num7 - num5);
    }

    public Vector2 Vector2()
    {
        float num;
        float num2;
        float num3;
        float num4;
        float num5;
        do
        {
            num = 2f * Float() - 1f;
            num2 = 2f * Float() - 1f;
            num3 = num * num;
            num4 = num2 * num2;
            num5 = num3 + num4;
        } while (!(num5 < 1f));

        var num6 = 1f / num5;
        return new Vector2((num3 - num4) * num6, 2f * num * num2 * num6);
    }

    public Vector2 Vector2(float length)
    {
        return Core.Vector2.Normalize(Vector2()) * length;
    }

    public Vector2 Vector2(float minLength, float maxLength)
    {
        return Core.Vector2.Normalize(Vector2()) * Float(minLength, maxLength);
    }

    public Vector3 Vector3()
    {
        float num;
        float num2;
        float num3;
        do
        {
            num = 2f * Float() - 1f;
            num2 = 2f * Float() - 1f;
            num3 = num * num + num2 * num2;
        } while (!(num3 < 1f));

        var num4 = MathUtils.Sqrt(1f - num3);
        return new Vector3(2f * num * num4, 2f * num2 * num4, 1f - 2f * num3);
    }

    public Vector3 Vector3(float length)
    {
        return Core.Vector3.Normalize(Vector3()) * length;
    }

    public Vector3 Vector3(float minLength, float maxLength)
    {
        return Core.Vector3.Normalize(Vector3()) * Float(minLength, maxLength);
    }

    public static uint RotateLeft(uint x, int k)
    {
        return (x << k) | (x >> (32 - k));
    }
}

/// <summary>
///     线性同余随机算法
/// </summary>
public class LcgRandom
{
    private const ulong _multiplier = 25214903917;

    private const ulong _addend = 11;

    private const ulong _mask = 0xFFFFFFFFFFFF;

    private static int _counter = (int)(Stopwatch.GetTimestamp() + DateTime.Now.Ticks);

    public LcgRandom() : this(997 * _counter++)
    {
    }

    public LcgRandom(int seed)
    {
        Reset(seed);
    }

    public ulong State { get; set; }

    public void Reset(int seed)
    {
        State = (ulong)(seed ^ 0x5DEECE66D);
    }

    public int Sign()
    {
        return Int() % 2 * 2 - 1;
    }

    public bool Bool()
    {
        return Int() % 2 == 0;
    }

    public bool Bool(float probability)
    {
        return Int() / 2.147484E+09f < probability;
    }

    public int Int()
    {
        State = (State * _multiplier + _addend) & _mask;
        return (int)(State >> 17);
    }

    public int Int(int bound)
    {
        return (int)(Int() * (long)bound / 0x8000000);
    }

    public int Int(int min, int max)
    {
        return (int)(min + Int() * (long)(max - min + 1) / 0x8000000);
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
        var num = Float();
        if (num < 0.5)
        {
            var num2 = MathUtils.Sqrt(-2f * MathUtils.Log(num));
            var num3 = 0.322232425f + num2 * (1f + num2 * (0.3422421f + num2 * (0.0204231218f + num2 * 4.536422E-05f)));
            var num4 = 0.09934846f +
                       num2 * (0.588581562f + num2 * (0.5311035f + num2 * (0.103537753f + num2 * 0.00385607f)));
            return mean + stdDev * (num3 / num4 - num2);
        }

        var num5 = MathUtils.Sqrt(-2f * MathUtils.Log(1f - num));
        var num6 = 0.322232425f + num5 * (1f + num5 * (0.3422421f + num5 * (0.0204231218f + num5 * 4.536422E-05f)));
        var num7 = 0.09934846f +
                   num5 * (0.588581562f + num5 * (0.5311035f + num5 * (0.103537753f + num5 * 0.00385607f)));
        return mean - stdDev * (num6 / num7 - num5);
    }

    public Vector2 Vector2()
    {
        float num;
        float num2;
        float num3;
        float num4;
        float num5;
        do
        {
            num = 2f * Float() - 1f;
            num2 = 2f * Float() - 1f;
            num3 = num * num;
            num4 = num2 * num2;
            num5 = num3 + num4;
        } while (!(num5 < 1f));

        var num6 = 1f / num5;
        return new Vector2((num3 - num4) * num6, 2f * num * num2 * num6);
    }

    public Vector2 Vector2(float length)
    {
        return Core.Vector2.Normalize(Vector2()) * length;
    }

    public Vector2 Vector2(float minLength, float maxLength)
    {
        return Core.Vector2.Normalize(Vector2()) * Float(minLength, maxLength);
    }

    public Vector3 Vector3()
    {
        float num;
        float num2;
        float num3;
        do
        {
            num = 2f * Float() - 1f;
            num2 = 2f * Float() - 1f;
            num3 = num * num + num2 * num2;
        } while (!(num3 < 1f));

        var num4 = MathUtils.Sqrt(1f - num3);
        return new Vector3(2f * num * num4, 2f * num2 * num4, 1f - 2f * num3);
    }

    public Vector3 Vector3(float length)
    {
        return Core.Vector3.Normalize(Vector3()) * length;
    }

    public Vector3 Vector3(float minLength, float maxLength)
    {
        return Core.Vector3.Normalize(Vector3()) * Float(minLength, maxLength);
    }
}
