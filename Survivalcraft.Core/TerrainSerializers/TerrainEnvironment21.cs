namespace Game.TerrainSerializers;

/// <summary>Read-only terrain environment calculations without world mutation services.</summary>
public class TerrainEnvironment21 : ITerrainEnvironment
{
    protected readonly float tgSurfaceMultiplier;

    protected readonly Vector2 humidityOffset;

    protected readonly Vector2? islandSize;

    protected readonly Vector2 mountainsOffset;

    protected readonly Vector2 oceanCorner;

    protected readonly Vector2 riversOffset;

    protected readonly int seed;

    protected readonly Vector2 temperatureOffset;

    protected readonly float tgBiomeScaling;

    protected readonly bool tgCavesAndPockets;

    protected readonly float tgDensityBias;

    protected readonly bool tgExtras;

    protected readonly float tgHeightBias;

    protected readonly float tgHillsStrength;

    protected readonly float tgIslandsFrequency;

    protected readonly float tgMountainsPercentage;

    protected readonly float tgMountainsPeriod;

    protected readonly float tgMountainsStrength;

    protected readonly bool tgNewBiomeNoise;

    protected readonly float tgOceanSlope;

    protected readonly float tgOceanSlopeVariation;

    protected readonly float tgRiversStrength;

    protected readonly float tgShoreFluctuations;

    protected readonly float tgShoreFluctuationsScaling;

    protected readonly float tgTurbulencePower;

    protected readonly float tgTurbulenceStrength;

    protected readonly float tgTurbulenceTopOffset;

    protected readonly WorldSettings worldSettings;

    public TerrainEnvironment21(WorldSettings worldSettings, int seed)
    {
        this.worldSettings = worldSettings;
        this.seed = seed;
        islandSize = TerrainGenerationModes.IsIsland(worldSettings.TerrainGenerationMode)
            ? new Vector2?(worldSettings.IslandSize)
            : null;
        var oldRandom = new OldRandom(100 + seed);
        var random = new Random(seed);
        if (TerrainGenerationModes.IsPre21(worldSettings.TerrainGenerationMode))
        {
            oceanCorner = new Vector2(oldRandom.UniformFloat(2000f, 4000f), oldRandom.UniformFloat(2000f, 4000f));
            temperatureOffset = new Vector2(1000f, 1000f);
            humidityOffset = new Vector2(0f, 0f);
            mountainsOffset = new Vector2(0f, 0f);
            riversOffset = new Vector2(0f, 0f);
            tgNewBiomeNoise = false;
            tgBiomeScaling = 1f;
            tgShoreFluctuations = 100f;
            tgShoreFluctuationsScaling = 1f;
            tgOceanSlope = 0.015f;
            tgOceanSlopeVariation = 0f;
            tgIslandsFrequency = 0.017f;
            tgDensityBias = 57f;
            tgHeightBias = 1f;
            tgRiversStrength = 0f;
            tgMountainsStrength = 56f;
            tgMountainsPeriod = 0.0014f;
            tgMountainsPercentage = 0.15f;
            tgHillsStrength = 13f;
            tgTurbulenceStrength = 13f;
            tgTurbulenceTopOffset = 3f;
            tgTurbulencePower = 0.5f;
            tgSurfaceMultiplier = 1f;
            tgExtras = true;
            tgCavesAndPockets = true;
        }
        else
        {
            var num = islandSize.HasValue
                ? MathUtils.Min(islandSize.Value.X, islandSize.Value.Y)
                : 3.40282347E+38f;
            oceanCorner = new Vector2(random.UniformFloat(-100f, -100f), random.UniformFloat(-100f, -100f));
            temperatureOffset = new Vector2(random.UniformFloat(-2000f, 2000f), random.UniformFloat(-2000f, 2000f));
            humidityOffset = new Vector2(random.UniformFloat(-2000f, 2000f), random.UniformFloat(-2000f, 2000f));
            mountainsOffset = new Vector2(random.UniformFloat(-2000f, 2000f), random.UniformFloat(-2000f, 2000f));
            riversOffset = new Vector2(random.UniformFloat(-2000f, 2000f), random.UniformFloat(-2000f, 2000f));
            tgNewBiomeNoise = true;
            tgBiomeScaling = 1.5f * worldSettings.BiomeSize;
            tgShoreFluctuations = MathUtils.Clamp(2f * num, 0f, 150f);
            tgShoreFluctuationsScaling = MathUtils.Clamp(0.04f * num, 0.5f, 3f);
            tgOceanSlope = 0.006f;
            tgOceanSlopeVariation = 0.004f;
            tgIslandsFrequency = 0.01f;
            tgDensityBias = 55f;
            tgHeightBias = 1f;
            tgRiversStrength = 1f;
            tgMountainsStrength = 85f;
            tgMountainsPeriod = 0.0015f;
            tgMountainsPercentage = 0.15f;
            tgHillsStrength = 8f;
            tgTurbulenceStrength = 35f;
            tgTurbulenceTopOffset = 0f;
            tgTurbulencePower = 0.3f;
            tgSurfaceMultiplier = 2f;
            tgExtras = true;
            tgCavesAndPockets = true;
        }
    }

