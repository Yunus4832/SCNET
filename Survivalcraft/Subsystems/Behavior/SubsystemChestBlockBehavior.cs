using EntitySystem.TemplatesDatabase;

using Game.Network;
using Game.Network.Enums;
using Game.Network.Packages;

namespace Game.Subsystems;

public class SubsystemChestBlockBehavior : SubsystemBlockBehavior
{
    private SubsystemAudio _subsystemAudio = null!;

    private SubsystemBlockEntities _subsystemBlockEntities = null!;

    public override int[] HandledBlocks => [45];

    public override void Load(ValuesDictionary valuesDictionary)
    {
        base.Load(valuesDictionary);
        _subsystemBlockEntities = Project.FindSubsystem<SubsystemBlockEntities>(true)!;
        _subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true)!;
    }

    public override void OnBlockAdded(int value, int oldValue, int x, int y, int z, ComponentMiner miner)
    {
        _subsystemBlockEntities.CreateBlockEntity("Chest", new Point3(x, y, z), miner);
    }

    public override void OnBlockRemoved(int value, int newValue, int x, int y, int z)
    {
        var blockEntity = _subsystemBlockEntities.GetBlockEntity(x, y, z);
        if (blockEntity == null)
        {
            return;
        }

        var position = new Vector3(x, y, z) + new Vector3(0.5f);
        foreach (var item in blockEntity.Entity.FindComponents<IInventory>())
        {
            item?.DropAllItems(position);
        }

        Project.RemoveEntity(blockEntity.Entity, true);
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

        var blockEntity = _subsystemBlockEntities.GetBlockEntity(raycastResult.CellFace.X, raycastResult.CellFace.Y,
            raycastResult.CellFace.Z);
        if (blockEntity == null)
        {
            return false;
        }

        if (componentMiner.ComponentPlayer is { PlayerData.IsMainPlayer: false })
        {
            return true;
        }

        var componentChest = blockEntity.Entity.FindComponent<ComponentChest>(true)!;
        componentMiner.ComponentPlayer?.ComponentGui.ModalPanelWidget =
            new ChestWidget(componentMiner.Inventory, componentChest);
        AudioManager.PlaySound("Audio/UI/ButtonClick", 1f, 0f, 0f);

        return true;
    }

    public override void OnHitByProjectile(CellFace cellFace, WorldItem worldItem)
    {
        if (worldItem.ToRemove)
        {
            return;
        }

        var blockEntity = _subsystemBlockEntities.GetBlockEntity(cellFace.X, cellFace.Y, cellFace.Z);
        if (blockEntity == null)
        {
            return;
        }

        var inventory = blockEntity.Entity.FindComponent<ComponentChest>(true)!;
        var pickable = worldItem as Pickable;
        var num = pickable?.Count ?? 1;
        var num2 = ComponentInventoryBase.AcquireItems(inventory, worldItem.Value, num);
        if (num2 < num)
        {
            _subsystemAudio.PlaySound("Audio/PickableCollected", 1f, 0f, worldItem.Position, 3f, true);
        }

        if (num2 <= 0)
        {
            worldItem.ToRemove = true;
        }
        else
        {
            pickable?.Count = num2;
        }
    }
}
