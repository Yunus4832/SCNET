using EntitySystem.TemplatesDatabase;

namespace Game.Subsystems;

public class SubsystemMagmaBlockBehavior() : SubsystemFluidBlockBehavior(
    BlocksManager.FluidBlocks[92]!,
    false), IUpdateable
{
    private readonly Random _random = new();

    private float _soundVolume;

    private SubsystemFireBlockBehavior _subsystemFireBlockBehavior = null!;

    private SubsystemParticles _subsystemParticles = null!;

    public override int[] HandledBlocks => [92];

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public void Update(float dt)
    {
        if (SubsystemTime.PeriodicGameTimeEvent(2.0, 0.0))
        {
            SpreadFluid();
        }

        if (SubsystemTime.PeriodicGameTimeEvent(1.0, 0.75))
        {
            var num = float.MaxValue;
            foreach (var listenerPosition in SubsystemAudio.ListenerPositions)
            {
                var num2 = CalculateDistanceToFluid(listenerPosition, 8, false);
                if (num2.HasValue && num2.Value < num)
                {
                    num = num2.Value;
                }
            }

            _soundVolume = SubsystemAudio.CalculateVolume(num, 2f, 3.5f);
        }

        SubsystemAmbientSounds.MagmaSoundVolume = MathUtils.Max(SubsystemAmbientSounds.MagmaSoundVolume, _soundVolume);
    }

    public override void OnBlockAdded(int value, int oldValue, int x, int y, int z)
    {
        base.OnBlockAdded(value, oldValue, x, y, z);
        for (var i = -1; i <= 1; i++)
        for (var j = -1; j <= 1; j++)
        for (var k = -1; k <= 1; k++)
        {
            ApplyMagmaNeighborhoodEffect(x + i, y + j, z + k);
        }
    }

    public override void OnNeighborBlockChanged(int x, int y, int z, int neighborX, int neighborY, int neighborZ)
    {
        base.OnNeighborBlockChanged(x, y, z, neighborX, neighborY, neighborZ);
        ApplyMagmaNeighborhoodEffect(neighborX, neighborY, neighborZ);
    }

    public override bool OnFluidInteract(int interactValue, int x, int y, int z, int fluidValue)
    {
        if (BlocksManager.Blocks[Terrain.ExtractContents(interactValue)] is WaterBlock)
        {
            SubsystemAudio.PlayRandomSound("Audio/Sizzles", 1f, _random.Float(-0.1f, 0.1f), new Vector3(x, y, z), 5f,
                true);
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
        _subsystemParticles = Project.FindSubsystem<SubsystemParticles>(true)!;
    }

    private void ApplyMagmaNeighborhoodEffect(int x, int y, int z)
    {
        _subsystemFireBlockBehavior.SetCellOnFire(x, y, z, 1f);
        switch (SubsystemTerrain.Terrain.GetCellContents(x, y, z))
        {
            case 61:
            case 62:
                SubsystemTerrain.DestroyCell(0, x, y, z, 0, false, false);
                _subsystemParticles.AddParticleSystem(new BurntDebrisParticleSystem(SubsystemTerrain,
                    new Vector3(x + 0.5f, y + 1, z + 0.5f)));
                break;
            case 8:
                SubsystemTerrain.ChangeCell(x, y, z, 2);
                _subsystemParticles.AddParticleSystem(new BurntDebrisParticleSystem(SubsystemTerrain,
                    new Vector3(x + 0.5f, y + 1, z + 0.5f)));
                break;
        }
    }
}