    public int OceanLevel => 64 + worldSettings.SeaLevelOffset;

    public float CalculateOceanShoreDistance(float x, float z)
    {
        if (islandSize.HasValue)
        {
            var num = CalculateOceanShoreX(z);
            var num2 = CalculateOceanShoreZ(x);
            var num3 = CalculateOceanShoreX(z + 1000f) + islandSize.Value.X;
            var num4 = CalculateOceanShoreZ(x + 1000f) + islandSize.Value.Y;
            return MathUtils.Min(x - num, z - num2, num3 - x, num4 - z);
        }

        var num5 = CalculateOceanShoreX(z);
        var num6 = CalculateOceanShoreZ(x);
        return MathUtils.Min(x - num5, z - num6);
    }

    public float CalculateMountainRangeFactor(float x, float z)
    {
        return 1f -
               MathUtils.Abs(
                   2f *
                   SimplexNoise.OctavedNoise(
                       x + mountainsOffset.X,
                       z + mountainsOffset.Y,
                       tgMountainsPeriod / tgBiomeScaling,
                       3,
                       1.91f,
                       0.75f
                   ) -
                   1f
               );
    }

    public float CalculateHeight(float x, float z)
    {
        var num = tgOceanSlope +
                  tgOceanSlopeVariation *
                  MathUtils.PowSign(
                      2f *
                      SimplexNoise.OctavedNoise(x + mountainsOffset.X, z + mountainsOffset.Y, 0.01f, 1, 2f, 0.5f) -
                      1f,
                      0.5f
                  );
        var num2 = CalculateOceanShoreDistance(x, z);
        var num3 = MathUtils.Saturate(1f - 0.05f * MathUtils.Abs(num2));
        var num4 = MathUtils.Saturate(MathUtils.Sin(tgIslandsFrequency * num2));
        var num5 = MathUtils.Saturate(MathUtils.Saturate((0f - num) * num2) - 0.85f * num4);
        var num6 = MathUtils.Saturate(MathUtils.Saturate(0.05f * (0f - num2 - 10f)) - num4);
        var num7 = CalculateMountainRangeFactor(x, z);
        var f = (1f - num3) * SimplexNoise.OctavedNoise(x, z, 0.001f / tgBiomeScaling, 2, 1.97f, 0.8f);
        var f2 = (1f - num3) * SimplexNoise.OctavedNoise(x, z, 0.0017f / tgBiomeScaling, 2, 1.93f, 0.7f);
        var num8 = (1f - num6) * (1f - num3) * MathUtils.Saturate((num7 - 0.6f) / 0.4f);
        var num9 = (1f - num6) * MathUtils.Saturate((num7 - (1f - tgMountainsPercentage)) / tgMountainsPercentage);
        var num10 = 2f * SimplexNoise.OctavedNoise(x, z, 0.02f, 3, 1.93f, 0.8f) - 1f;
        var num11 = 1.5f * SimplexNoise.OctavedNoise(x, z, 0.004f, 4, 1.98f, 0.9f) - 0.5f;
        var num12 = MathUtils.Lerp(
            60f,
            30f,
            MathUtils.Saturate(1f * num9 + 0.5f * num8 + MathUtils.Saturate(1f - num2 / 30f))
        );
        var x2 = MathUtils.Lerp(-2f, -4f, MathUtils.Saturate(num9 + 0.5f * num8));
        var num13 = MathUtils.Saturate(
            1.5f -
            num12 *
            MathUtils.Abs(
                2f *
                SimplexNoise.OctavedNoise(x + riversOffset.X, z + riversOffset.Y, 0.001f, 4, 2f, 0.5f) -
                1f
            )
        );
        var num14 = -50f * num5 + tgHeightBias;
        var num15 = MathUtils.Lerp(0f, 8f, f);
        var num16 = MathUtils.Lerp(0f, -6f, f2);
        var num17 = tgHillsStrength * num8 * num10;
        var num18 = tgMountainsStrength * num9 * num11;
        var f3 = tgRiversStrength * num13;
        var num19 = num14 + num15 + num16 + num18 + num17;
        var num20 = MathUtils.Min(MathUtils.Lerp(num19, x2, f3), num19);
        return MathUtils.Clamp(64f + num20, 10f, 251f);
    }

