namespace Game;

public static class SimplexNoise
{
    private static readonly int[][] _grad3 = new int[][]
    {
        [1, 1, 0],
        [-1, 1, 0],
        [1, -1, 0],
        [-1, -1, 0],
        [1, 0, 1],
        [-1, 0, 1],
        [1, 0, -1],
        [-1, 0, -1],
        [0, 1, 1],
        [0, -1, 1],
        [0, 1, -1],
        [0, -1, -1]
    };

    private static readonly int[] _permutations =
    [
        151,
        160,
        137,
        91,
        90,
        15,
        131,
        13,
        201,
        95,
        96,
        53,
        194,
        233,
        7,
        225,
        140,
        36,
        103,
        30,
        69,
        142,
        8,
        99,
        37,
        240,
        21,
        10,
        23,
        190,
        6,
        148,
        247,
        120,
        234,
        75,
        0,
        26,
        197,
        62,
        94,
        252,
        219,
        203,
        117,
        35,
        11,
        32,
        57,
        177,
        33,
        88,
        237,
        149,
        56,
        87,
        174,
        20,
        125,
        136,
        171,
        168,
        68,
        175,
        74,
        165,
        71,
        134,
        139,
        48,
        27,
        166,
        77,
        146,
        158,
        231,
        83,
        111,
        229,
        122,
        60,
        211,
        133,
        230,
        220,
        105,
        92,
        41,
        55,
        46,
        245,
        40,
        244,
        102,
        143,
        54,
        65,
        25,
        63,
        161,
        1,
        216,
        80,
        73,
        209,
        76,
        132,
        187,
        208,
        89,
        18,
        169,
        200,
        196,
        135,
        130,
        116,
        188,
        159,
        86,
        164,
        100,
        109,
        198,
        173,
        186,
        3,
        64,
        52,
        217,
        226,
        250,
        124,
        123,
        5,
        202,
        38,
        147,
        118,
        126,
        255,
        82,
        85,
        212,
        207,
        206,
        59,
        227,
        47,
        16,
        58,
        17,
        182,
        189,
        28,
        42,
        223,
        183,
        170,
        213,
        119,
        248,
        152,
        2,
        44,
        154,
        163,
        70,
        221,
        153,
        101,
        155,
        167,
        43,
        172,
        9,
        129,
        22,
        39,
        253,
        19,
        98,
        108,
        110,
        79,
        113,
        224,
        232,
        178,
        185,
        112,
        104,
        218,
        246,
        97,
        228,
        251,
        34,
        242,
        193,
        238,
        210,
        144,
        12,
        191,
        179,
        162,
        241,
        81,
        51,
        145,
        235,
        249,
        14,
        239,
        107,
        49,
        192,
        214,
        31,
        181,
        199,
        106,
        157,
        184,
        84,
        204,
        176,
        115,
        121,
        50,
        45,
        127,
        4,
        150,
        254,
        138,
        236,
        205,
        93,
        222,
        114,
        67,
        29,
        24,
        72,
        243,
        141,
        128,
        195,
        78,
        66,
        215,
        61,
        156,
        180,
        151,
        160,
        137,
        91,
        90,
        15,
        131,
        13,
        201,
        95,
        96,
        53,
        194,
        233,
        7,
        225,
        140,
        36,
        103,
        30,
        69,
        142,
        8,
        99,
        37,
        240,
        21,
        10,
        23,
        190,
        6,
        148,
        247,
        120,
        234,
        75,
        0,
        26,
        197,
        62,
        94,
        252,
        219,
        203,
        117,
        35,
        11,
        32,
        57,
        177,
        33,
        88,
        237,
        149,
        56,
        87,
        174,
        20,
        125,
        136,
        171,
        168,
        68,
        175,
        74,
        165,
        71,
        134,
        139,
        48,
        27,
        166,
        77,
        146,
        158,
        231,
        83,
        111,
        229,
        122,
        60,
        211,
        133,
        230,
        220,
        105,
        92,
        41,
        55,
        46,
        245,
        40,
        244,
        102,
        143,
        54,
        65,
        25,
        63,
        161,
        1,
        216,
        80,
        73,
        209,
        76,
        132,
        187,
        208,
        89,
        18,
        169,
        200,
        196,
        135,
        130,
        116,
        188,
        159,
        86,
        164,
        100,
        109,
        198,
        173,
        186,
        3,
        64,
        52,
        217,
        226,
        250,
        124,
        123,
        5,
        202,
        38,
        147,
        118,
        126,
        255,
        82,
        85,
        212,
        207,
        206,
        59,
        227,
        47,
        16,
        58,
        17,
        182,
        189,
        28,
        42,
        223,
        183,
        170,
        213,
        119,
        248,
        152,
        2,
        44,
        154,
        163,
        70,
        221,
        153,
        101,
        155,
        167,
        43,
        172,
        9,
        129,
        22,
        39,
        253,
        19,
        98,
        108,
        110,
        79,
        113,
        224,
        232,
        178,
        185,
        112,
        104,
        218,
        246,
        97,
        228,
        251,
        34,
        242,
        193,
        238,
        210,
        144,
        12,
        191,
        179,
        162,
        241,
        81,
        51,
        145,
        235,
        249,
        14,
        239,
        107,
        49,
        192,
        214,
        31,
        181,
        199,
        106,
        157,
        184,
        84,
        204,
        176,
        115,
        121,
        50,
        45,
        127,
        4,
        150,
        254,
        138,
        236,
        205,
        93,
        222,
        114,
        67,
        29,
        24,
        72,
        243,
        141,
        128,
        195,
        78,
        66,
        215,
        61,
        156,
        180
    ];

