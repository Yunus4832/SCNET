using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Subsystems;

public class SubsystemBlocksScanner : Subsystem, IUpdateable
{
    public const float ScanPeriod = 60f;

    private SubsystemPollableBlockBehavior[][] _pollableBehaviorsByContents = [];

    private Point2 _pollChunkCoordinates;

    private float _pollCount;

    private int _pollPass;

    private int _pollX;

    private int _pollZ;

    private SubsystemBlockBehaviors _subsystemBlockBehaviors = null!;

    private SubsystemTerrain _subsystemTerrain = null!;

    public UpdateOrder UpdateOrder => UpdateOrder.BlocksScanner;

    public void Update(float dt)
    {
        var terrain = _subsystemTerrain.Terrain;
        _pollCount += terrain.AllocatedChunks.Length * 16 * 16 * dt / 60f;
        _pollCount = MathUtils.Clamp(_pollCount, 0f, 200f);
        var nextChunk = terrain.GetNextChunk(_pollChunkCoordinates.X, _pollChunkCoordinates.Y);
        if (nextChunk == null)
        {
            return;
        }

        while (_pollCount >= 1f)
        {
            if (nextChunk.MainThreadState <= TerrainChunkState.InvalidContents4)
            {
                _pollCount -= 65536f;
            }
            else
            {
                while (_pollX < 16)
                {
                    while (_pollZ < 16)
                    {
                        if (_pollCount < 1f)
                        {
                            return;
                        }

                        _pollCount -= 1f;
                        var topHeightFast = nextChunk.GetTopHeightFast(_pollX, _pollZ);
                        var num = TerrainChunk.CalculateCellIndex(_pollX, 0, _pollZ);
                        var num2 = 0;
                        while (num2 <= topHeightFast)
                        {
                            var cellValueFast = nextChunk.GetCellValueFast(num);
                            var num3 = Terrain.ExtractContents(cellValueFast);
                            if (num3 != 0)
                            {
                                var array = _pollableBehaviorsByContents[num3];
                                foreach (var item in array)
                                {
                                    item.OnPoll(
                                        cellValueFast,
                                        nextChunk.Origin.X + _pollX,
                                        num2,
                                        nextChunk.Origin.Y + _pollZ,
                                        _pollPass
                                    );
                                }
                            }

                            num2++;
                            num++;
                        }

                        _pollZ++;
                    }

                    _pollZ = 0;
                    _pollX++;
                }

                _pollX = 0;
            }

            ScanningChunkCompleted?.Invoke(nextChunk);
            nextChunk = terrain.GetNextChunk(nextChunk.Coords.X + 1, nextChunk.Coords.Y);
            if (nextChunk == null)
            {
                break;
            }

            if (Terrain.ComparePoints(nextChunk.Coords, _pollChunkCoordinates) < 0)
            {
                _pollPass++;
            }

            _pollChunkCoordinates = nextChunk.Coords;
        }
    }

    public event Action<TerrainChunk>? ScanningChunkCompleted;

    public override void Load(ValuesDictionary valuesDictionary)
    {
        _subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true)!;
        _subsystemBlockBehaviors = Project.FindSubsystem<SubsystemBlockBehaviors>(true)!;
        _pollChunkCoordinates = valuesDictionary.GetValue<Point2>("PollChunkCoordinates");
        var value = valuesDictionary.GetValue<Point2>("PollPoint");
        _pollX = value.X;
        _pollZ = value.Y;
        _pollPass = valuesDictionary.GetValue<int>("PollPass");
        _pollableBehaviorsByContents = new SubsystemPollableBlockBehavior[BlocksManager.Blocks.Length][];
        for (var i = 0; i < _pollableBehaviorsByContents.Length; i++)
        {
            _pollableBehaviorsByContents[i] = (
                from s in _subsystemBlockBehaviors.GetBlockBehaviors(i)
                where s is SubsystemPollableBlockBehavior
                select (SubsystemPollableBlockBehavior)s
            ).ToArray();
        }
    }

    public override void Save(ValuesDictionary valuesDictionary)
    {
        valuesDictionary.SetValue("PollChunkCoordinates", _pollChunkCoordinates);
        valuesDictionary.SetValue("PollPoint", new Point2(_pollX, _pollZ));
        valuesDictionary.SetValue("PollPass", _pollPass);
    }
}
