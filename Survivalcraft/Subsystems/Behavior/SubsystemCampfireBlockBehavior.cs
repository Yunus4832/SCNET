using EntitySystem.TemplatesDatabase;

namespace Game.Subsystems;

public class SubsystemCampfireBlockBehavior : SubsystemBlockBehavior, IUpdateable
{
    private float _fireSoundVolume;

    private readonly HashSet<Point3> _campfireCells = [];

    private readonly Dictionary<Point3, FireParticleSystem> _particleSystemsByCell = new();

    private readonly Random _random = new();

    private SubsystemAmbientSounds _subsystemAmbientSounds = null!;

    private SubsystemParticles _subsystemParticles = null!;

    private SubsystemTime _subsystemTime = null!;

    private SubsystemWeather _subsystemWeather = null!;

    private readonly List<Point3> _toReduce = [];

    private int _updateIndex;

    public IEnumerable<Point3> Campfires
    {
        get
        {
            if (RunMode.Value is RunModeType.HeadlessServer)
            {
                return _campfireCells;
            }

            return _particleSystemsByCell.Keys;
        }
    }

    public override int[] HandledBlocks => [];

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public void Update(float dt)
    {
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

        if (RunMode.Value is RunModeType.HeadlessServer)
        {
            return;
        }

        if (Time.PeriodicEvent(0.5, 0.0))
        {
            var num = float.MaxValue;
            foreach (var key2 in Campfires)
            {
                var x = _subsystemAmbientSounds.SubsystemAudio.CalculateListenerDistanceSquared(new Vector3(key2.X,
                    key2.Y, key2.Z));
                num = MathUtils.Min(num, x);
            }

            _fireSoundVolume = _subsystemAmbientSounds.SubsystemAudio.CalculateVolume(MathUtils.Sqrt(num), 2f);
        }

        _subsystemAmbientSounds.FireSoundVolume =
            MathUtils.Max(_subsystemAmbientSounds.FireSoundVolume, _fireSoundVolume);
    }

    public override void OnNeighborBlockChanged(int x, int y, int z, int neighborX, int neighborY, int neighborZ)
    {
        var cellContents = SubsystemTerrain.Terrain.GetCellContents(x, y - 1, z);
        if (BlocksManager.Blocks[cellContents].Transparent)
        {
            SubsystemTerrain.DestroyCell(0, x, y, z, 0, false, false);
        }
    }

    public override void OnBlockAdded(int value, int oldValue, int x, int y, int z)
    {
        AddCampfireParticleSystem(value, x, y, z);
    }

    public override void OnBlockRemoved(int value, int newValue, int x, int y, int z)
    {
        RemoveCampfireParticleSystem(x, y, z);
    }

    public override void OnBlockModified(int value, int oldValue, int x, int y, int z)
    {
        RemoveCampfireParticleSystem(x, y, z);
        AddCampfireParticleSystem(value, x, y, z);
    }

    public override void OnBlockGenerated(int value, int x, int y, int z, bool isLoaded)
    {
        AddCampfireParticleSystem(value, x, y, z);
    }

    public override void OnChunkDiscarding(TerrainChunk chunk)
    {
        var list = new List<Point3>();
        foreach (var key in Campfires)
        {
            if (key.X >= chunk.Origin.X && key.X < chunk.Origin.X + 16 && key.Z >= chunk.Origin.Y &&
                key.Z < chunk.Origin.Y + 16)
            {
                list.Add(key);
            }
        }

        foreach (var item in list)
        {
            ResizeCampfire(item.X, item.Y, item.Z, -15, false);
            RemoveCampfireParticleSystem(item.X, item.Y, item.Z);
        }
    }

    public override void OnHitByProjectile(CellFace cellFace, WorldItem worldItem)
    {
        if (!worldItem.ToRemove && AddFuel(cellFace.X, cellFace.Y, cellFace.Z, worldItem.Value,
                (worldItem as Pickable)?.Count ?? 1))
        {
            worldItem.ToRemove = true;
        }
    }

    public override bool OnInteract(TerrainRaycastResult raycastResult, ComponentMiner componentMiner)
    {
        if (AddFuel(raycastResult.CellFace.X, raycastResult.CellFace.Y, raycastResult.CellFace.Z,
                componentMiner.ActiveBlockValue, 1))
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
        if (RunMode.Value is RunModeType.HeadlessServer)
        {
            return;
        }

        _subsystemParticles = Project.FindSubsystem<SubsystemParticles>(true)!;
        _subsystemAmbientSounds = Project.FindSubsystem<SubsystemAmbientSounds>(true)!;
    }

    public void AddCampfireParticleSystem(int value, int x, int y, int z)
    {
        var num = Terrain.ExtractData(value);
        if (num <= 0)
        {
            return;
        }

        if (RunMode.Value is RunModeType.HeadlessServer)
        {
            _campfireCells.Add(new Point3(x, y, z));
        }
        else
        {
            var v = new Vector3(0.5f, 0.15f, 0.5f);
            var size = MathUtils.Lerp(0.2f, 0.5f, num / 15f);
            var fireParticleSystem = new FireParticleSystem(new Vector3(x, y, z) + v, size, 256f);
            _subsystemParticles.AddParticleSystem(fireParticleSystem);
            _particleSystemsByCell[new Point3(x, y, z)] = fireParticleSystem;
        }
    }

    public void RemoveCampfireParticleSystem(int x, int y, int z)
    {
        var key = new Point3(x, y, z);
        if (RunMode.Value is RunModeType.HeadlessServer)
        {
            _campfireCells.Remove(key);
            return;
        }

        if (!_particleSystemsByCell.TryGetValue(key, out var value))
        {
            return;
        }

        value.IsStopped = true;
        _particleSystemsByCell.Remove(key);
    }

    public bool AddFuel(int x, int y, int z, int value, int count)
    {
        if (Terrain.ExtractData(SubsystemTerrain.Terrain.GetCellValue(x, y, z)) <= 0)
        {
            return false;
        }

        var num = Terrain.ExtractContents(value);
        var block = BlocksManager.Blocks[num];
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

        if (RunMode.Value is RunModeType.HeadlessServer)
        {
            return true;
        }

        if (steps >= 0)
        {
            _subsystemAmbientSounds.SubsystemAudio.PlaySound("Audio/BlockPlaced", 1f, 0f,
                new Vector3(x, y, z), 3f, false);
        }
        else
        {
            _subsystemAmbientSounds.SubsystemAudio.PlayRandomSound("Audio/Sizzles", 1f, 0f,
                new Vector3(x, y, z), 3f, true);
        }

        return true;
    }
}
