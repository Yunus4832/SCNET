using Engine.Core.Mathematics;

using Game.Blocks;
using Game.Crafting;
using Game.ElectricElements;
using Game.Protocol;
using Game.Protocol.Localization;
using Game.Protocol.Terrain;
using Game.Subsystems;
using Game.Terrains;

namespace BeaconMod;

public sealed class BeaconBlock : Block, IElectricElementBlock
{
    public static ResourceId Id { get; } = ResourceId.Parse("scnet.beacon:beacon");

    private readonly BoundingBox[] _collision = [new(new Vector3(0.1f, 0, 0.1f), new Vector3(0.9f, 0.5f, 0.9f))];

    // Bits 0–2 select a color; bits 3–6 contain the circuit's light intensity.
    public static int GetIntensity(int data) => (data >> 3) & 15;
    public static int SetIntensity(int data, int intensity) => (data & 7) | (Math.Clamp(intensity, 0, 15) << 3);
    public static int CycleColor(int data) => (data & ~7) | ((data + 1) & 7);
    public static bool IsEnabled(int data) => GetIntensity(data) > 0;

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

    public override bool IsEditable(int value) => false;
    public override int GetEmittedLightAmount(int value) => GetIntensity(Terrain.ExtractData(value));
    public override BoundingBox[] GetCustomCollisionBoxes(SubsystemTerrain terrain, int value) => _collision;
    public override IEnumerable<int> GetCreativeValues() => [Terrain.MakeBlockValue(BlockIndex)];

    public override BlockPlacementData GetPlacementValue(
        SubsystemTerrain terrain,
        Vector3 eyeForward,
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
                Description = new LocalizedText("Beacon", "Recipe", string.Empty)
            }
        ];
    }
}