    public static float Dot(int[] g, float x, float y)
    {
        return g[0] * x + g[1] * y;
    }

    public static float Dot(int[] g, float x, float y, float z)
    {
        return g[0] * x + g[1] * y + g[2] * z;
    }

    public static float Hash(int x)
    {
        x = (x << 13) ^ x;
        return ((x * (x * x * 15731 + 789221) + 1376312589) & 0x7FFFFFFF) / 2.14748365E+09f;
    }

    public static float Noise(float x)
    {
        var num = (int)MathUtils.Floor(x);
        var x2 = (int)MathUtils.Ceiling(x);
        var num2 = x - num;
        var num3 = Hash(num);
        var num4 = Hash(x2);
        return num3 + num2 * num2 * (3f - 2f * num2) * (num4 - num3);
    }

    public static float Noise(float x, float y)
    {
        var num = (x + y) * 0.366025418f;
        var num2 = (int)MathUtils.Floor(x + num);
        var num3 = (int)MathUtils.Floor(y + num);
        var num4 = (num2 + num3) * 0.211324871f;
        var num5 = num2 - num4;
        var num6 = num3 - num4;
        var num7 = x - num5;
        var num8 = y - num6;
        int num9;
        int num10;
        if (num7 > num8)
        {
            num9 = 1;
            num10 = 0;
        }
        else
        {
            num9 = 0;
            num10 = 1;
        }

        var num11 = num7 - num9 + 0.211324871f;
        var num12 = num8 - num10 + 0.211324871f;
        var num13 = num7 - 1f + 0.422649741f;
        var num14 = num8 - 1f + 0.422649741f;
        var num15 = num2 & 0xFF;
        var num16 = num3 & 0xFF;
        var num17 = _permutations[num15 + _permutations[num16]] % 12;
        var num18 = _permutations[num15 + num9 + _permutations[num16 + num10]] % 12;
        var num19 = _permutations[num15 + 1 + _permutations[num16 + 1]] % 12;
        var num20 = 0.5f - num7 * num7 - num8 * num8;
        float num21;
        if (num20 < 0f)
        {
            num21 = 0f;
        }
        else
        {
            num20 *= num20;
            num21 = num20 * num20 * Dot(_grad3[num17], num7, num8);
        }

        var num22 = 0.5f - num11 * num11 - num12 * num12;
        float num23;
        if (num22 < 0f)
        {
            num23 = 0f;
        }
        else
        {
            num22 *= num22;
            num23 = num22 * num22 * Dot(_grad3[num18], num11, num12);
        }

        var num24 = 0.5f - num13 * num13 - num14 * num14;
        float num25;
        if (num24 < 0f)
        {
            num25 = 0f;
        }
        else
        {
            num24 *= num24;
            num25 = num24 * num24 * Dot(_grad3[num19], num13, num14);
        }

        return 35f * (num21 + num23 + num25) + 0.5f;
    }

