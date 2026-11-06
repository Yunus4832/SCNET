using EntitySystem.TemplatesDatabase;

namespace Game.Subsystems.Behavior;

public class SubsystemCraftingTableBlockBehavior : SubsystemBlockBehavior
{
    private SubsystemBlockEntities _subsystemBlockEntities = null!;

    public override ResourceId[] HandledBlocks => [ResourceId.Parse("game:crafting_table")];

    public override void OnBlockAdded(int value, int oldValue, int x, int y, int z, ComponentMiner miner)
    {
        _subsystemBlockEntities.CreateBlockEntity("CraftingTable", new Point3(x, y, z), miner);
    }

    public override void OnBlockRemoved(int value, int newValue, int x, int y, int z)
    {
        _subsystemBlockEntities.DestroyBlockEntity(new Point3(x, y, z));
    }

    public override bool OnInteract(TerrainRaycastResult raycastResult, ComponentMiner componentMiner)
    {
        if (!WorldRuntimeContext.For(Project).IsAuthority &&
            componentMiner.ComponentCharacter?.IsLocallyControlled == true)
        {
            IProtocolMessage package =
                new OpenBlockInventoryPackage
                {
                    Point3 = new Point3(raycastResult.CellFace.X, raycastResult.CellFace.Y, raycastResult.CellFace.Z)
                };
            NetworkSender.SendToServer(WorldRuntimeContext.For(Project), package);
            return true;
        }

        var blockEntity = SubsystemTerrain.Project.FindSubsystem<SubsystemBlockEntities>(true)!
            .GetBlockEntity(raycastResult.CellFace.X, raycastResult.CellFace.Y, raycastResult.CellFace.Z);
        if (blockEntity == null)
        {
            return false;
        }

        return true;
    }

    public override void Load(ValuesDictionary valuesDictionary)
    {
        base.Load(valuesDictionary);
        _subsystemBlockEntities = Project.FindSubsystem<SubsystemBlockEntities>(true)!;
    }
}
