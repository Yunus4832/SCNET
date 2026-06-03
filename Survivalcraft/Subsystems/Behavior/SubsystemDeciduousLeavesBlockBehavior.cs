using EntitySystem.TemplatesDatabase;

namespace Game.Subsystems;

public class SubsystemDeciduousLeavesBlockBehavior : SubsystemPollableBlockBehavior, IUpdateable
{
    private DynamicArray<LeafParticles> _leafParticles = [];

    private readonly Random _random = new();

    private SubsystemCellChangeQueue _subsystemCellChangeQueue = null!;

    private SubsystemGameInfo _subsystemGameInfo = null!;

    private SubsystemGameWidgets _subsystemGameWidgets = null!;

    private SubsystemParticles _subsystemParticles = null!;

    private SubsystemSeasons _subsystemSeasons = null!;

    private SubsystemTerrain _subsystemTerrain = null!;

    private SubsystemTime _subsystemTime = null!;

    private DynamicArray<LeafParticles> _tmpLeafParticles = [];

    public override int[] HandledBlocks => [];

    UpdateOrder IUpdateable.UpdateOrder => UpdateOrder.Default;

    void IUpdateable.Update(float dt)
    {
        if (!_subsystemTime.PeriodicGameTimeEvent(1.0, 0.0))
        {
            return;
        }

        foreach (var leafParticle in _leafParticles)
        {
            if (_subsystemTime.GameTime >= leafParticle.Time)
            {
                if (_subsystemGameWidgets.CalculateDistanceFromNearestView(new Vector3(leafParticle.Position)) < 32f)
                {
                    var cellValue = _subsystemTerrain.Terrain.GetCellValue(leafParticle.Position.X,
                        leafParticle.Position.Y, leafParticle.Position.Z);
                    var num = Terrain.ExtractContents(cellValue);
                    if (BlocksManager.Blocks[num] is DeciduousLeavesBlock deciduousLeavesBlock &&
                        deciduousLeavesBlock.GetLeafDropProbability(cellValue) > 0f)
                    {
                        _subsystemParticles.AddParticleSystem(new LeavesParticleSystem(_subsystemTerrain,
                            leafParticle.Position, _random.Int(1, 2), true, false, cellValue));
                    }
                }
            }
            else
            {
                _tmpLeafParticles.Add(leafParticle);
            }
        }

        Utilities.Swap(ref _leafParticles, ref _tmpLeafParticles);
        _tmpLeafParticles.Clear();
    }

    public void CreateFallenLeaves(Point3 p, bool applyImmediately)
    {
        int? num = null;
        while (p.Y is >= 1 and < 256)
        {
            var cellValue = _subsystemTerrain.Terrain.GetCellValue(p.X, p.Y, p.Z);
            if (num.HasValue)
            {
                if (SubsystemFallenLeavesBlockBehavior.CanSupportFallenLeaves(cellValue) &&
                    SubsystemFallenLeavesBlockBehavior.CanBeReplacedByFallenLeaves(num.Value))
                {
                    _subsystemCellChangeQueue.QueueCellChange(p.X, p.Y + 1, p.Z, Terrain.MakeBlockValue(261),
                        applyImmediately);
                    break;
                }

                if (SubsystemFallenLeavesBlockBehavior.StopsFallenLeaves(cellValue))
                {
                    break;
                }
            }

            num = cellValue;
            p.Y--;
        }
    }

    public override void OnBlockGenerated(int value, int x, int y, int z, bool isLoaded)
    {
        UpdateTimeOfYear(value, x, y, z, true);
        QueueLeafParticles(value, x, y, z);
    }

    public override void OnPoll(int value, int x, int y, int z, int pollPass)
    {
        UpdateTimeOfYear(value, x, y, z, false);
        QueueLeafParticles(value, x, y, z);
    }

    public override void Load(ValuesDictionary valuesDictionary)
    {
        base.Load(valuesDictionary);
        _subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true)!;
        _subsystemSeasons = Project.FindSubsystem<SubsystemSeasons>(true)!;
        _subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true)!;
        _subsystemTime = Project.FindSubsystem<SubsystemTime>(true)!;
        _subsystemGameWidgets = Project.FindSubsystem<SubsystemGameWidgets>(true)!;
        _subsystemParticles = Project.FindSubsystem<SubsystemParticles>(true)!;
        _subsystemCellChangeQueue = Project.FindSubsystem<SubsystemCellChangeQueue>(true)!;
    }

    public void UpdateTimeOfYear(int value, int x, int y, int z, bool applyImmediately)
    {
        var num = 0.03f * MathUtils.Hash((uint)(x + y * 59 + z * 3319)) / 4.2949673E+09f;
        var timeOfYear = IntervalUtils.Normalize(_subsystemGameInfo.WorldSettings.TimeOfYear + num);
        var obj = (DeciduousLeavesBlock)BlocksManager.Blocks[Terrain.ExtractContents(value)];
        var num2 = Terrain.ExtractData(value);
        var num3 = obj.SetTimeOfYear(num2, timeOfYear);
        if (num3 == num2)
        {
            return;
        }

        var value2 = Terrain.ReplaceData(value, num3);
        _subsystemCellChangeQueue.QueueCellChange(x, y, z, value2, applyImmediately);
        var season = DeciduousLeavesBlock.GetSeason(num2);
        if (DeciduousLeavesBlock.GetSeason(num3) == Season.Winter && season != Season.Winter)
        {
            CreateFallenLeaves(new Point3(x, y, z), applyImmediately);
        }
    }

    private void QueueLeafParticles(int value, int x, int y, int z)
    {
        var deciduousLeavesBlock = (DeciduousLeavesBlock)BlocksManager.Blocks[Terrain.ExtractContents(value)];
        if (_leafParticles.Count < 30000 &&
            _random.Bool(deciduousLeavesBlock.GetLeafDropProbability(value) / 60f * 60f) &&
            _subsystemGameWidgets.CalculateDistanceFromNearestView(new Vector3(x, y, z)) < 128f)
        {
            _leafParticles.Add(new LeafParticles
            {
                Position = new Point3(x, y, z),
                Time = _subsystemTime.GameTime + _random.Float(0f, 60f)
            });
        }
    }

    public struct LeafParticles
    {
        public double Time;

        public Point3 Position;
    }
}
