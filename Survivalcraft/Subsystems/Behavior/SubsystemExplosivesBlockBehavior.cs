using System.Globalization;

using Engine.Audio;

using EntitySystem.TemplatesDatabase;

namespace Game.Subsystems;

public class SubsystemExplosivesBlockBehavior : SubsystemBlockBehavior, IUpdateable
{
    private readonly Dictionary<Point3, ExplosiveData> _explosiveDataByPoint = new();

    private Sound _fuseSound = null!;

    private readonly Random _random = new();

    private SubsystemAudio _subsystemAudio = null!;

    private SubsystemExplosions _subsystemExplosions = null!;

    private SubsystemFireBlockBehavior _subsystemFireBlockBehavior = null!;

    private SubsystemParticles _subsystemParticles = null!;

    private SubsystemTerrain _subsystemTerrain = null!;

    public override int[] HandledBlocks => [];

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public void Update(float dt)
    {
        var num = float.MaxValue;
        if (_explosiveDataByPoint.Count > 0)
        {
            var array = _explosiveDataByPoint.Values.ToArray();
            foreach (var explosiveData in array)
            {
                var point = explosiveData.Point;
                var cellValue = _subsystemTerrain.Terrain.GetCellValue(point.X, point.Y, point.Z);
                var num2 = Terrain.ExtractContents(cellValue);
                var block = BlocksManager.Blocks[num2];
                if (explosiveData.FuseParticleSystem == null)
                {
                    if (block is GunpowderKegBlock gunpowderKegBlock)
                    {
                        explosiveData.FuseParticleSystem =
                            new FuseParticleSystem(
                                new Vector3(point.X, point.Y, point.Z) + gunpowderKegBlock.FuseOffset);
                        _subsystemParticles.AddParticleSystem(explosiveData.FuseParticleSystem);
                    }
                }

                explosiveData.TimeToExplosion -= dt;
                if (explosiveData.TimeToExplosion <= 0f)
                {
                    _subsystemExplosions.TryExplodeBlock(explosiveData.Point.X, explosiveData.Point.Y,
                        explosiveData.Point.Z, cellValue, explosiveData.Miner);
                }

                var x = _subsystemAudio.CalculateListenerDistance(new Vector3(point.X, point.Y, point.Z) +
                                                                  new Vector3(0.5f));
                num = MathUtils.Min(num, x);
            }
        }

        _fuseSound.Volume = SettingsManager.SoundsVolume * _subsystemAudio.CalculateVolume(num, 2f);
        if (_fuseSound.Volume > AudioManager.MinAudibleVolume)
        {
            _fuseSound.Play();
        }
        else
        {
            _fuseSound.Pause();
        }
    }

    public bool IgniteFuse(int x, int y, int z, PlayerData? miner = null)
    {
        var cellContents = _subsystemTerrain.Terrain.GetCellContents(x, y, z);
        switch (BlocksManager.Blocks[cellContents])
        {
            case GunpowderKegBlock:
                //延时爆炸
                AddExplosive(new Point3(x, y, z), _random.Float(6f, 7f), miner);
                return true;
            case DetonatorBlock:
                //延时爆炸
                AddExplosive(new Point3(x, y, z), _random.Float(0.8f, 1.2f), miner);
                return true;
            default:
                return false;
        }
    }

    public override void OnNeighborBlockChanged(int x, int y, int z, int neighborX, int neighborY, int neighborZ)
    {
        if (_subsystemFireBlockBehavior.IsCellOnFire(x, y, z))
        {
            IgniteFuse(x, y, z);
        }
    }

    public override void OnBlockRemoved(int value, int newValue, int x, int y, int z)
    {
        var point = new Point3(x, y, z);
        RemoveExplosive(point);
    }

    public override void OnChunkDiscarding(TerrainChunk chunk)
    {
        var list = new List<Point3>();
        foreach (var key in _explosiveDataByPoint.Keys)
        {
            if (key.X >= chunk.Origin.X && key.X < chunk.Origin.X + 16 && key.Z >= chunk.Origin.Y &&
                key.Z < chunk.Origin.Y + 16)
            {
                list.Add(key);
            }
        }

        foreach (var item in list)
        {
            RemoveExplosive(item);
        }
    }

    public override void OnExplosion(int value, int x, int y, int z, float damage)
    {
        var num = Terrain.ExtractContents(value);
        var block = BlocksManager.Blocks[num];
        if (block.GetExplosionPressure(value) > 0f && MathUtils.Saturate(damage / block.ExplosionResilience) > 0.01f &&
            SubsystemExplosions.SharedRandom.Float(0f, 1f) < 0.5f)
        {
            IgniteFuse(x, y, z);
        }
    }

    public override void Load(ValuesDictionary valuesDictionary)
    {
        base.Load(valuesDictionary);
        _subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true)!;
        _subsystemParticles = Project.FindSubsystem<SubsystemParticles>(true)!;
        _subsystemExplosions = Project.FindSubsystem<SubsystemExplosions>(true)!;
        _subsystemFireBlockBehavior = Project.FindSubsystem<SubsystemFireBlockBehavior>(true)!;
        _subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true)!;
        _fuseSound = _subsystemAudio.CreateSound("Audio/Fuse");
        _fuseSound.IsLooped = true;
        foreach (ValuesDictionary value3 in valuesDictionary.GetValue<ValuesDictionary>("Explosives").Values)
        {
            var value = value3.GetValue<Point3>("Point");
            var value2 = value3.GetValue<float>("TimeToExplosion");
            AddExplosive(value, value2);
        }
    }

    public override void Save(ValuesDictionary valuesDictionary)
    {
        base.Save(valuesDictionary);
        var num = 0;
        var valuesDictionary2 = new ValuesDictionary();
        valuesDictionary.SetValue("Explosives", valuesDictionary2);
        foreach (var value in _explosiveDataByPoint.Values)
        {
            var valuesDictionary3 = new ValuesDictionary();
            valuesDictionary2.SetValue(num++.ToString(CultureInfo.InvariantCulture), valuesDictionary3);
            valuesDictionary3.SetValue("Point", value.Point);
            valuesDictionary3.SetValue("TimeToExplosion", value.TimeToExplosion);
        }
    }

    public override void Dispose()
    {
        Utilities.Dispose(ref _fuseSound!);
    }

    private void AddExplosive(Point3 point, float timeToExplosion, PlayerData? miner = null)
    {
        if (_explosiveDataByPoint.ContainsKey(point))
        {
            return;
        }

        var explosiveData = new ExplosiveData
        {
            Point = point,
            Miner = miner,
            TimeToExplosion = timeToExplosion
        };
        _explosiveDataByPoint.Add(point, explosiveData);
    }

    private void RemoveExplosive(Point3 point)
    {
        if (!_explosiveDataByPoint.Remove(point, out var value))
        {
            return;
        }

        if (value.FuseParticleSystem != null)
        {
            _subsystemParticles.RemoveParticleSystem(value.FuseParticleSystem);
        }
    }

    private class ExplosiveData
    {
        public FuseParticleSystem? FuseParticleSystem;

        public PlayerData? Miner;

        public Point3 Point;

        public float TimeToExplosion;
    }
}
