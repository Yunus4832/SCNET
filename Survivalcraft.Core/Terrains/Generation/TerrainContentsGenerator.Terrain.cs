namespace Game.Terrains.Generation;

public partial class TerrainContentsGenerator
{
    public void GenerateSurfaceParameters(TerrainChunk chunk, int x1, int z1, int x2, int z2)
    {
        for (var localX = x1; localX < x2; localX++)
        {
            for (var localZ = z1; localZ < z2; localZ++)
            {
                var worldX = localX + chunk.Origin.X;
                var worldZ = localZ + chunk.Origin.Y;
                var temperature = CalculateTemperature(worldX, worldZ);
                var humidity = CalculateHumidity(worldX, worldZ);
                chunk.SetTemperatureFast(localX, localZ, temperature);
                chunk.SetHumidityFast(localX, localZ, humidity);
            }
        }
    }

    public void GenerateTerrain(TerrainChunk chunk, int x1, int z1, int x2, int z2)
    {
        var width = x2 - x1;
        var depth = z2 - z1;
        var worldStartX = chunk.Origin.X + x1;
        var worldStartZ = chunk.Origin.Y + z1;
        var shoreDistances = new Grid2D(width, depth);
        var mountainFactors = new Grid2D(width, depth);
        for (var offsetZ = 0; offsetZ < depth; offsetZ++)
        {
            for (var offsetX = 0; offsetX < width; offsetX++)
            {
                shoreDistances.Set(
                    offsetX,
                    offsetZ,
                    CalculateOceanShoreDistance(offsetX + worldStartX, offsetZ + worldStartZ)
                );
                mountainFactors.Set(
                    offsetX,
                    offsetZ,
                    CalculateMountainRangeFactor(offsetX + worldStartX, offsetZ + worldStartZ)
                );
            }
        }

        var densities = new Grid3D(width / 4 + 1, 33, depth / 4 + 1);
        for (var sampleX = 0; sampleX < densities.SizeX; sampleX++)
        {
            for (var sampleZ = 0; sampleZ < densities.SizeZ; sampleZ++)
            {
                var worldX = sampleX * 4 + worldStartX;
                var worldZ = sampleZ * 4 + worldStartZ;
                var surfaceHeight = CalculateHeight(worldX, worldZ);
                var mountainFactor = CalculateMountainRangeFactor(worldX, worldZ);
                var turbulenceScale = MathUtils.Lerp(tgMinTurbulence, 1f, Squish(mountainFactor, tgTurbulenceZero, 1f));
                for (var sampleY = 0; sampleY < densities.SizeY; sampleY++)
                {
                    var worldY = sampleY * 8;
                    var turbulence = tgTurbulenceStrength *
                                     turbulenceScale *
                                     MathUtils.Saturate(surfaceHeight - worldY) *
                                     (2f *
                                      SimplexNoise.OctavedNoise(
                                          worldX,
                                          worldY,
                                          worldZ,
                                          tgTurbulenceFreq,
                                          tgTurbulenceOctaves,
                                          4f,
                                          tgTurbulencePersistence
                                      ) -
                                      1f);
                    var displacedY = worldY + turbulence;
                    var density = surfaceHeight - displacedY;
                    density += MathUtils.Max(4f * (tgDensityBias - worldY), 0f);
                    densities.Set(sampleX, sampleY, sampleZ, density);
                }
            }
        }

        var oceanLevel = OceanLevel;
        for (var gridX = 0; gridX < densities.SizeX - 1; gridX++)
        {
            for (var gridZ = 0; gridZ < densities.SizeZ - 1; gridZ++)
            {
                for (var gridY = 0; gridY < densities.SizeY - 1; gridY++)
                {
                    densities.Get8(
                        gridX,
                        gridY,
                        gridZ,
                        out var lowerNearLeft,
                        out var lowerNearRight,
                        out var upperNearLeft,
                        out var upperNearRight,
                        out var lowerFarLeft,
                        out var lowerFarRight,
                        out var upperFarLeft,
                        out var upperFarRight
                    );
                    var lowerNearStepX = (lowerNearRight - lowerNearLeft) / 4f;
                    var upperNearStepX = (upperNearRight - upperNearLeft) / 4f;
                    var lowerFarStepX = (lowerFarRight - lowerFarLeft) / 4f;
                    var upperFarStepX = (upperFarRight - upperFarLeft) / 4f;
                    var lowerNear = lowerNearLeft;
                    var upperNear = upperNearLeft;
                    var lowerFar = lowerFarLeft;
                    var upperFar = upperFarLeft;
                    for (var cellOffsetX = 0; cellOffsetX < 4; cellOffsetX++)
                    {
                        var lowerStepZ = (lowerFar - lowerNear) / 4f;
                        var upperStepZ = (upperFar - upperNear) / 4f;
                        var lower = lowerNear;
                        var upper = upperNear;
                        for (var cellOffsetZ = 0; cellOffsetZ < 4; cellOffsetZ++)
                        {
                            var stepY = (upper - lower) / 8f;
                            var cellDensity = lower;
                            var offsetX = cellOffsetX + gridX * 4;
                            var offsetZ = cellOffsetZ + gridZ * 4;
                            var localX = x1 + offsetX;
                            var localZ = z1 + offsetZ;
                            var shoreDistance = shoreDistances.Get(offsetX, offsetZ);
                            var columnMountainFactor = mountainFactors.Get(offsetX, offsetZ);
                            var temperature = chunk.GetTemperatureFast(localX, localZ);
                            var humidity = chunk.GetHumidityFast(localX, localZ);
                            var rockDensityFactor = columnMountainFactor - 0.01f * humidity;
                            var sandstoneThreshold = MathUtils.Lerp(100f, 0f, rockDensityFactor);
                            var basaltThreshold = MathUtils.Lerp(300f, 30f, rockDensityFactor);
                            var sandyColumn = (temperature > 8 && humidity < 8 && columnMountainFactor < 0.97f) ||
                                              (MathUtils.Abs(shoreDistance) < 16f && columnMountainFactor < 0.97f);
                            var columnIndex = TerrainChunk.CalculateCellIndex(localX, 0, localZ);
                            for (var cellOffsetY = 0; cellOffsetY < 8; cellOffsetY++)
                            {
                                var cellY = cellOffsetY + gridY * 8;
                                var value = 0;
                                if (cellDensity < 0f)
                                {
                                    if (cellY <= oceanLevel)
                                    {
                                        value = 18;
                                    }
                                }
                                else if (sandyColumn && cellDensity < sandstoneThreshold)
                                {
                                    value = 4;
                                }
                                else
                                {
                                    value = cellDensity < basaltThreshold ? 3 : 67;
                                }

                                chunk.SetCellValueFast(columnIndex + cellY, value);
                                cellDensity += stepY;
                            }

                            lower += lowerStepZ;
                            upper += upperStepZ;
                        }

                        lowerNear += lowerNearStepX;
                        upperNear += upperNearStepX;
                        lowerFar += lowerFarStepX;
                        upperFar += upperFarStepX;
                    }
                }
            }
        }
    }

