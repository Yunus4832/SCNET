namespace Game.Subsystems;

public class SubsystemWaterBlockBehavior() : SubsystemFluidBlockBehavior(
    BlocksManager.FluidBlocks[18]!,
    true
), IUpdateable
{
    private readonly Random _random = new();

    private float _soundVolume;

    public override int[] HandledBlocks => [18];

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public void Update(float dt)
    {
        if (SubsystemTime.PeriodicGameTimeEvent(0.25, 0.0))
        {
            SpreadFluid();
        }

        if (SubsystemTime.PeriodicGameTimeEvent(1.0, 0.25))
        {
            var num = float.MaxValue;
            foreach (var listenerPosition in SubsystemAudio.ListenerPositions)
            {
                var num2 = CalculateDistanceToFluid(listenerPosition, 8, true);
                if (num2 < num)
                {
                    num = num2.Value;
                }
            }

            _soundVolume = 0.5f * SubsystemAudio.CalculateVolume(num, 2f, 3.5f);
        }

        SubsystemAmbientSounds.WaterSoundVolume = MathUtils.Max(SubsystemAmbientSounds.WaterSoundVolume, _soundVolume);
    }

    public override bool OnFluidInteract(int interactValue, int x, int y, int z, int fluidValue)
    {
        if (BlocksManager.Blocks[Terrain.ExtractContents(interactValue)] is not MagmaBlock)
        {
            return base.OnFluidInteract(interactValue, x, y, z, fluidValue);
        }

        SubsystemAudio.PlayRandomSound("Audio/Sizzles", 1f, _random.Float(-0.1f, 0.1f), new Vector3(x, y, z), 5f,
            true);
        SubsystemTerrain.DestroyCell(0, x, y, z, 0, false, false);
        Set(x, y, z, 3);
        return true;
    }

    public override void OnItemHarvested(
        int x, int y, int z,
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
