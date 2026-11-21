namespace Game.Terrains.Generation;

public partial class TerrainContentsGenerator
{
    private static readonly List<TerrainBrush> _coalBrushes = [];

    private static readonly List<TerrainBrush> _ironBrushes = [];

    private static readonly List<TerrainBrush> _copperBrushes = [];

    private static readonly List<TerrainBrush> _saltpeterBrushes = [];

    private static readonly List<TerrainBrush> _sulphurBrushes = [];

    private static readonly List<TerrainBrush> _diamondBrushes = [];

    private static readonly List<TerrainBrush> _germaniumBrushes = [];

    private static readonly List<TerrainBrush> _dirtPocketBrushes = [];

    private static readonly List<TerrainBrush> _gravelPocketBrushes = [];

    private static readonly List<TerrainBrush> _limestonePocketBrushes = [];

    private static readonly List<TerrainBrush> _sandPocketBrushes = [];

    private static readonly List<TerrainBrush> _basaltPocketBrushes = [];

    private static readonly List<TerrainBrush> _granitePocketBrushes = [];

    private static readonly List<TerrainBrush> _clayPocketBrushes = [];

    private static readonly List<TerrainBrush> _waterPocketBrushes = [];

    private static readonly List<TerrainBrush> _magmaPocketBrushes = [];

    private static readonly List<List<TerrainBrush>> _caveBrushesByType = [];

