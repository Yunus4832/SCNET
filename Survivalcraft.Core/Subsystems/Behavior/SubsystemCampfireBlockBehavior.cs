using EntitySystem.TemplatesDatabase;

namespace Game.Subsystems.Behavior;

public class SubsystemCampfireBlockBehavior : SubsystemBlockBehavior, IUpdateable
{
    public UpdateExecutionSide UpdateSide => UpdateExecutionSide.Authority;

    private SubsystemSpatialSoundRequests _soundRequests = null!;

    private readonly HashSet<Point3> _campfireCells = [];

    private readonly Random _random = new();

    private SubsystemTime _subsystemTime = null!;

    private SubsystemWeather _subsystemWeather = null!;

    private readonly List<Point3> _toReduce = [];

    private int _updateIndex;

    public IEnumerable<Point3> Campfires => _campfireCells;

    public event Action<Point3, int>? CampfireChanged;

    public override ResourceId[] HandledBlocks => [];

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public void Update(float dt)
    {
        if (!WorldRuntimeContext.IsProjectActive(Project) || !WorldRuntimeContext.For(Project).IsAuthority) { return; }

        if (_subsystemTime.PeriodicGameTimeEvent(5.0, 0.0))
        {
            _updateIndex++;
            foreach (var key in Campfires)
            {
                var precipitationShaftInfo = _subsystemWeather.GetPrecipitationShaftInfo(key.X, key.Z);
                if ((precipitationShaftInfo.Intensity > 0f && key.Y >= precipitationShaftInfo.YLimit - 1) ||
                    _updateIndex % 5 == 0)
                {
                    _toReduce.Add(key);
                }
            }

            foreach (var item in _toReduce)
            {
                ResizeCampfire(item.X, item.Y, item.Z, -1, true);
            }

            _toReduce.Clear();
        }
    }

    public override void OnNeighborBlockChanged(int x, int y, int z, int neighborX, int neighborY, int neighborZ)
    {
        var cellContents = SubsystemTerrain.Terrain.GetCellContents(x, y - 1, z);
        if (WorldContentBinding.For(Project).Catalog.Blocks[cellContents].Transparent)
        {
            SubsystemTerrain.DestroyCell(0, x, y, z, 0, false, false);
        }
    }

    public override void OnBlockAdded(int value, int oldValue, int x, int y, int z)
    {
        SetCampfire(value, x, y, z);
    }

    public override void OnBlockRemoved(int value, int newValue, int x, int y, int z)
    {
        RemoveCampfire(x, y, z);
    }

    public override void OnBlockModified(int value, int oldValue, int x, int y, int z)
    {
        RemoveCampfire(x, y, z);
        SetCampfire(value, x, y, z);
    }

    public override void OnBlockGenerated(int value, int x, int y, int z, bool isLoaded)
    {
        SetCampfire(value, x, y, z);
    }

    public override void OnChunkDiscarding(TerrainChunk chunk)
    {
        var list = new List<Point3>();
        foreach (var key in Campfires)
        {
            if (key.X >= chunk.Origin.X &&
                key.X < chunk.Origin.X + 16 &&
                key.Z >= chunk.Origin.Y &&
                key.Z < chunk.Origin.Y + 16)
            {
                list.Add(key);
            }
        }

        foreach (var item in list)
        {
            ResizeCampfire(item.X, item.Y, item.Z, -15, false);
            RemoveCampfire(item.X, item.Y, item.Z);
        }
    }

    public override void OnHitByProjectile(CellFace cellFace, WorldItem worldItem)
    {
        if (!worldItem.ToRemove &&
            AddFuel(
                cellFace.X,
                cellFace.Y,
                cellFace.Z,
                worldItem.Value,
                (worldItem as Pickable)?.Count ?? 1
            ))
        {
            worldItem.ToRemove = true;
        }
    }

    public override bool OnInteract(TerrainRaycastResult raycastResult, ComponentMiner componentMiner)
    {
        if (AddFuel(
                raycastResult.CellFace.X,
                raycastResult.CellFace.Y,
                raycastResult.CellFace.Z,
                componentMiner.ActiveBlockValue,
                1
            ))
        {
            componentMiner.RemoveActiveTool(1);
        }

        return true;
    }

    public override void Load(ValuesDictionary valuesDictionary)
    {
        base.Load(valuesDictionary);
        _subsystemTime = Project.FindSubsystem<SubsystemTime>(true)!;
        _subsystemWeather = Project.FindSubsystem<SubsystemWeather>(true)!;
        _soundRequests = Project.FindSubsystem<SubsystemSpatialSoundRequests>(true)!;
    }

    private void SetCampfire(int value, int x, int y, int z)
    {
        var point = new Point3(x, y, z);
        if (Terrain.ExtractData(value) <= 0)
        {
            RemoveCampfire(x, y, z);
            return;
        }

        _campfireCells.Add(point);
        CampfireChanged?.Invoke(point, value);
    }

    private void RemoveCampfire(int x, int y, int z)
    {
        var point = new Point3(x, y, z);
        if (_campfireCells.Remove(point)) { CampfireChanged?.Invoke(point, 0); }
    }

    public override void Dispose()
    {
        CampfireChanged = null;
        _campfireCells.Clear();
    }

    public bool AddFuel(int x, int y, int z, int value, int count)
    {
        if (Terrain.ExtractData(SubsystemTerrain.Terrain.GetCellValue(x, y, z)) <= 0)
        {
            return false;
        }

        var num = Terrain.ExtractContents(value);
        var block = WorldContentBinding.For(Project).Catalog.Blocks[num];
        if (Project.FindSubsystem<SubsystemExplosions>(true)!.TryExplodeBlock(x, y, z, value))
        {
            return true;
        }

        if (block is SnowBlock || block is SnowballBlock || block is IceBlock)
        {
            return ResizeCampfire(x, y, z, -1, true);
        }

        if (!(block.FuelHeatLevel > 0f))
        {
            return false;
        }

        var num2 = count * MathUtils.Min(block.FuelFireDuration, 20f) / 5f;
        var num3 = (int)num2;
        var num4 = num2 - num3;
        if (_random.Float(0f, 1f) < num4)
        {
            num3++;
        }

        return num3 <= 0 || ResizeCampfire(x, y, z, num3, true);
    }

    private bool ResizeCampfire(int x, int y, int z, int steps, bool playSound)
    {
        var cellValue = SubsystemTerrain.Terrain.GetCellValue(x, y, z);
        var num = Terrain.ExtractData(cellValue);
        if (num <= 0)
        {
            return false;
        }

        var num2 = MathUtils.Clamp(num + steps, 0, 15);
        if (num2 == num)
        {
            return false;
        }

        var value = Terrain.ReplaceData(cellValue, num2);
        SubsystemTerrain.ChangeCell(x, y, z, value);
        if (!playSound)
        {
            return true;
        }

        if (steps >= 0)
        {
            _soundRequests.RequestSound(
                "Audio/BlockPlaced",
                1f,
                0f,
                new Vector3(x, y, z),
                3f,
                false
            );
        }
        else
        {
            _soundRequests.RequestRandomSound(
                "Audio/Sizzles",
                1f,
                0f,
                new Vector3(x, y, z),
                3f,
                true
            );
        }

        return true;
    }
}
