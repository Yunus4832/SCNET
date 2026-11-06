using EntitySystem.TemplatesDatabase;

namespace Game.Subsystems.Behavior;

public class SubsystemMagmaBlockBehavior() : SubsystemFluidBlockBehavior(
    ResourceId.Parse("game:magma"),
    false
), IUpdateable
{
    public UpdateExecutionSide UpdateSide => UpdateExecutionSide.Authority;

    private readonly Random _random = new();

    private SubsystemFireBlockBehavior _subsystemFireBlockBehavior = null!;

    public override ResourceId[] HandledBlocks => [ResourceId.Parse("game:magma")];

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public void Update(float dt)
    {
        if (WorldRuntimeContext.For(Project).IsAuthority && SubsystemTime.PeriodicGameTimeEvent(2.0, 0.0))
        {
            SpreadFluid();
        }
    }

    public override void OnBlockAdded(int value, int oldValue, int x, int y, int z)
    {
        base.OnBlockAdded(value, oldValue, x, y, z);
        for (var i = -1; i <= 1; i++)
        {
            for (var j = -1; j <= 1; j++)
            {
                for (var k = -1; k <= 1; k++)
                {
                    ApplyMagmaNeighborhoodEffect(x + i, y + j, z + k);
                }
            }
        }
    }

    public override void OnNeighborBlockChanged(int x, int y, int z, int neighborX, int neighborY, int neighborZ)
    {
        base.OnNeighborBlockChanged(x, y, z, neighborX, neighborY, neighborZ);
        ApplyMagmaNeighborhoodEffect(neighborX, neighborY, neighborZ);
    }

    public override bool OnFluidInteract(int interactValue, int x, int y, int z, int fluidValue)
    {
        if (!WorldRuntimeContext.For(Project).IsAuthority) { return false; }

        if (WorldContentBinding.For(Project).Catalog
                .Blocks[Terrain.ExtractContents(interactValue)] is WaterBlock)
        {
            _subsystemFireBlockBehavior.PublishBurnEffect(new Vector3(x, y, z), BlockBurnEffectKind.FluidSizzle);
            SubsystemTerrain.DestroyCell(0, x, y, z, 0, false, false);
            Set(x, y, z, 67);
            return true;
        }

        return base.OnFluidInteract(interactValue, x, y, z, fluidValue);
    }

    public override void Load(ValuesDictionary valuesDictionary)
    {
        base.Load(valuesDictionary);
        _subsystemFireBlockBehavior = Project.FindSubsystem<SubsystemFireBlockBehavior>(true)!;
    }

    private void ApplyMagmaNeighborhoodEffect(int x, int y, int z)
    {
        if (!WorldRuntimeContext.For(Project).IsAuthority) { return; }

        _subsystemFireBlockBehavior.SetCellOnFire(x, y, z, 1f);
        switch (SubsystemTerrain.Terrain.GetCellContents(x, y, z))
        {
            case 61:
            case 62:
                SubsystemTerrain.DestroyCell(0, x, y, z, 0, false, false);
                _subsystemFireBlockBehavior.PublishBurnEffect(
                    new Vector3(x + 0.5f, y + 1, z + 0.5f),
                    BlockBurnEffectKind.Debris
                );
                break;
            case 8:
                SubsystemTerrain.ChangeCell(x, y, z, 2);
                _subsystemFireBlockBehavior.PublishBurnEffect(
                    new Vector3(x + 0.5f, y + 1, z + 0.5f),
                    BlockBurnEffectKind.Debris
                );
                break;
        }
    }
}