    public int CalculateTemperature(float x, float z)
    {
        if (tgNewBiomeNoise)
        {
            return MathUtils.Clamp(
                (int)(MathUtils.Saturate(
                          4f *
                          SimplexNoise.OctavedNoise(
                              x + temperatureOffset.X,
                              z + temperatureOffset.Y,
                              0.0015f / tgBiomeScaling,
                              5,
                              2f,
                              0.7f
                          ) -
                          1.6f +
                          worldSettings.TemperatureOffset / 16f
                      ) *
                      16f),
                0,
                15
            );
        }

        return MathUtils.Clamp(
            (int)((MathUtils.Saturate(
                       4f *
                       SimplexNoise.OctavedNoise(
                           x + temperatureOffset.X,
                           z + temperatureOffset.Y,
                           0.0006f / tgBiomeScaling,
                           4,
                           1.93f,
                           1f
                       ) -
                       1.6f
                   ) +
                   worldSettings.TemperatureOffset / 16f) *
                  16f),
            0,
            15
        );
    }

    public int CalculateHumidity(float x, float z)
    {
        if (tgNewBiomeNoise)
        {
            return MathUtils.Clamp(
                (int)(MathUtils.Saturate(
                          4f *
                          SimplexNoise.OctavedNoise(
                              x + humidityOffset.X,
                              z + humidityOffset.Y,
                              0.0012f / tgBiomeScaling,
                              5,
                              2f,
                              0.7f
                          ) -
                          1.2f +
                          worldSettings.HumidityOffset / 16f
                      ) *
                      16f),
                0,
                15
            );
        }

        return MathUtils.Clamp(
            (int)((MathUtils.Saturate(
                       4f *
                       SimplexNoise.OctavedNoise(
                           x + humidityOffset.X,
                           z + humidityOffset.Y,
                           0.0008f / tgBiomeScaling,
                           5,
                           1.97f,
                           1f
                       ) -
                       1.5f
                   ) +
                   worldSettings.HumidityOffset / 16f) *
                  16f),
            0,
            15
        );
    }

    public float CalculateOceanShoreX(float z)
    {
        return oceanCorner.X +
               tgShoreFluctuations *
               SimplexNoise.OctavedNoise(z, 0f, 0.005f / tgShoreFluctuationsScaling, 4, 1.95f, 1f);
    }

    public float CalculateOceanShoreZ(float x)
    {
        return oceanCorner.Y +
               tgShoreFluctuations *
               SimplexNoise.OctavedNoise(0f, x, 0.005f / tgShoreFluctuationsScaling, 4, 1.95f, 1f);
    }

    public class Random
    {
        private const ulong _multiplier = 25214903917uL;

        private const ulong _addend = 11uL;

        private const ulong _mask = 281474976710655uL;

        private static int _counter = (int)Stopwatch.GetTimestamp();

        public static readonly Random GlobalRandom = new(0);

        private ulong _seed;

        public Random()
            : this(997 * _counter++)
        {
        }

        public Random(int seed)
        {
            Reset(seed);
        }

        public void Reset(int seed)
        {
            _seed = (ulong)(seed ^ 0x5DEECE66D);
        }

        public int Sign()
        {
            if (Int() % 2 != 0)
            {
                return 1;
            }

            return -1;
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
            _seed = (_seed * 25214903917L + 11) & 0xFFFFFFFFFFFF;
            return (int)(_seed >> 17);
        }

        public int UniformInt(int min, int max)
        {
            return (int)(min + Int() * (long)(max - min + 1) / 2147483648L);
        }

        public float UniformFloat(float min, float max)
        {
            var num = Int() / 2.147484E+09f;
            return min + num * (max - min);
        }

