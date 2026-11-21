namespace Game.Terrains.Generation;

public sealed class TerrainGeneratorDefinition
{
    private readonly Func<WorldSettings, int, ITerrainEnvironment> _environment;

    private readonly Func<SubsystemTerrain, Terrain, ITerrainContentsGenerator> _generator;

    public string Id { get; }

    public int Version { get; }

    public bool CreativeOnly { get; }

    public string NameKey { get; }

    public string DescriptionKey { get; }

    public IReadOnlyList<TerrainGeneratorParameter> Parameters { get; }

    private TerrainGeneratorDefinition(
        string id,
        int version,
        string textKey,
        bool creativeOnly,
        TerrainGeneratorParameter[] parameters,
        Func<WorldSettings, int, ITerrainEnvironment> environment,
        Func<SubsystemTerrain, Terrain, ITerrainContentsGenerator> generator
    )
    {
        Id = id;
        Version = version;
        CreativeOnly = creativeOnly;
        NameKey = textKey + "_Name";
        DescriptionKey = textKey + "_Description";
        Parameters = Array.AsReadOnly(parameters);
        _environment = environment;
        _generator = generator;
    }

    private static readonly double[] _sizes =
        [30, 40, 50, 60, 80, 100, 120, 150, 200, 250, 300, 400, 500, 600, 800, 1000, 1200, 1500, 2000, 2500];

    private static readonly double[] _offsets = [-16, -14, -12, -10, -8, -6, -4, -2, 0, 2, 4, 6, 8, 10, 12, 14, 16];

    private static readonly TerrainGeneratorParameter[] _common =
    [
        TerrainGeneratorParameter.String(
            "shape",
            "Shape",
            "continent",
            ("continent", "Shape_Continent"),
            ("island", "Shape_Island")
        ),
        TerrainGeneratorParameter.Number("islandWidth", "IslandWidth", 400, 30, 2500, 1, "Blocks_Format", _sizes),
        TerrainGeneratorParameter.Number("islandLength", "IslandLength", 400, 30, 2500, 1, "Blocks_Format", _sizes),
        TerrainGeneratorParameter.Number("seaLevelOffset", "SeaLevelOffset", 0, -16, 16, 1, "Blocks_Format", _offsets),
        TerrainGeneratorParameter.Number("temperatureOffset", "TemperatureOffset", 0, -16, 16, 1, null, _offsets),
        TerrainGeneratorParameter.Number("humidityOffset", "HumidityOffset", 0, -16, 16, 1, null, _offsets)
    ];

    public static TerrainGeneratorDefinition Standard { get; } = new(
        "game:standard",
        1,
        "Standard",
        false,
        [
            .. _common,
            TerrainGeneratorParameter.Number(
                "biomeSize",
                "BiomeSize",
                1,
                0.25,
                4,
                0.01,
                null,
                0.25,
                0.33,
                0.5,
                0.75,
                1,
                1.5,
                2,
                3,
                4
            )
        ],
        static (settings, seed) => new GeneratedTerrainEnvironment(settings, seed),
        static (world, terrain) => new TerrainContentsGenerator(world, terrain)
    );

    public static TerrainGeneratorDefinition Flat { get; } = new(
        "game:flat",
        1,
        "Flat",
        true,
        [
            .. _common,
            TerrainGeneratorParameter.Number(
                "terrainHeight",
                "TerrainHeight",
                64,
                2,
                252,
                1,
                "Blocks_Format",
                2,
                16,
                32,
                48,
                64,
                80,
                96,
                112,
                128,
                144,
                160,
                176,
                192,
                208,
                224,
                240,
                252
            ),
            TerrainGeneratorParameter.Number("shoreRoughness", "ShoreRoughness", 0.5, 0, 1, 0.1),
            TerrainGeneratorParameter.Block(
                "surfaceBlock",
                "SurfaceBlock",
                "game:grass",
                "game:grass",
                "game:dirt",
                "game:sand",
                "game:granite",
                "game:basalt",
                "game:limestone",
                "game:sandstone",
                "game:cobblestone",
                "game:stone_brick",
                "game:bricks",
                "game:planks",
                "game:iron",
                "game:copper",
                "game:glass",
                "game:ice",
                "game:marble",
                "game:diamond",
                "game:malachite",
                "game:bedrock"
            ),
            TerrainGeneratorParameter.Block("oceanBlock", "OceanBlock", "game:water", "game:water", "game:magma")
        ],
        static (settings, seed) => new TerrainEnvironmentFlat(settings, seed),
        static (world, terrain) => new TerrainContentsGeneratorFlat(world, terrain)
    );

    public static IReadOnlyList<TerrainGeneratorDefinition> All { get; } = Array.AsReadOnly(new[] { Standard, Flat });

    public static TerrainGeneratorDefinition Find(string id)
    {
        return All.FirstOrDefault(generator => generator.Id == id) ??
               throw new InvalidDataException($"Unknown terrain generator '{id}'.");
    }

    public ITerrainEnvironment CreateEnvironment(WorldSettings settings, int seed) => _environment(settings, seed);

    public ITerrainContentsGenerator CreateGenerator(SubsystemTerrain world, Terrain terrain)
    {
        return _generator(world, terrain);
    }
}
