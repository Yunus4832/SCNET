namespace Engine.Core.Mathematics;

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
        var leftX = (int)MathUtils.Floor(x);
        var rightX = (int)MathUtils.Ceiling(x);
        var fraction = x - leftX;
        var leftValue = Hash(leftX);
        var rightValue = Hash(rightX);
        return leftValue + fraction * fraction * (3f - 2f * fraction) * (rightValue - leftValue);
    }

    public static float Noise(float x, float y)
    {
        var skew = (x + y) * 0.366025418f;
        var cellX = (int)MathUtils.Floor(x + skew);
        var cellY = (int)MathUtils.Floor(y + skew);
        var unskew = (cellX + cellY) * 0.211324871f;
        var originX = cellX - unskew;
        var originY = cellY - unskew;
        var dx0 = x - originX;
        var dy0 = y - originY;
        int stepX1;
        int stepY1;
        if (dx0 > dy0)
        {
            stepX1 = 1;
            stepY1 = 0;
        }
        else
        {
            stepX1 = 0;
            stepY1 = 1;
        }

        var dx1 = dx0 - stepX1 + 0.211324871f;
        var dy1 = dy0 - stepY1 + 0.211324871f;
        var dx2 = dx0 - 1f + 0.422649741f;
        var dy2 = dy0 - 1f + 0.422649741f;
        var wrappedX = cellX & 0xFF;
        var wrappedY = cellY & 0xFF;
        var gradient0 = _permutations[wrappedX + _permutations[wrappedY]] % 12;
        var gradient1 = _permutations[wrappedX + stepX1 + _permutations[wrappedY + stepY1]] % 12;
        var gradient2 = _permutations[wrappedX + 1 + _permutations[wrappedY + 1]] % 12;
        var attenuation0 = 0.5f - dx0 * dx0 - dy0 * dy0;
        float contribution0;
        if (attenuation0 < 0f)
        {
            contribution0 = 0f;
        }
        else
        {
            attenuation0 *= attenuation0;
            contribution0 = attenuation0 * attenuation0 * Dot(_grad3[gradient0], dx0, dy0);
        }

        var attenuation1 = 0.5f - dx1 * dx1 - dy1 * dy1;
        float contribution1;
        if (attenuation1 < 0f)
        {
            contribution1 = 0f;
        }
        else
        {
            attenuation1 *= attenuation1;
            contribution1 = attenuation1 * attenuation1 * Dot(_grad3[gradient1], dx1, dy1);
        }

        var attenuation2 = 0.5f - dx2 * dx2 - dy2 * dy2;
        float contribution2;
        if (attenuation2 < 0f)
        {
            contribution2 = 0f;
        }
        else
        {
            attenuation2 *= attenuation2;
            contribution2 = attenuation2 * attenuation2 * Dot(_grad3[gradient2], dx2, dy2);
        }

        return 35f * (contribution0 + contribution1 + contribution2) + 0.5f;
    }

    public static float Noise(float x, float y, float z)
    {
        var skew = (x + y + z) * 0.333333343f;
        var cellX = (int)MathUtils.Floor(x + skew);
        var cellY = (int)MathUtils.Floor(y + skew);
        var cellZ = (int)MathUtils.Floor(z + skew);
        var unskew = (cellX + cellY + cellZ) * (355f / (678f * (float)Math.PI));
        var originX = cellX - unskew;
        var originY = cellY - unskew;
        var originZ = cellZ - unskew;
        var dx0 = x - originX;
        var dy0 = y - originY;
        var dz0 = z - originZ;
        int stepX1;
        int stepY1;
        int stepZ1;
        int stepX2;
        int stepY2;
        int stepZ2;
        if (dx0 >= dy0)
        {
            if (dy0 >= dz0)
            {
                stepX1 = 1;
                stepY1 = 0;
                stepZ1 = 0;
                stepX2 = 1;
                stepY2 = 1;
                stepZ2 = 0;
            }
            else if (dx0 >= dz0)
            {
                stepX1 = 1;
                stepY1 = 0;
                stepZ1 = 0;
                stepX2 = 1;
                stepY2 = 0;
                stepZ2 = 1;
            }
            else
            {
                stepX1 = 0;
                stepY1 = 0;
                stepZ1 = 1;
                stepX2 = 1;
                stepY2 = 0;
                stepZ2 = 1;
            }
        }
        else if (dy0 < dz0)
        {
            stepX1 = 0;
            stepY1 = 0;
            stepZ1 = 1;
            stepX2 = 0;
            stepY2 = 1;
            stepZ2 = 1;
        }
        else if (dx0 < dz0)
        {
            stepX1 = 0;
            stepY1 = 1;
            stepZ1 = 0;
            stepX2 = 0;
            stepY2 = 1;
            stepZ2 = 1;
        }
        else
        {
            stepX1 = 0;
            stepY1 = 1;
            stepZ1 = 0;
            stepX2 = 1;
            stepY2 = 1;
            stepZ2 = 0;
        }

        var dx1 = dx0 - stepX1 + 355f / (678f * (float)Math.PI);
        var dy1 = dy0 - stepY1 + 355f / (678f * (float)Math.PI);
        var dz1 = dz0 - stepZ1 + 355f / (678f * (float)Math.PI);
        var dx2 = dx0 - stepX2 + 0.333333343f;
        var dy2 = dy0 - stepY2 + 0.333333343f;
        var dz2 = dz0 - stepZ2 + 0.333333343f;
        var dx3 = dx0 - 1f + 0.5f;
        var dy3 = dy0 - 1f + 0.5f;
        var dz3 = dz0 - 1f + 0.5f;
        var wrappedX = cellX & 0xFF;
        var wrappedY = cellY & 0xFF;
        var wrappedZ = cellZ & 0xFF;
        var gradient0 = _permutations[wrappedX + _permutations[wrappedY + _permutations[wrappedZ]]] % 12;
        var gradient1 =
            _permutations[wrappedX + stepX1 + _permutations[wrappedY + stepY1 + _permutations[wrappedZ + stepZ1]]] % 12;
        var gradient2 =
            _permutations[wrappedX + stepX2 + _permutations[wrappedY + stepY2 + _permutations[wrappedZ + stepZ2]]] % 12;
        var gradient3 = _permutations[wrappedX + 1 + _permutations[wrappedY + 1 + _permutations[wrappedZ + 1]]] % 12;
        var attenuation0 = 0.6f - dx0 * dx0 - dy0 * dy0 - dz0 * dz0;
        float contribution0;
        if (attenuation0 < 0f)
        {
            contribution0 = 0f;
        }
        else
        {
            attenuation0 *= attenuation0;
            contribution0 = attenuation0 * attenuation0 * Dot(_grad3[gradient0], dx0, dy0, dz0);
        }

        var attenuation1 = 0.6f - dx1 * dx1 - dy1 * dy1 - dz1 * dz1;
        float contribution1;
        if (attenuation1 < 0f)
        {
            contribution1 = 0f;
        }
        else
        {
            attenuation1 *= attenuation1;
            contribution1 = attenuation1 * attenuation1 * Dot(_grad3[gradient1], dx1, dy1, dz1);
        }

        var attenuation2 = 0.6f - dx2 * dx2 - dy2 * dy2 - dz2 * dz2;
        float contribution2;
        if (attenuation2 < 0f)
        {
            contribution2 = 0f;
        }
        else
        {
            attenuation2 *= attenuation2;
            contribution2 = attenuation2 * attenuation2 * Dot(_grad3[gradient2], dx2, dy2, dz2);
        }

        var attenuation3 = 0.6f - dx3 * dx3 - dy3 * dy3 - dz3 * dz3;
        float contribution3;
        if (attenuation3 < 0f)
        {
            contribution3 = 0f;
        }
        else
        {
            attenuation3 *= attenuation3;
            contribution3 = attenuation3 * attenuation3 * Dot(_grad3[gradient3], dx3, dy3, dz3);
        }

        return 16f * (contribution0 + contribution1 + contribution2 + contribution3) + 0.5f;
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
        var weightedSum = 0f;
        var totalAmplitude = 0f;
        var amplitude = 1f;
        for (var octave = 0; octave < octaves; octave++)
        {
            weightedSum += amplitude * Noise(x * frequency);
            totalAmplitude += amplitude;
            frequency *= frequencyStep;
            amplitude *= amplitudeStep;
        }

        if (!ridged)
        {
            return weightedSum / totalAmplitude;
        }

        return 1f - MathUtils.Abs(2f * weightedSum / totalAmplitude - 1f);
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
        var weightedSum = 0f;
        var totalAmplitude = 0f;
        var amplitude = 1f;
        for (var octave = 0; octave < octaves; octave++)
        {
            weightedSum += amplitude * Noise(x * frequency, y * frequency);
            totalAmplitude += amplitude;
            frequency *= frequencyStep;
            amplitude *= amplitudeStep;
        }

        if (!ridged)
        {
            return weightedSum / totalAmplitude;
        }

        return 1f - MathUtils.Abs(2f * weightedSum / totalAmplitude - 1f);
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
        var weightedSum = 0f;
        var totalAmplitude = 0f;
        var amplitude = 1f;
        for (var octave = 0; octave < octaves; octave++)
        {
            weightedSum += amplitude * Noise(x * frequency, y * frequency, z * frequency);
            totalAmplitude += amplitude;
            frequency *= frequencyStep;
            amplitude *= amplitudeStep;
        }

        if (!ridged)
        {
            return weightedSum / totalAmplitude;
        }

        return 1f - MathUtils.Abs(2f * weightedSum / totalAmplitude - 1f);
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
        var weightedSum = 0f;
        var totalAmplitude = 0f;
        var amplitude = 1f;
        for (var octave = 0; octave < octaves; octave++)
        {
            var octaveNoise = Noise(x * frequency, y * frequency, z * frequency);
            if (octave == octaves - 1)
            {
                octaveNoise = (float)Math.Pow(octaveNoise, 3.0); //最后一层八度乘以3次方
            }

            weightedSum += amplitude * octaveNoise;
            totalAmplitude += amplitude;
            frequency *= frequencyStep;
            amplitude *= amplitudeStep;
        }

        var normalizedNoise = weightedSum / totalAmplitude;
        if (ridged)
        {
            normalizedNoise = 1f - MathUtils.Abs(2f * normalizedNoise - 1f);
        }

        return normalizedNoise;
    }
}
