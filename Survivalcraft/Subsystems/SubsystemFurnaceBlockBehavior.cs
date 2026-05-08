using EntitySystem.TemplatesDatabase;
using Game.NetWork;
using Game.NetWork.Packages;

namespace Game.Subsystems;

public class SubsystemFurnaceBlockBehavior : SubsystemBlockBehavior, IUpdateable
{
    private readonly Dictionary<Point3, FireParticleSystem> _particleSystemsByCell = new();

    private SubsystemBlockEntities _subsystemBlockEntities = null!;

    private SubsystemParticles _subsystemParticles = null!;

    public override int[] HandledBlocks => [64, 65];

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public void Update(float dt)
    {
        if (!Time.PeriodicEvent(0.1, 0.0) || CommonLib.WorkType != WorkType.Server)
        {
            return;
        }

        var componentFurnaces = new List<ComponentFurnace>();
        var subsystemBlockEntities = Project.FindSubsystem<SubsystemBlockEntities>(true)!;
        foreach (var pair in subsystemBlockEntities.BlockEntities)
        {
            var furnace = pair.Value.Entity.FindComponent<ComponentFurnace>();
            if (furnace != null)
            {
                componentFurnaces.Add(furnace);
            }
        }

        if (componentFurnaces.Count > 0)
        {
            CommonLib.Net.QueuePackage(new ComponentFurnacePackage(componentFurnaces));
        }
    }

    public override void OnBlockAdded(int value, int oldValue, int x, int y, int z, ComponentMiner miner)
    {
        if (Terrain.ExtractContents(oldValue) != FurnaceBlock.Index &&
            Terrain.ExtractContents(oldValue) != LitFurnaceBlock.Index)
        {
            _subsystemBlockEntities.CreateBlockEntity("Furnace", new Point3(x, y, z), miner);
        }

        var content = Terrain.ExtractContents(value);
        if (content == LitFurnaceBlock.Index)
        {
            AddFire(value, x, y, z);
        }
    }

    public override void OnBlockAdded(int value, int oldValue, int x, int y, int z)
    {
        if (Terrain.ExtractContents(value) == LitFurnaceBlock.Index)
        {
            AddFire(value, x, y, z);
        }
    }

    public override void OnBlockRemoved(int value, int newValue, int x, int y, int z)
    {
        if (Terrain.ExtractContents(value) == LitFurnaceBlock.Index)
        {
            RemoveFire(x, y, z);
        }

        if (CommonLib.WorkType == WorkType.Client)
        {
            return;
        }

        if (Terrain.ExtractContents(newValue) == FurnaceBlock.Index ||
            Terrain.ExtractContents(newValue) == LitFurnaceBlock.Index)
        {
            return;
        }

        var blockEntity = SubsystemTerrain.Project.FindSubsystem<SubsystemBlockEntities>(true)!
            .GetBlockEntity(x, y, z);
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

    public override void OnBlockGenerated(int value, int x, int y, int z, bool isLoaded)
    {
        if (Terrain.ExtractContents(value) == LitFurnaceBlock.Index)
        {
            AddFire(value, x, y, z);
        }
    }

    public override void OnChunkDiscarding(TerrainChunk chunk)
    {
        var list = new List<Point3>();
        foreach (var key in _particleSystemsByCell.Keys)
        {
            if (key.X >= chunk.Origin.X && key.X < chunk.Origin.X + 16 && key.Z >= chunk.Origin.Y &&
                key.Z < chunk.Origin.Y + 16)
            {
                list.Add(key);
            }
        }

        foreach (var item in list)
        {
            RemoveFire(item.X, item.Y, item.Z);
        }
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

        var componentFurnace = blockEntity.Entity.FindComponent<ComponentFurnace>(true)!;
        componentMiner.ComponentPlayer?.ComponentGui.ModalPanelWidget =
            new FurnaceWidget(componentMiner.Inventory, componentFurnace);
        AudioManager.PlaySound("Audio/UI/ButtonClick", 1f, 0f, 0f);

        return true;

    }

    public override void Load(ValuesDictionary valuesDictionary)
    {
        base.Load(valuesDictionary);
        _subsystemParticles = Project.FindSubsystem<SubsystemParticles>(true)!;
        _subsystemBlockEntities = Project.FindSubsystem<SubsystemBlockEntities>(true)!;
    }

    private void AddFire(int value, int x, int y, int z)
    {
        var v = new Vector3(0.5f, 0.2f, 0.5f);
        const float size = 0.15f;
        var fireParticleSystem = new FireParticleSystem(new Vector3(x, y, z) + v, size, 16f);
        _subsystemParticles.AddParticleSystem(fireParticleSystem);
        _particleSystemsByCell[new Point3(x, y, z)] = fireParticleSystem;
    }

    private void RemoveFire(int x, int y, int z)
    {
        var key = new Point3(x, y, z);
        var particleSystem = _particleSystemsByCell[key];
        _subsystemParticles.RemoveParticleSystem(particleSystem);
        _particleSystemsByCell.Remove(key);
    }
}