        public float NormalFloat(float mean, float stddev)
        {
            var num = UniformFloat(0f, 1f);
            if (num < 0.5)
            {
                var num2 = MathUtils.Sqrt(-2f * MathUtils.Log(num));
                var num3 = 0.322232425f +
                           num2 * (1f + num2 * (0.3422421f + num2 * (0.0204231218f + num2 * 4.536422E-05f)));
                var num4 = 0.09934846f +
                           num2 * (0.588581562f + num2 * (0.5311035f + num2 * (0.103537753f + num2 * 0.00385607f)));
                return mean + stddev * (num3 / num4 - num2);
            }

            var num5 = MathUtils.Sqrt(-2f * MathUtils.Log(1f - num));
            var num6 = 0.322232425f + num5 * (1f + num5 * (0.3422421f + num5 * (0.0204231218f + num5 * 4.536422E-05f)));
            var num7 = 0.09934846f +
                       num5 * (0.588581562f + num5 * (0.5311035f + num5 * (0.103537753f + num5 * 0.00385607f)));
            return mean - stddev * (num6 / num7 - num5);
        }

        public Vector2 Vector2(float length, bool circular = false)
        {
            Vector2 v;
            float num;
            do
            {
                v = new Vector2(UniformFloat(-1f, 1f), UniformFloat(-1f, 1f));
                num = v.LengthSquared();
            } while (circular && num > 1f);

            return v * (length / MathUtils.Sqrt(num));
        }

        public Vector2 UniformVector2(float minLength, float maxLength, bool circular = false)
        {
            Vector2 v;
            float num;
            do
            {
                v = new Vector2(UniformFloat(-1f, 1f), UniformFloat(-1f, 1f));
                num = v.LengthSquared();
            } while (circular && num > 1f);

            return v * (UniformFloat(minLength, maxLength) / MathUtils.Sqrt(num));
        }

        public Vector3 Vector3(float length, bool spherical = false)
        {
            Vector3 v;
            float num;
            do
            {
                v = new Vector3(UniformFloat(-1f, 1f), UniformFloat(-1f, 1f), UniformFloat(-1f, 1f));
                num = v.LengthSquared();
            } while (spherical && num > 1f);

            return v * (length / MathUtils.Sqrt(num));
        }

        public Vector3 UniformVector3(float minLength, float maxLength, bool spherical = false)
        {
            Vector3 v;
            float num;
            do
            {
                v = new Vector3(UniformFloat(-1f, 1f), UniformFloat(-1f, 1f), UniformFloat(-1f, 1f));
                num = v.LengthSquared();
            } while (spherical && num > 1f);

            return v * (UniformFloat(minLength, maxLength) / MathUtils.Sqrt(num));
        }
    }

    public class OldRandom
    {
        private static int _seed = (int)Stopwatch.GetTimestamp();

        public static readonly OldRandom GlobalRandom = new(0);

        private InternalRandom _random;

        public OldRandom()
        {
            _random = new InternalRandom(997 * _seed++);
        }

        public OldRandom(int seed)
        {
            _random = new InternalRandom(seed);
        }

        public int Sign()
        {
            if (_random.Next() % 2 != 0)
            {
                return 1;
            }

            return -1;
        }

        public bool Bool()
        {
            return _random.Next() % 2 == 0;
        }

        public int UniformInt(int min, int max)
        {
            return _random.Next(min, max + 1);
        }

        public float UniformFloat(float min, float max)
        {
            return (float)_random.NextDouble() * (max - min) + min;
        }

        public float NormalFloat(float mean, float stddev)
        {
            var num = UniformFloat(0f, 1f);
            if (num < 0.5)
            {
                var num2 = MathUtils.Sqrt(-2f * MathUtils.Log(num));
                var num3 = 0.322232425f +
                           num2 * (1f + num2 * (0.3422421f + num2 * (0.0204231218f + num2 * 4.536422E-05f)));
                var num4 = 0.09934846f +
                           num2 * (0.588581562f + num2 * (0.5311035f + num2 * (0.103537753f + num2 * 0.00385607f)));
                return mean + stddev * (num3 / num4 - num2);
            }

            var num5 = MathUtils.Sqrt(-2f * MathUtils.Log(1f - num));
            var num6 = 0.322232425f + num5 * (1f + num5 * (0.3422421f + num5 * (0.0204231218f + num5 * 4.536422E-05f)));
            var num7 = 0.09934846f +
                       num5 * (0.588581562f + num5 * (0.5311035f + num5 * (0.103537753f + num5 * 0.00385607f)));
            return mean - stddev * (num6 / num7 - num5);
        }