    public static float Noise(float x, float y, float z)
    {
        var num = (x + y + z) * 0.333333343f;
        var num2 = (int)MathUtils.Floor(x + num);
        var num3 = (int)MathUtils.Floor(y + num);
        var num4 = (int)MathUtils.Floor(z + num);
        var num5 = (num2 + num3 + num4) * (355f / (678f * (float)Math.PI));
        var num6 = num2 - num5;
        var num7 = num3 - num5;
        var num8 = num4 - num5;
        var num9 = x - num6;
        var num10 = y - num7;
        var num11 = z - num8;
        int num12;
        int num13;
        int num14;
        int num15;
        int num16;
        int num17;
        if (num9 >= num10)
        {
            if (num10 >= num11)
            {
                num12 = 1;
                num13 = 0;
                num14 = 0;
                num15 = 1;
                num16 = 1;
                num17 = 0;
            }
            else if (num9 >= num11)
            {
                num12 = 1;
                num13 = 0;
                num14 = 0;
                num15 = 1;
                num16 = 0;
                num17 = 1;
            }
            else
            {
                num12 = 0;
                num13 = 0;
                num14 = 1;
                num15 = 1;
                num16 = 0;
                num17 = 1;
            }
        }
        else if (num10 < num11)
        {
            num12 = 0;
            num13 = 0;
            num14 = 1;
            num15 = 0;
            num16 = 1;
            num17 = 1;
        }
        else if (num9 < num11)
        {
            num12 = 0;
            num13 = 1;
            num14 = 0;
            num15 = 0;
            num16 = 1;
            num17 = 1;
        }
        else
        {
            num12 = 0;
            num13 = 1;
            num14 = 0;
            num15 = 1;
            num16 = 1;
            num17 = 0;
        }

        var num18 = num9 - num12 + 355f / (678f * (float)Math.PI);
        var num19 = num10 - num13 + 355f / (678f * (float)Math.PI);
        var num20 = num11 - num14 + 355f / (678f * (float)Math.PI);
        var num21 = num9 - num15 + 0.333333343f;
        var num22 = num10 - num16 + 0.333333343f;
        var num23 = num11 - num17 + 0.333333343f;
        var num24 = num9 - 1f + 0.5f;
        var num25 = num10 - 1f + 0.5f;
        var num26 = num11 - 1f + 0.5f;
        var num27 = num2 & 0xFF;
        var num28 = num3 & 0xFF;
        var num29 = num4 & 0xFF;
        var num30 = _permutations[num27 + _permutations[num28 + _permutations[num29]]] % 12;
        var num31 = _permutations[num27 + num12 + _permutations[num28 + num13 + _permutations[num29 + num14]]] % 12;
        var num32 = _permutations[num27 + num15 + _permutations[num28 + num16 + _permutations[num29 + num17]]] % 12;
        var num33 = _permutations[num27 + 1 + _permutations[num28 + 1 + _permutations[num29 + 1]]] % 12;
        var num34 = 0.6f - num9 * num9 - num10 * num10 - num11 * num11;
        float num35;
        if (num34 < 0f)
        {
            num35 = 0f;
        }
        else
        {
            num34 *= num34;
            num35 = num34 * num34 * Dot(_grad3[num30], num9, num10, num11);
        }

        var num36 = 0.6f - num18 * num18 - num19 * num19 - num20 * num20;
        float num37;
        if (num36 < 0f)
        {
            num37 = 0f;
        }
        else
        {
            num36 *= num36;
            num37 = num36 * num36 * Dot(_grad3[num31], num18, num19, num20);
        }

        var num38 = 0.6f - num21 * num21 - num22 * num22 - num23 * num23;
        float num39;
        if (num38 < 0f)
        {
            num39 = 0f;
        }
        else
        {
            num38 *= num38;
            num39 = num38 * num38 * Dot(_grad3[num32], num21, num22, num23);
        }

        var num40 = 0.6f - num24 * num24 - num25 * num25 - num26 * num26;
        float num41;
        if (num40 < 0f)
        {
            num41 = 0f;
        }
        else
        {
            num40 *= num40;
            num41 = num40 * num40 * Dot(_grad3[num33], num24, num25, num26);
        }

        return 16f * (num35 + num37 + num39 + num41) + 0.5f;
    }

