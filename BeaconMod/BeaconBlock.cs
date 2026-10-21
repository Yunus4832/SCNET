using Engine.Core;
using Engine.Graphics;

using Game;
using Game.Blocks;
using Game.Components;
using Game.ElectricElements;
using Game.Managers;
using Game.Protocol;
using Game.Protocol.Terrain;
using Game.Subsystems;
using Game.Terrains;

namespace BeaconMod;

public sealed class BeaconBlock : Block, IElectricElementBlock
{
    public static ResourceId Id { get; } = ResourceId.Parse("scnet.beacon:beacon");

    public static readonly Color[] Colors =
    [
        new(180, 245, 255), new(255, 100, 95), new(255, 180, 65), new(255, 245, 105),
        new(110, 255, 145), new(95, 175, 255), new(190, 130, 255), new(255, 140, 210)
    ];

    private readonly BlockMesh _baseMesh = new();

    private readonly BlockMesh _glassMesh = new();

    private readonly BlockMesh[] _cores = new BlockMesh[16];

    private readonly BlockMesh[] _standalone = new BlockMesh[16];

    private readonly BoundingBox[] _collision = [new(new Vector3(0.1f, 0, 0.1f), new Vector3(0.9f, 0.5f, 0.9f))];

    // Bits 0–2 select a color; bits 3–6 contain the circuit's light intensity.
    public static int GetIntensity(int data) => (data >> 3) & 15;
    public static int SetIntensity(int data, int intensity) => (data & 7) | (Math.Clamp(intensity, 0, 15) << 3);
    public static int CycleColor(int data) => (data & ~7) | ((data + 1) & 7);
    public static bool IsEnabled(int data) => GetIntensity(data) > 0;
    public static Color GetColor(int data) => Colors[data & 7];
    public static Color GetEmitterColor(int data) => Color.Lerp(GetColor(data), Color.White, 0.8f);

    public ElectricElement CreateElectricElement(SubsystemElectricity electricity, int value, int x, int y, int z) =>
        new BeaconElectricElement(electricity, new CellFace(x, y, z, 4));

    public ElectricConnectorType? GetConnectorType(
        SubsystemTerrain terrain,
        int value,
        int face,
        int connectorFace,
        int x,
        int y,
        int z
    ) => face == 4 &&
         SubsystemElectricity.GetConnectorDirection(4, 0, connectorFace).HasValue
        ? ElectricConnectorType.Input
        : null;

    public int GetConnectionMask(int value) => int.MaxValue;

    public override void Initialize()
    {
        base.Initialize();
        using var stream = ModEntry.OpenResource("Models.Beacon.dae");
        using var model = Model.Load(stream, true);
        var clay = (ClayBlock)Catalog.GetBlock(ResourceId.Parse("game:clay"));
        var baseSlot = clay.GetFaceTextureSlot(0, 0);
        var emissiveSlot = clay.ColoredTextureSlot;
        // Use the existing glass tile, including its transparent interior and edge highlights.
        var glassSlot = Catalog.GetBlock(ResourceId.Parse("game:glass")).GetFaceTextureSlot(0, 0);
        AppendPart(_baseMesh, model, "base", new Color(85, 90, 100), false, baseSlot, false);
        AppendPart(_glassMesh, model, "glass", Color.White, false, glassSlot, true);
        for (var data = 0; data < 16; data++)
        {
            var core = new BlockMesh();
            AppendPart(
                core,
                model,
                "core",
                (data & 8) != 0 ? GetEmitterColor(data) : new Color(65, 75, 80),
                (data & 8) != 0,
                emissiveSlot,
                false
            );
            _cores[data] = core;
            var standalone = new BlockMesh();
            standalone.AppendBlockMesh(_baseMesh);
            standalone.AppendBlockMesh(core);
            standalone.AppendBlockMesh(_glassMesh);
            standalone.TransformPositions(Matrix.CreateTranslation(-0.5f, -0.25f, -0.5f));
            _standalone[data] = standalone;
        }
    }