        public Vector2 Vector2(float length)
        {
            return Engine.Core.Mathematics.Vector2.Normalize(
                       new Vector2(UniformFloat(-1f, 1f), UniformFloat(-1f, 1f))
                   ) *
                   length;
        }

        public Vector2 UniformVector2(float minLength, float maxLength)
        {
            return Engine.Core.Mathematics.Vector2.Normalize(
                       new Vector2(UniformFloat(-1f, 1f), UniformFloat(-1f, 1f))
                   ) *
                   UniformFloat(minLength, maxLength);
        }

        public Vector3 Vector3(float length)
        {
            return Engine.Core.Mathematics.Vector3.Normalize(
                       new Vector3(
                           UniformFloat(-1f, 1f),
                           UniformFloat(-1f, 1f),
                           UniformFloat(-1f, 1f)
                       )
                   ) *
                   length;
        }

        public Vector3 UniformVector3(float minLength, float maxLength)
        {
            return Engine.Core.Mathematics.Vector3.Normalize(
                       new Vector3(
                           UniformFloat(-1f, 1f),
                           UniformFloat(-1f, 1f),
                           UniformFloat(-1f, 1f)
                       )
                   ) *
                   UniformFloat(minLength, maxLength);
        }

        public class InternalRandom
        {
            private int _iNext;

            private int _iNextP;

            private readonly int[] _seedArray;

            public InternalRandom(int seed)
            {
                _seedArray = new int[56];
                var num = seed == -2147483648 ? 2147483647 : Math.Abs(seed);
                var num2 = 161803398 - num;
                _seedArray[55] = num2;
                var num3 = 1;
                for (var i = 1; i < 55; i++)
                {
                    var num4 = 21 * i % 55;
                    _seedArray[num4] = num3;
                    num3 = num2 - num3;
                    if (num3 < 0)
                    {
                        num3 += 2147483647;
                    }

                    num2 = _seedArray[num4];
                }

                for (var j = 1; j < 5; j++)
                {
                    for (var k = 1; k < 56; k++)
                    {
                        _seedArray[k] -= _seedArray[1 + (k + 30) % 55];
                        if (_seedArray[k] < 0)
                        {
                            _seedArray[k] += 2147483647;
                        }
                    }
                }

                _iNext = 0;
                _iNextP = 21;
            }

            public double GetSampleForLargeRange()
            {
                var num = InternalSample();
                if (InternalSample() % 2 == 0)
                {
                    num = -num;
                }

                return (num + 2147483646.0) / 4294967293.0;
            }

            public int InternalSample()
            {
                var iNext = _iNext;
                var iNextP = _iNextP;
                if (++iNext >= 56)
                {
                    iNext = 1;
                }

                if (++iNextP >= 56)
                {
                    iNextP = 1;
                }

                var num = _seedArray[iNext] - _seedArray[iNextP];
                if (num == 2147483647)
                {
                    num--;
                }

                if (num < 0)
                {
                    num += 2147483647;
                }

                _seedArray[iNext] = num;
                _iNext = iNext;
                _iNextP = iNextP;
                return num;
            }

            public int Next()
            {
                return InternalSample();
            }

            public int Next(int maxValue)
            {
                if (maxValue < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(maxValue));
                }

                return (int)(Sample() * maxValue);
            }

            public int Next(int minValue, int maxValue)
            {
                if (minValue > maxValue)
                {
                    throw new ArgumentOutOfRangeException(nameof(minValue));
                }

                long num = maxValue - minValue;
                if (num <= 2147483647)
                {
                    return (int)(Sample() * num) + minValue;
                }

                return (int)(long)(GetSampleForLargeRange() * num) + minValue;
            }

            public void NextBytes(byte[] buffer)
            {
                if (buffer == null)
                {
                    throw new ArgumentNullException(nameof(buffer));
                }

                for (var i = 0; i < buffer.Length; i++)
                {
                    buffer[i] = (byte)(InternalSample() % 256);
                }
            }

            public double NextDouble()
            {
                return Sample();
            }

            public double Sample()
            {
                return InternalSample() * 4.6566128752457969E-10;
            }
        }
    }
}