    public static float OctavedNoise(
        float x,
        float frequency,
        int octaves,
        float frequencyStep,
        float amplitudeStep,
        bool ridged = false
    )
    {
        var num = 0f;
        var num2 = 0f;
        var num3 = 1f;
        for (var i = 0; i < octaves; i++)
        {
            num += num3 * Noise(x * frequency);
            num2 += num3;
            frequency *= frequencyStep;
            num3 *= amplitudeStep;
        }

        if (!ridged)
        {
            return num / num2;
        }

        return 1f - MathUtils.Abs(2f * num / num2 - 1f);
    }

    public static float OctavedNoise(
        float x,
        float y,
        float frequency,
        int octaves,
        float frequencyStep,
        float amplitudeStep,
        bool ridged = false
    )
    {
        var num = 0f;
        var num2 = 0f;
        var num3 = 1f;
        for (var i = 0; i < octaves; i++)
        {
            num += num3 * Noise(x * frequency, y * frequency);
            num2 += num3;
            frequency *= frequencyStep;
            num3 *= amplitudeStep;
        }

        if (!ridged)
        {
            return num / num2;
        }

        return 1f - MathUtils.Abs(2f * num / num2 - 1f);
    }

    public static float OctavedNoise(
        float x,
        float y,
        float z,
        float frequency,
        int octaves,
        float frequencyStep,
        float amplitudeStep,
        bool ridged = false
    )
    {
        var num = 0f;
        var num2 = 0f;
        var num3 = 1f;
        for (var i = 0; i < octaves; i++)
        {
            num += num3 * Noise(x * frequency, y * frequency, z * frequency);
            num2 += num3;
            frequency *= frequencyStep;
            num3 *= amplitudeStep;
        }

        if (!ridged)
        {
            return num / num2;
        }

        return 1f - MathUtils.Abs(2f * num / num2 - 1f);
    }

    public static float NewOctavedNoise(
        float x,
        float y,
        float z,
        float frequency,
        int octaves,
        float frequencyStep,
        float amplitudeStep,
        bool ridged = false
    )
    {
        var num = 0f;
        var num2 = 0f;
        var num3 = 1f;
        for (var i = 0; i < octaves; i++)
        {
            var num4 = Noise(x * frequency, y * frequency, z * frequency);
            if (i == octaves - 1)
            {
                num4 = (float)Math.Pow(num4, 3.0); //最后一层八度乘以3次方
            }

            num += num3 * num4;
            num2 += num3;
            frequency *= frequencyStep;
            num3 *= amplitudeStep;
        }

        var num5 = num / num2;
        if (ridged)
        {
            num5 = 1f - MathUtils.Abs(2f * num5 - 1f);
        }

        return num5;
    }
}
