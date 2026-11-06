using EntitySystem.TemplatesDatabase;

using Game.Modding.Blocks;

namespace Game.Subsystems;

public sealed class SubsystemBlockFlamePresentation : SubsystemBlockBehavior
{
    private sealed record Flame(int Value, FireParticleSystem System);

    private readonly Dictionary<Point3, Flame> _flames = [];

    private readonly Func<Vector3, float, float, FireParticleSystem> _createFire;

    private SubsystemParticles _particles = null!;

    private bool _disposed;

    private bool _loaded;

    public SubsystemBlockFlamePresentation() : this((position, size, distance) =>
        new FireParticleSystem(position, size, distance)
    )
    {
    }

    internal SubsystemBlockFlamePresentation(Func<Vector3, float, float, FireParticleSystem> createFire) =>
        _createFire = createFire;

    public override ResourceId[] HandledBlocks =>
    [
        ResourceId.Parse("game:torch"), ResourceId.Parse("game:wicker_lamp"), ResourceId.Parse("game:jack_o_lantern"),
        ResourceId.Parse("game:furnace"), ResourceId.Parse("game:lit_furnace")
    ];

    public override void Load(ValuesDictionary valuesDictionary)
    {
        base.Load(valuesDictionary);
        _particles = Project.FindSubsystem<SubsystemParticles>(true)!;
        SubsystemTerrain.Closing += OnTerrainClosing;
        _loaded = true;
    }

    public override void OnBlockAdded(int value, int oldValue, int x, int y, int z) =>
        SetFlame(value, new Point3(x, y, z));

    public override void OnBlockAdded(int value, int oldValue, int x, int y, int z, ComponentMiner miner) =>
        SetFlame(value, new Point3(x, y, z));

    public override void OnBlockGenerated(int value, int x, int y, int z, bool isLoaded) =>
        SetFlame(value, new Point3(x, y, z));

    public override void OnBlockModified(int value, int oldValue, int x, int y, int z) =>
        SetFlame(value, new Point3(x, y, z));

    public override void OnBlockRemoved(int value, int newValue, int x, int y, int z) =>
        RemoveFlame(new Point3(x, y, z));

    public override void OnChunkDiscarding(TerrainChunk chunk)
    {
        foreach (var point in _flames.Keys.Where(point =>
                     point.X >= chunk.Origin.X &&
                     point.X < chunk.Origin.X + 16 &&
                     point.Z >= chunk.Origin.Y &&
                     point.Z < chunk.Origin.Y + 16
                 ).ToArray())
        {
            RemoveFlame(point);
        }
    }

    public override void Dispose()
    {
        if (_disposed) { return; }

        _disposed = true;
        if (_loaded) { SubsystemTerrain.Closing -= OnTerrainClosing; }

        foreach (var point in _flames.Keys.ToArray()) { RemoveFlame(point); }
    }

    private void OnTerrainClosing(SubsystemTerrain terrain) => Dispose();

    private void SetFlame(int value, Point3 point)
    {
        if (_disposed || !WorldRuntimeContext.IsProjectActive(Project)) { return; }

        var contents = Terrain.ExtractContents(value);
        if (contents == BuiltInBlockRegistry.GetIndex<FurnaceBlock>())
        {
            RemoveFlame(point);
            return;
        }

        if (_flames.TryGetValue(point, out var current) &&
            current.Value == value &&
            _particles.ContainsParticleSystem(current.System)) { return; }

        RemoveFlame(point);
        Vector3 offset;
        float size;
        var distance = 24f;
        if (contents == BuiltInBlockRegistry.GetIndex<TorchBlock>())
        {
            offset = Terrain.ExtractData(value) switch
            {
                0 => new(.5f, .58f, .27f), 1 => new(.27f, .58f, .5f),
                2 => new(.5f, .58f, .73f), 3 => new(.73f, .58f, .5f), _ => new(.5f, .53f, .5f)
            };
            size = .15f;
        }
        else if (contents == BuiltInBlockRegistry.GetIndex<JackOLanternBlock>())
        {
            offset = new(.5f, .1f, .5f);
            size = .1f;
        }
        else if (contents == BuiltInBlockRegistry.GetIndex<LitFurnaceBlock>())
        {
            offset = new(.5f, .2f, .5f);
            size = .15f;
            distance = 16f;
        }
        else if (contents == BuiltInBlockRegistry.GetIndex<WickerLampBlock>())
        {
            offset = new(.5f, .2f, .5f);
            size = .2f;
        }
        else { return; }

        var system = _createFire(new Vector3(point) + offset, size, distance);
        _particles.AddParticleSystem(system);
        _flames.Add(point, new Flame(value, system));
    }

    private void RemoveFlame(Point3 point)
    {
        if (_flames.Remove(point, out var flame) && _particles.ContainsParticleSystem(flame.System))
        {
            _particles.RemoveParticleSystem(flame.System);
        }
    }
}
