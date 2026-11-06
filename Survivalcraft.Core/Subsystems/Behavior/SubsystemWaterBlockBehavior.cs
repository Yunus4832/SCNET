namespace Game.Subsystems.Behavior;

public class SubsystemWaterBlockBehavior() : SubsystemFluidBlockBehavior(
    ResourceId.Parse("game:water"),
    true
), IUpdateable
{
    public UpdateExecutionSide UpdateSide => UpdateExecutionSide.Authority;

    public override ResourceId[] HandledBlocks => [ResourceId.Parse("game:water")];

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public void Update(float dt)
    {
        if (WorldRuntimeContext.For(Project).IsAuthority && SubsystemTime.PeriodicGameTimeEvent(.25, 0.0))
        {
            SpreadFluid();
        }
    }

    public override bool OnFluidInteract(int interactValue, int x, int y, int z, int fluidValue)
    {
        if (!WorldRuntimeContext.For(Project).IsAuthority) { return false; }

        if (WorldContentBinding.For(Project).Catalog
                .Blocks[Terrain.ExtractContents(interactValue)] is not
            MagmaBlock)
        {
            return base.OnFluidInteract(interactValue, x, y, z, fluidValue);
        }

        Project.FindSubsystem<SubsystemFireBlockBehavior>(true)!
            .PublishBurnEffect(new Vector3(x, y, z), BlockBurnEffectKind.FluidSizzle);
        SubsystemTerrain.DestroyCell(0, x, y, z, 0, false, false);
        Set(x, y, z, 3);
        return true;
    }

    public override void OnItemHarvested(
        int x,
        int y,
        int z,
        int blockValue,
        ref BlockDropValue dropValue,
        ref int newBlockValue
    )
    {
        if (y > 80 && SubsystemWeather.IsPlaceFrozen(SubsystemTerrain.Terrain.GetSeasonalTemperature(x, z), y))
        {
            dropValue.Value = Terrain.MakeBlockValue(62);
        }
        else
        {
            base.OnItemHarvested(x, y, z, blockValue, ref dropValue, ref newBlockValue);
        }
    }
}