    public void GenerateSurface(TerrainChunk chunk)
    {
        var random = new Random(seed + chunk.Coords.X + 101 * chunk.Coords.Y);
        for (var localX = 0; localX < 16; localX++)
        {
            for (var localZ = 0; localZ < 16; localZ++)
            {
                var worldX = localX + chunk.Origin.X;
                var worldZ = localZ + chunk.Origin.Y;
                var cellIndex = TerrainChunk.CalculateCellIndex(localX, 254, localZ);
                var surfaceY = 254;
                while (surfaceY >= 0)
                {
                    var baseContents = Terrain.ExtractContents(chunk.GetCellValueFast(cellIndex));
                    if (!_generationBlocks[baseContents].Transparent)
                    {
                        var mountainFactor = CalculateMountainRangeFactor(worldX, worldZ);
                        var temperature = chunk.GetTemperatureFast(localX, localZ);
                        var humidity = chunk.GetHumidityFast(localX, localZ);
                        int surfaceContents;
                        if (baseContents == 4) //如果是砂岩
                        {
                            if (temperature > 4 && temperature < 7)
                            {
                                surfaceContents = 6; //如果温度大于4小于7，则设置为沙砾
                            }
                            else
                            {
                                surfaceContents = 7; //否则，设置为沙子
                            }
                        }
                        else
                        {
                            var temperatureBand = temperature / 4;
                            int contentsAbove;
                            if (surfaceY + 1 < 255)
                            {
                                contentsAbove = chunk.GetCellContentsFast(localX, surfaceY + 1, localZ);
                            }
                            else
                            {
                                contentsAbove = 0;
                            }

                            if (surfaceY > 170 &&
                                SubsystemWeather.IsPlaceFrozen(temperature, surfaceY)) //如果高度大于170且为冰封区
                            {
                                surfaceContents = 62; //设置为冰块
                            }
                            else if ((surfaceY < 66 ||
                                      surfaceY == 84 + temperatureBand ||
                                      surfaceY == 103 + temperatureBand) &&
                                     humidity == 9 &&
                                     temperature % 6 == 1)
                            {
                                surfaceContents = 66; //石灰石
                            }
                            else if (contentsAbove != 18 || humidity <= 8 || humidity % 2 != 0 || temperature % 3 != 0)
                            {
                                surfaceContents = 2; //泥土
                            }
                            else
                            {
                                surfaceContents = 72; //粘土
                            }
                        }

                        int layerDepth;
                        if (surfaceContents == 62)
                        {
                            layerDepth = (int)MathUtils.Clamp(1f * -temperature, 1f, 7f);
                        }
                        else
                        {
                            var altitudeFactor = MathUtils.Saturate((surfaceY - 100f) * 0.05f);
                            var thinningFactor = MathUtils.Saturate(
                                MathUtils.Saturate((mountainFactor - 0.9f) / 0.1f) -
                                MathUtils.Saturate((humidity - 3f) / 12f) +
                                tgSurfaceMultiplier * altitudeFactor
                            );
                            var minDepth = (int)MathUtils.Lerp(4f, 0f, thinningFactor);
                            var maxDepth = (int)MathUtils.Lerp(7f, 0f, thinningFactor);
                            layerDepth = MathUtils.Min(random.Int(minDepth, maxDepth), surfaceY);
                        }

                        var layerEndIndex = TerrainChunk.CalculateCellIndex(localX, surfaceY + 1, localZ);
                        for (var layerIndex = layerEndIndex - layerDepth; layerIndex < layerEndIndex; layerIndex++)
                        {
                            if (Terrain.ExtractContents(chunk.GetCellValueFast(layerIndex)) == 0)
                            {
                                continue;
                            }

                            var value = Terrain.ReplaceContents(0, surfaceContents);
                            chunk.SetCellValueFast(layerIndex, value);
                        }

                        break;
                    }

                    surfaceY--;
                    cellIndex--;
                }
            }
        }
    }

