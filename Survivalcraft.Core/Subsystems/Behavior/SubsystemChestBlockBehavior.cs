using EntitySystem.TemplatesDatabase;

namespace Game.Subsystems.Behavior;

public class SubsystemChestBlockBehavior : SubsystemBlockBehavior
{
    private SubsystemSpatialSoundRequests _soundRequests = null!;

    private SubsystemBlockEntities _subsystemBlockEntities = null!;

    public override ResourceId[] HandledBlocks => [ResourceId.Parse("game:chest")];

    public override void Load(ValuesDictionary valuesDictionary)
    {
        base.Load(valuesDictionary);
        _subsystemBlockEntities = Project.FindSubsystem<SubsystemBlockEntities>(true)!;
        _soundRequests = Project.FindSubsystem<SubsystemSpatialSoundRequests>(true)!;
    }

    public override void OnBlockAdded(int value, int oldValue, int x, int y, int z, ComponentMiner miner)
    {
        _subsystemBlockEntities.CreateBlockEntity("Chest", new Point3(x, y, z), miner);
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

        var blockEntity = _subsystemBlockEntities.GetBlockEntity(
            raycastResult.CellFace.X,
            raycastResult.CellFace.Y,
            raycastResult.CellFace.Z
        );
        if (blockEntity == null)
        {
            return false;
        }

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
            _soundRequests.RequestSound("Audio/PickableCollected", 1f, 0f, worldItem.Position, 3f, true);
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
