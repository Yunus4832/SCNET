using EntitySystem.TemplatesDatabase;

namespace Game.Subsystems;

public class SubsystemBucketBlockBehavior : SubsystemBlockBehavior
{
    private readonly Random _random = new();

    private SubsystemAudio _subsystemAudio = null!;

    private SubsystemParticles _subsystemParticles = null!;

    public override int[] HandledBlocks =>
    [
        90,
        91,
        93,
        110,
        245,
        251,
        252,
        129,
        128
    ];

    public override bool OnUse(Ray3 ray, ComponentMiner componentMiner)
    {
        var inventory = componentMiner.Inventory;
        var activeBlockValue = componentMiner.ActiveBlockValue;
        var num = Terrain.ExtractContents(activeBlockValue);
        switch (num)
        {
            case EmptyBucketBlock.Index:
                var obj = componentMiner.Raycast(ray, RaycastMode.Gathering);
                if (obj is TerrainRaycastResult result)
                {
                    var cellFace = result.CellFace;
                    var cellValue = SubsystemTerrain.Terrain.GetCellValue(cellFace.X, cellFace.Y, cellFace.Z);
                    var num2 = Terrain.ExtractContents(cellValue);
                    var data = Terrain.ExtractData(cellValue);
                    var block = BlocksManager.Blocks[num2];
                    if (block is WaterBlock && FluidBlock.GetLevel(data) == 0)
                    {
                        var value = Terrain.ReplaceContents(activeBlockValue, 91);
                        inventory.RemoveNetSlotItems(inventory.ActiveSlotIndex,
                            inventory.GetSlotCount(inventory.ActiveSlotIndex));
                        if (inventory.GetSlotCount(inventory.ActiveSlotIndex) == 0)
                        {
                            inventory.AddNetSlotItems(inventory.ActiveSlotIndex, value, 1);
                        }

                        SubsystemTerrain.DestroyCell(0, cellFace.X, cellFace.Y, cellFace.Z, 0, false, false,
                            componentMiner);
                        return true;
                    }

                    if (block is MagmaBlock && FluidBlock.GetLevel(data) == 0)
                    {
                        var value2 = Terrain.ReplaceContents(activeBlockValue, 93);
                        inventory.RemoveNetSlotItems(inventory.ActiveSlotIndex,
                            inventory.GetSlotCount(inventory.ActiveSlotIndex));
                        if (inventory.GetSlotCount(inventory.ActiveSlotIndex) == 0)
                        {
                            inventory.AddNetSlotItems(inventory.ActiveSlotIndex, value2, 1);
                        }

                        SubsystemTerrain.DestroyCell(0, cellFace.X, cellFace.Y, cellFace.Z, 0, false, false,
                            componentMiner);
                        return true;
                    }
                }
                else if (obj is BodyRaycastResult raycastResult)
                {
                    var componentUdder = raycastResult.ComponentBody.Entity.FindComponent<ComponentUdder>();
                    if (componentUdder == null || !componentUdder.Milk(componentMiner))
                    {
                        return true;
                    }

                    var value3 = Terrain.ReplaceContents(activeBlockValue, 110);
                    inventory.RemoveNetSlotItems(inventory.ActiveSlotIndex,
                        inventory.GetSlotCount(inventory.ActiveSlotIndex));
                    if (inventory.GetSlotCount(inventory.ActiveSlotIndex) == 0)
                    {
                        inventory.AddNetSlotItems(inventory.ActiveSlotIndex, value3, 1);
                    }

                    _subsystemAudio.PlaySound("Audio/Milked", 1f, 0f, ray.Position, 2f, true);

                    return true;
                }

                break;
            case WaterBucketBlock.Index:
                var terrainRaycastResult = componentMiner.Raycast<TerrainRaycastResult>(ray, RaycastMode.Interaction);
                if (terrainRaycastResult.HasValue &&
                    componentMiner.Place(terrainRaycastResult.Value, Terrain.MakeBlockValue(18)))
                {
                    inventory.RemoveNetSlotItems(inventory.ActiveSlotIndex, 1);
                    if (inventory.GetSlotCount(inventory.ActiveSlotIndex) != 0)
                    {
                        return true;
                    }

                    var value4 = Terrain.ReplaceContents(activeBlockValue, 90);
                    inventory.AddNetSlotItems(inventory.ActiveSlotIndex, value4, 1);

                    return true;
                }

                break;
            case MagmaBucketBlock.Index:
                var terrainRaycastResult2 = componentMiner.Raycast<TerrainRaycastResult>(ray, RaycastMode.Interaction);
                if (terrainRaycastResult2.HasValue)
                {
                    if (!componentMiner.Place(terrainRaycastResult2.Value, Terrain.MakeBlockValue(92)))
                    {
                        return true;
                    }

                    inventory.RemoveNetSlotItems(inventory.ActiveSlotIndex, 1);
                    if (inventory.GetSlotCount(inventory.ActiveSlotIndex) != 0)
                    {
                        return true;
                    }

                    var value5 = Terrain.ReplaceContents(activeBlockValue, 90);
                    inventory.AddNetSlotItems(inventory.ActiveSlotIndex, value5, 1);

                    return true;
                }

                break;
            case MilkBucketBlock.Index:
            case RottenMilkBucketBlock.Index:
                return true;
            case PumpkinSoupBucketBlock.Index:
            case RottenPumpkinSoupBucketBlock.Index:
                return true;
            case PaintStripperBucketBlock.Index:
            case PaintBucketBlock.Index:
                {
                    var terrainRaycastResult3 = componentMiner.Raycast<TerrainRaycastResult>(ray, RaycastMode.Digging);
                    if (terrainRaycastResult3.HasValue)
                    {
                        var cellFace2 = terrainRaycastResult3.Value.CellFace;
                        var cellValue2 = SubsystemTerrain.Terrain.GetCellValue(cellFace2.X, cellFace2.Y, cellFace2.Z);
                        var num3 = Terrain.ExtractContents(cellValue2);
                        var block2 = BlocksManager.Blocks[num3];
                        if (block2 is not IPaintableBlock block)
                        {
                            return true;
                        }

                        var normal = CellFace.FaceToVector3(terrainRaycastResult3.Value.CellFace.Face);
                        var position = terrainRaycastResult3.Value.HitPoint();
                        var num4 = num == 128
                            ? null
                            : new int?(PaintBucketBlock.GetColor(Terrain.ExtractData(activeBlockValue)));
                        var color = num4.HasValue
                            ? SubsystemPalette.GetColor(SubsystemTerrain, num4)
                            : new Color(128, 128, 128, 128);
                        var value6 = block.Paint(SubsystemTerrain, cellValue2, num4);
                        SubsystemTerrain.ChangeCell(cellFace2.X, cellFace2.Y, cellFace2.Z, value6);
                        componentMiner.DamageActiveTool(1);
                        _subsystemAudio.PlayRandomSound("Audio/Paint", 0.4f, _random.Float(-0.1f, 0.1f),
                            componentMiner.ComponentCreature.ComponentBody.Position, 2f, true);
                        _subsystemParticles.AddParticleSystem(new PaintParticleSystem(SubsystemTerrain, position,
                            normal, color));

                        return true;
                    }

                    break;
                }
        }

        return false;
    }

    public override void Load(ValuesDictionary valuesDictionary)
    {
        base.Load(valuesDictionary);
        _subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true)!;
        _subsystemParticles = Project.FindSubsystem<SubsystemParticles>(true)!;
    }
}