    public class Grid2D
    {
        private readonly float[] _data;

        private readonly int _sizeX;

        private readonly int _sizeY;

        public Grid2D(int sizeX, int sizeY)
        {
            _sizeX = sizeX;
            _sizeY = sizeY;
            _data = new float[_sizeX * _sizeY];
        }

        public int SizeX => _sizeX;

        public int SizeY => _sizeY;

        public float Get(int x, int y)
        {
            return _data[x + y * _sizeX];
        }

        public void Set(int x, int y, float value)
        {
            _data[x + y * _sizeX] = value;
        }

        public float Sample(float x, float y)
        {
            var num = (int)MathUtils.Floor(x);
            var num2 = (int)MathUtils.Floor(y);
            var num3 = (int)MathUtils.Ceiling(x);
            var num4 = (int)MathUtils.Ceiling(y);
            var f = x - num;
            var f2 = y - num2;
            var x2 = _data[num + num2 * _sizeX];
            var x3 = _data[num3 + num2 * _sizeX];
            var x4 = _data[num + num4 * _sizeX];
            var x5 = _data[num3 + num4 * _sizeX];
            var x6 = MathUtils.Lerp(x2, x3, f);
            var x7 = MathUtils.Lerp(x4, x5, f);
            return MathUtils.Lerp(x6, x7, f2);
        }
    }