    private static void AppendPart(
        BlockMesh target,
        Model model,
        string name,
        Color color,
        bool emissive,
        int slot,
        bool glass
    )
    {
        var mesh = model.FindMesh(name)!;
        foreach (var part in mesh.MeshParts)
        {
            target.AppendModelMeshPart(
                part,
                BlockMesh.GetBoneAbsoluteTransform(mesh.ParentBone!),
                emissive,
                false,
                glass,
                false,
                color
            );
        }

        var bounds = target.CalculateBoundingBox();
        for (var i = 0; i < target.Vertices.Count; i++)
        {
            var vertex = target.Vertices.Array[i];
            var p = (vertex.Position - bounds.Min) / (bounds.Max - bounds.Min);
            var uv = vertex.Face switch
            {
                0 or 2 => new Vector2(p.X, 1 - p.Y),
                1 or 3 => new Vector2(p.Z, 1 - p.Y),
                _ => new Vector2(p.X, p.Z)
            };
            // A flat white texel for solid parts; full glass tile for the cover.
            if (!glass)
            {
                uv = new Vector2(0.5f);
            }

            vertex.TextureCoordinates = (new Vector2(slot % 16, slot / 16) +
                                         new Vector2(0.01f) +
                                         uv * 0.98f) /
                                        16f;
            vertex.Position += new Vector3(0.5f, 0, 0.5f);
            target.Vertices.Array[i] = vertex;
        }
    }

    public override bool IsEditable(int value) => false;
    public override int GetEmittedLightAmount(int value) => GetIntensity(Terrain.ExtractData(value));
    public override BoundingBox[] GetCustomCollisionBoxes(SubsystemTerrain terrain, int value) => _collision;
    public override IEnumerable<int> GetCreativeValues() => [Terrain.MakeBlockValue(BlockIndex)];

    public override void GenerateTerrainVertices(
        BlockGeometryGenerator generator,
        TerrainGeometry geometry,
        int value,
        int x,
        int y,
        int z
    )
    {
        var data = Terrain.ExtractData(value);
        var meshIndex = (data & 7) | (IsEnabled(data) ? 8 : 0);
        generator.GenerateMeshVertices(this, x, y, z, _baseMesh, Color.White, null, geometry.SubsetOpaque);
        var brightness = IsEnabled(data) ? GetIntensity(data) / 15f : 1f;
        generator.GenerateMeshVertices(
            this,
            x,
            y,
            z,
            _cores[meshIndex],
            Color.MultiplyColorOnly(Color.White, brightness),
            null,
            geometry.SubsetOpaque
        );
        generator.GenerateWireVertices(value, x, y, z, 4, 0.01f, Vector2.Zero, geometry.SubsetOpaque);
        generator.GenerateMeshVertices(this, x, y, z, _glassMesh, Color.White, null, geometry.SubsetAlphaTest);
    }

    public override void DrawBlock(
        PrimitivesRenderer3D renderer,
        int value,
        Color color,
        float size,
        ref Matrix matrix,
        DrawBlockEnvironmentData environmentData
    ) => BlocksManager.DrawMeshBlock(
        renderer,
        _standalone[0],
        color,
        size,
        ref matrix,
        environmentData
    );

    public override BlockPlacementData GetPlacementValue(
        SubsystemTerrain terrain,
        ComponentMiner miner,
        int value,
        TerrainRaycastResult hit
    ) => new() { Value = Terrain.MakeBlockValue(BlockIndex), CellFace = hit.CellFace };

    public override void GetDropValues(
        SubsystemTerrain terrain,
        int oldValue,
        int newValue,
        int toolLevel,
        List<BlockDropValue> drops,
        out bool showDebris
    )
    {
        showDebris = true;
        drops.Add(new BlockDropValue { Value = Terrain.MakeBlockValue(BlockIndex), Count = 1 });
    }

    public override IEnumerable<CraftingRecipe> GetProceduralCraftingRecipes()
    {
        string Ingredient(string id) => Catalog.GetBlock(ResourceId.Parse("game:" + id)).CraftingId + ":0";
        var glass = Ingredient("glass");
        var iron = Ingredient("iron_ingot");
        return
        [
            new CraftingRecipe
            {
                ResultValue = Terrain.MakeBlockValue(BlockIndex), ResultCount = 1,
                Ingredients =
                [
                    glass, glass, glass, glass, Ingredient("lightbulb"), glass,
                    iron, iron, iron
                ],
                Description = LanguageManager.Get("Beacon", "Recipe")
            }
        ];
    }
}
