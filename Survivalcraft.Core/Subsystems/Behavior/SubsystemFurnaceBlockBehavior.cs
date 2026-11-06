using EntitySystem.TemplatesDatabase;

namespace Game.Subsystems.Behavior;

public class SubsystemFurnaceBlockBehavior : SubsystemBlockBehavior, IUpdateable
{
    public UpdateExecutionSide UpdateSide => UpdateExecutionSide.Authority;

    private SubsystemBlockEntities _subsystemBlockEntities = null!;

    public override ResourceId[] HandledBlocks =>
        [ResourceId.Parse("game:furnace"), ResourceId.Parse("game:lit_furnace")];

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public void Update(float dt)
    {
        if (!Time.PeriodicEvent(0.1, 0.0) || !WorldRuntimeContext.For(Project).IsAuthority)
        {
            return;
        }

        var byClient = new Dictionary<WorldConnection, List<ComponentFurnace>>(ReferenceEqualityComparer.Instance);
        var subsystemBlockEntities = Project.FindSubsystem<SubsystemBlockEntities>(true)!;
        foreach (var pair in subsystemBlockEntities.BlockEntities)
        {
            var furnace = pair.Value.Entity.FindComponent<ComponentFurnace>();
            if (furnace != null)
            {
                foreach (var client in SubsystemInventories.GetObservers(furnace).Where(client =>
                             client.IsConnected && client.State == ClientState.Playing
                         ))
                {
                    if (!byClient.TryGetValue(client, out var furnaces))
                    {
                        furnaces = [];
                        byClient.Add(client, furnaces);
                    }

                    furnaces.Add(furnace);
                }
            }
        }

        foreach (var (client, furnaces) in byClient)
        {
            NetworkSender.SendTo(client, EntityStatePackageBuilder.Capture(furnaces));
        }
    }

    public override void OnBlockAdded(int value, int oldValue, int x, int y, int z, ComponentMiner miner)
    {
        if (Terrain.ExtractContents(oldValue) != BuiltInBlockRegistry.GetIndex<FurnaceBlock>() &&
            Terrain.ExtractContents(oldValue) != BuiltInBlockRegistry.GetIndex<LitFurnaceBlock>())
        {
            _subsystemBlockEntities.CreateBlockEntity("Furnace", new Point3(x, y, z), miner);
        }
    }

    public override void OnBlockRemoved(int value, int newValue, int x, int y, int z)
    {
        if (!WorldRuntimeContext.For(Project).IsAuthority)
        {
            return;
        }

        if (Terrain.ExtractContents(newValue) == BuiltInBlockRegistry.GetIndex<FurnaceBlock>() ||
            Terrain.ExtractContents(newValue) == BuiltInBlockRegistry.GetIndex<LitFurnaceBlock>())
        {
            return;
        }

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
