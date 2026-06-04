using EntitySystem.TemplatesDatabase;

namespace Game.Subsystems;

public class SubsystemTorchBlockBehavior : SubsystemBlockBehavior
{
    private readonly Dictionary<Point3, FireParticleSystem> _particleSystemsByCell = new();

    private SubsystemParticles _subsystemParticles = null!;

    public override int[] HandledBlocks =>
    [
        TorchBlock.Index,
        WickerLampBlock.Index,
        JackOLanternBlock.Index
    ];

    public override void OnNeighborBlockChanged(int x, int y, int z, int neighborX, int neighborY, int neighborZ)
    {
        var cellValueFast = SubsystemTerrain.Terrain.GetCellValueFast(x, y, z);
        switch (Terrain.ExtractContents(cellValueFast))
        {
            case TorchBlock.Index:
            {
                var point = CellFace.FaceToPoint3(Terrain.ExtractData(cellValueFast));
                var x2 = x - point.X;
                var y2 = y - point.Y;
                var z2 = z - point.Z;
                var cellContents2 = SubsystemTerrain.Terrain.GetCellContents(x2, y2, z2);
                if (!BlocksManager.Blocks[cellContents2].Collidable)
                {
                    SubsystemTerrain.DestroyCell(0, x, y, z, 0, false, false);
                }

                break;
            }
            case JackOLanternBlock.Index:
            {
                var cellContents = SubsystemTerrain.Terrain.GetCellContents(x, y - 1, z);
                if (!BlocksManager.Blocks[cellContents].Collidable)
                {
                    SubsystemTerrain.DestroyCell(0, x, y, z, 0, false, false);
                }

                break;
            }
        }
    }

    public override void OnBlockAdded(int value, int oldValue, int x, int y, int z)
    {
        AddTorch(value, x, y, z);
    }

    public override void OnBlockRemoved(int value, int newValue, int x, int y, int z)
    {
        RemoveTorch(x, y, z);
    }

    public override void OnBlockModified(int value, int oldValue, int x, int y, int z)
    {
        RemoveTorch(x, y, z);
        AddTorch(value, x, y, z);
    }

    public override void OnBlockGenerated(int value, int x, int y, int z, bool isLoaded)
    {
        AddTorch(value, x, y, z);
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
            RemoveTorch(item.X, item.Y, item.Z);
        }
    }

    public override void Load(ValuesDictionary valuesDictionary)
    {
        base.Load(valuesDictionary);
        _subsystemParticles = Project.FindSubsystem<SubsystemParticles>(true)!;
    }

    public void AddTorch(int value, int x, int y, int z)
    {
        if (RunMode.Value is RunModeType.HeadlessServer)
        {
            return;
        }

        Vector3 v;
        float size;
        switch (Terrain.ExtractContents(value))
        {
            case TorchBlock.Index:
                v = Terrain.ExtractData(value) switch
                {
                    0 => new Vector3(0.5f, 0.58f, 0.27f),
                    1 => new Vector3(0.27f, 0.58f, 0.5f),
                    2 => new Vector3(0.5f, 0.58f, 0.73f),
                    3 => new Vector3(0.73f, 0.58f, 0.5f),
                    _ => new Vector3(0.5f, 0.53f, 0.5f)
                };

                size = 0.15f;
                break;
            case JackOLanternBlock.Index:
                v = new Vector3(0.5f, 0.1f, 0.5f);
                size = 0.1f;
                break;
            default:
                v = new Vector3(0.5f, 0.2f, 0.5f);
                size = 0.2f;
                break;
        }

        var fireParticleSystem = new FireParticleSystem(new Vector3(x, y, z) + v, size, 24f);
        _subsystemParticles.AddParticleSystem(fireParticleSystem);
        _particleSystemsByCell[new Point3(x, y, z)] = fireParticleSystem;
    }

    public void RemoveTorch(int x, int y, int z)
    {
        var key = new Point3(x, y, z);
        if (RunMode.Value is RunModeType.HeadlessServer)
        {
            _particleSystemsByCell.Remove(key);
            return;
        }

        if (!_particleSystemsByCell.TryGetValue(key, out var particleSystem))
        {
            return;
        }

        _subsystemParticles.RemoveParticleSystem(particleSystem);
        _particleSystemsByCell.Remove(key);
    }
}