    public static void CreateBrushes()
    {
        var random = new Random(24);
        // Brush families share a seeded random stream. Their order determines world generation.
        CreateVeinBrushes(_coalBrushes, 16, (4, 12), (3, 8), 1f, firstOriginContents: 150);
        CreateVeinBrushes(_ironBrushes, 39, (3, 7), (3, 6), 1f);
        CreateVeinBrushes(_copperBrushes, 41, (4, 10), (3, 6), 2f);
        CreateVeinBrushes(_saltpeterBrushes, 100, (8, 16), (4, 8), 0.25f);
        CreateVeinBrushes(_sulphurBrushes, 101, (4, 10), (3, 6), 1f);
        CreateVeinBrushes(_diamondBrushes, 112, (2, 6), (3, 6), 1f);
        CreateVeinBrushes(_germaniumBrushes, 148, (4, 10), (3, 6), 1f);
        CreateVeinBrushes(_dirtPocketBrushes, 2, (16, 32), (6, 12), 0.75f);
        CreateVeinBrushes(_gravelPocketBrushes, 6, (16, 32), (6, 12), 0.75f);
        CreateVeinBrushes(_limestonePocketBrushes, 66, (16, 32), (6, 12), 0.75f);
        CreateVeinBrushes(_clayPocketBrushes, 72, (16, 32), (6, 12), 0.1f);
        CreateVeinBrushes(_sandPocketBrushes, 7, (16, 32), (6, 12), 0.75f);
        CreateVeinBrushes(_basaltPocketBrushes, 67, (16, 32), (6, 12), 0.75f);
        CreateVeinBrushes(_granitePocketBrushes, 3, (16, 32), (5, 10), 1f);

        var waterBaseSize = new[] { 4, 6, 8 };
        for (var i = 0; i < 4 * waterBaseSize.Length; i++)
        {
            var waterPocketBrush = new TerrainBrush();
            var curBaseSize = waterBaseSize[i / 4];
            var verticalOffset = i % 2 + 1;
            var shapeCorrectionFactor = i % 4 == 2 ? 0.5f : 1f;
            var iterationCount = i % 4 == 1 ? curBaseSize * curBaseSize : 2 * curBaseSize * curBaseSize;
            for (var j = 0; j < iterationCount; j++)
            {
                var horizontalPosition = random.Vector2(0f, curBaseSize);
                var distanceFromCenter = horizontalPosition.Length();
                var horizontalSize = random.Int(3, 4);
                var verticalHeight =
                    1 +
                    (int)MathUtils.Lerp(
                        MathUtils.Max(curBaseSize / 3, 2.5f) * shapeCorrectionFactor,
                        0f,
                        distanceFromCenter / curBaseSize
                    ) +
                    random.Int(0, 1);
                waterPocketBrush.AddBox(
                    (int)MathUtils.Floor(horizontalPosition.X),
                    0,
                    (int)MathUtils.Floor(horizontalPosition.Y),
                    horizontalSize,
                    verticalHeight,
                    horizontalSize,
                    0
                );
                waterPocketBrush.AddBox(
                    (int)MathUtils.Floor(horizontalPosition.X),
                    -verticalOffset,
                    (int)MathUtils.Floor(horizontalPosition.Y),
                    horizontalSize,
                    verticalOffset,
                    horizontalSize,
                    18
                );
            }

            waterPocketBrush.Compile();
            _waterPocketBrushes.Add(waterPocketBrush);
        }

        var magmaBaseSize = new[] { 8, 12, 14, 16 };
        for (var i = 0; i < 4 * magmaBaseSize.Length; i++)
        {
            var magmaPocketBrush = new TerrainBrush();
            var curBaseSize = magmaBaseSize[i / 4];
            var verticalOffset = curBaseSize + 2;
            var shapeCorrectionFactor = i % 4 == 2 ? 0.5f : 1f;
            var iterationCount = i % 4 == 1 ? curBaseSize * curBaseSize : 2 * curBaseSize * curBaseSize;
            for (var j = 0; j < iterationCount; j++)
            {
                var horizontalPosition = random.Vector2(0f, curBaseSize);
                var distanceFromCenter = horizontalPosition.Length();
                var horizontalSize = random.Int(3, 4);
                var verticalHeight =
                    1 +
                    (int)MathUtils.Lerp(
                        MathUtils.Max(curBaseSize / 3, 2.5f) * shapeCorrectionFactor,
                        0f,
                        distanceFromCenter / curBaseSize
                    ) +
                    random.Int(0, 1);
                var num81 = 1 +
                            (int)MathUtils.Lerp(verticalOffset, 0f, distanceFromCenter / curBaseSize) +
                            random.Int(0, 1);
                magmaPocketBrush.AddBox(
                    (int)MathUtils.Floor(horizontalPosition.X),
                    0,
                    (int)MathUtils.Floor(horizontalPosition.Y),
                    horizontalSize,
                    verticalHeight,
                    horizontalSize,
                    0
                );
                magmaPocketBrush.AddBox(
                    (int)MathUtils.Floor(horizontalPosition.X),
                    -num81,
                    (int)MathUtils.Floor(horizontalPosition.Y),
                    horizontalSize,
                    num81,
                    horizontalSize,
                    92
                );
            }

            magmaPocketBrush.Compile();
            _magmaPocketBrushes.Add(magmaPocketBrush);
        }

        for (var i = 0; i < 7; i++)
        {
            _caveBrushesByType.Add([]);
            for (var j = 0; j < 3; j++)
            {
                var caveBrush = new TerrainBrush();
                var tunnelCount = 6 + 4 * i;
                var maxTunnelCrossSectionLimit = 3 + i / 3;
                var maxLengthLimit = 9 + i;
                for (var k = 0; k < tunnelCount; k++)
                {
                    var tunnelCrossSectionSize = 2;
                    var tunnelLength = random.Int(8, maxLengthLimit) - 2 * tunnelCrossSectionSize;
                    var growDirection = 0.5f *
                                        new Vector3(
                                            random.Float(-1f, 1f),
                                            random.Float(0f, 1f),
                                            random.Float(-1f, 1f)
                                        );
                    var curGrowDirection = Vector3.Zero;
                    for (var l = 0; l < tunnelLength; l++)
                    {
                        caveBrush.AddBox(
                            (int)MathUtils.Floor(curGrowDirection.X) - tunnelCrossSectionSize / 2,
                            (int)MathUtils.Floor(curGrowDirection.Y) - tunnelCrossSectionSize / 2,
                            (int)MathUtils.Floor(curGrowDirection.Z) - tunnelCrossSectionSize / 2,
                            tunnelCrossSectionSize,
                            tunnelCrossSectionSize,
                            tunnelCrossSectionSize,
                            0
                        );
                        curGrowDirection += growDirection;
                    }
                }

                caveBrush.Compile();
                _caveBrushesByType[i].Add(caveBrush);
            }
        }

        void CreateVeinBrushes(
            List<TerrainBrush> brushes,
            int contents,
            (int Min, int Max) veins,
            (int Min, int Max) length,
            float verticalRange,
            int? firstOriginContents = null
        )
        {
            for (var variant = 0; variant < 16; variant++)
            {
                var brush = new TerrainBrush();
                var veinCount = random.Int(veins.Min, veins.Max);
                for (var vein = 0; vein < veinCount; vein++)
                {
                    var direction = 0.5f *
                                    Vector3.Normalize(
                                        new Vector3(
                                            random.Float(-1f, 1f),
                                            random.Float(-verticalRange, verticalRange),
                                            random.Float(-1f, 1f)
                                        )
                                    );
                    var steps = random.Int(length.Min, length.Max);
                    var position = Vector3.Zero;
                    for (var step = 0; step < steps; step++)
                    {
                        brush.AddBox(
                            (int)MathUtils.Floor(position.X),
                            (int)MathUtils.Floor(position.Y),
                            (int)MathUtils.Floor(position.Z),
                            1,
                            1,
                            1,
                            contents
                        );
                        position += direction;
                    }
                }

                if (variant == 0 && firstOriginContents.HasValue)
                {
                    brush.AddCell(0, 0, 0, firstOriginContents.Value);
                }

                brush.Compile();
                brushes.Add(brush);
            }
        }
    }
}