    public class Grid3D
    {
        private readonly float[] _data;

        private readonly int _sizeX;

        private readonly int _sizeXy;

        private readonly int _sizeY;

        private readonly int _sizeZ;

        public Grid3D(int sizeX, int sizeY, int sizeZ)
        {
            _sizeX = sizeX;
            _sizeY = sizeY;
            _sizeZ = sizeZ;
            _sizeXy = _sizeX * _sizeY;
            _data = new float[_sizeX * _sizeY * _sizeZ];
        }

        public int SizeX => _sizeX;

        public int SizeY => _sizeY;

        public int SizeZ => _sizeZ;

        public void Get8(
            int x,
            int y,
            int z,
            out float v111,
            out float v211,
            out float v121,
            out float v221,
            out float v112,
            out float v212,
            out float v122,
            out float v222
        )
        {
            var num = x + y * _sizeX + z * _sizeXy;
            v111 = _data[num];
            v211 = _data[num + 1];
            v121 = _data[num + _sizeX];
            v221 = _data[num + 1 + _sizeX];
            v112 = _data[num + _sizeXy];
            v212 = _data[num + 1 + _sizeXy];
            v122 = _data[num + _sizeX + _sizeXy];
            v222 = _data[num + 1 + _sizeX + _sizeXy];
        }

        public float Get(int x, int y, int z)
        {
            return _data[x + y * _sizeX + z * _sizeXy];
        }

        public void Set(int x, int y, int z, float value)
        {
            _data[x + y * _sizeX + z * _sizeXy] = value;
        }

        public float Sample(float x, float y, float z)
        {
            var num = (int)MathUtils.Floor(x);
            var num2 = (int)MathUtils.Ceiling(x);
            var num3 = (int)MathUtils.Floor(y);
            var num4 = (int)MathUtils.Ceiling(y);
            var num5 = (int)MathUtils.Floor(z);
            var num6 = (int)MathUtils.Ceiling(z);
            var f = x - num;
            var f2 = y - num3;
            var f3 = z - num5;
            var x2 = _data[num + num3 * _sizeX + num5 * _sizeX * _sizeY];
            var x3 = _data[num2 + num3 * _sizeX + num5 * _sizeX * _sizeY];
            var x4 = _data[num + num4 * _sizeX + num5 * _sizeX * _sizeY];
            var x5 = _data[num2 + num4 * _sizeX + num5 * _sizeX * _sizeY];
            var x6 = _data[num + num3 * _sizeX + num6 * _sizeX * _sizeY];
            var x7 = _data[num2 + num3 * _sizeX + num6 * _sizeX * _sizeY];
            var x8 = _data[num + num4 * _sizeX + num6 * _sizeX * _sizeY];
            var x9 = _data[num2 + num4 * _sizeX + num6 * _sizeX * _sizeY];
            var x10 = MathUtils.Lerp(x2, x3, f);
            var x11 = MathUtils.Lerp(x4, x5, f);
            var x12 = MathUtils.Lerp(x6, x7, f);
            var x13 = MathUtils.Lerp(x8, x9, f);
            var x14 = MathUtils.Lerp(x10, x11, f2);
            var x15 = MathUtils.Lerp(x12, x13, f2);
            return MathUtils.Lerp(x14, x15, f3);
        }
    }
}
