using EntitySystem.TemplatesDatabase;

using Game.Network;
using Game.Network.Enums;
using Game.Network.Packages;

namespace Game.Subsystems;

public class SubsystemCraftingTableBlockBehavior : SubsystemBlockBehavior
{
    private SubsystemBlockEntities _subsystemBlockEntities = null!;

    public override int[] HandledBlocks => [27];

    public override void OnBlockAdded(int value, int oldValue, int x, int y, int z, ComponentMiner miner)
    {
        _subsystemBlockEntities.CreateBlockEntity("CraftingTable", new Point3(x, y, z), miner);
    }

    public override void OnBlockRemoved(int value, int newValue, int x, int y, int z)
    {
        var blockEntity = SubsystemTerrain.Project.FindSubsystem<SubsystemBlockEntities>(true)!.GetBlockEntity(x, y, z);
        if (blockEntity == null)
        {
            return;
        }

        var position = new Vector3(x, y, z) + new Vector3(0.5f);
        foreach (var item in blockEntity.Entity.FindComponents<IInventory>())
        {
            item?.DropAllItems(position);
        }

        SubsystemTerrain.Project.RemoveEntity(blockEntity.Entity, true);
    }

    public override bool OnInteract(TerrainRaycastResult raycastResult, ComponentMiner componentMiner)
    {
        if (CommonLib.WorkType == WorkType.Client && CommonLib.MainPlayer == componentMiner.ComponentPlayer)
        {
            IPackage package =
                new BlockEditPackage(
                    new Point3(raycastResult.CellFace.X, raycastResult.CellFace.Y, raycastResult.CellFace.Z),
                    BlockEditPackage.EventType.OpenInventoryByPoint);
            CommonLib.Net.QueuePackage(package);
            return true;
        }

        var blockEntity = SubsystemTerrain.Project.FindSubsystem<SubsystemBlockEntities>(true)!
            .GetBlockEntity(raycastResult.CellFace.X, raycastResult.CellFace.Y, raycastResult.CellFace.Z);
        if (blockEntity == null)
        {
            return false;
        }

        if (componentMiner.ComponentPlayer is { PlayerData.IsMainPlayer: false })
        {
            return true;
        }

        var componentCraftingTable = blockEntity.Entity.FindComponent<ComponentCraftingTable>(true)!;
        componentMiner.ComponentPlayer?.ComponentGui.ModalPanelWidget =
            new CraftingTableWidget(componentMiner.Inventory, componentCraftingTable);
        AudioManager.PlaySound("Audio/UI/ButtonClick", 1f, 0f, 0f);

        return true;
    }

    public override void Load(ValuesDictionary valuesDictionary)
    {
        base.Load(valuesDictionary);
        _subsystemBlockEntities = Project.FindSubsystem<SubsystemBlockEntities>(true)!;
    }
}
