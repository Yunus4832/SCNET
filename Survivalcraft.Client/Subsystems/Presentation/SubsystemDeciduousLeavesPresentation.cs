using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Subsystems;

/// <summary>Client-owned ambient leaf scheduling. Shaking particles remain in the block dig presentation.</summary>
public sealed class SubsystemDeciduousLeavesPresentation : Subsystem, IUpdateable
{
    public UpdateExecutionSide UpdateSide => UpdateExecutionSide.Presentation;

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    private DynamicArray<LeafParticles> _leafParticles = [];

    private DynamicArray<LeafParticles> _tmpLeafParticles = [];

    private readonly Random _random = new();

    private SubsystemDeciduousLeavesBlockBehavior _rules = null!;

    private SubsystemGameWidgets _subsystemGameWidgets = null!;

    private SubsystemParticles _subsystemParticles = null!;

    private SubsystemTerrain _subsystemTerrain = null!;

    private SubsystemTime _subsystemTime = null!;

    private bool _disposed;

    public override void Load(ValuesDictionary valuesDictionary)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _rules = Project.FindSubsystem<SubsystemDeciduousLeavesBlockBehavior>(true)!;
        _subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true)!;
        _subsystemTime = Project.FindSubsystem<SubsystemTime>(true)!;
        _subsystemGameWidgets = Project.FindSubsystem<SubsystemGameWidgets>(true)!;
        _subsystemParticles = Project.FindSubsystem<SubsystemParticles>(true)!;
        _rules.LeafCellVisited += QueueLeafParticles;
    }

    public void Update(float dt)
    {
        if (_disposed || !WorldRuntimeContext.IsProjectActive(Project)) { return; }

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
                    var cellValue = _subsystemTerrain.Terrain.GetCellValue(
                        leafParticle.Position.X,
                        leafParticle.Position.Y,
                        leafParticle.Position.Z
                    );
                    var num = Terrain.ExtractContents(cellValue);
                    if (Modding.Blocks.WorldContentBinding.For(Project).Catalog.Blocks[num] is DeciduousLeavesBlock
                            deciduousLeavesBlock &&
                        deciduousLeavesBlock.GetLeafDropProbability(cellValue) > 0f)
                    {
                        _subsystemParticles.AddParticleSystem(
                            new LeavesParticleSystem(
                                _subsystemTerrain,
                                leafParticle.Position,
                                _random.Int(1, 2),
                                true,
                                cellValue
                            )
                        );
                    }
                }
            }
            else
            {
                _tmpLeafParticles.Add(leafParticle);
            }
        }

        (_leafParticles, _tmpLeafParticles) = (_tmpLeafParticles, _leafParticles);
        _tmpLeafParticles.Clear();
    }

    private void QueueLeafParticles(int value, Point3 point)
    {
        if (_disposed || !WorldRuntimeContext.IsProjectActive(Project)) { return; }

        var deciduousLeavesBlock =
            (DeciduousLeavesBlock)Modding.Blocks.WorldContentBinding.For(Project).Catalog
                .Blocks[Terrain.ExtractContents(value)];
        if (_leafParticles.Count < 30000 &&
            _random.Bool(deciduousLeavesBlock.GetLeafDropProbability(value) / 60f * 60f) &&
            _subsystemGameWidgets.CalculateDistanceFromNearestView(new Vector3(point)) < 128f)
        {
            _leafParticles.Add(
                new LeafParticles
                {
                    Position = point,
                    Time = _subsystemTime.GameTime + _random.Float(0f, 60f)
                }
            );
        }
    }

    public override void Dispose()
    {
        if (_disposed) { return; }

        _disposed = true;
        if (_rules is not null) { _rules.LeafCellVisited -= QueueLeafParticles; }

        _leafParticles.Clear();
        _tmpLeafParticles.Clear();
    }

    private struct LeafParticles
    {
        public double Time;

        public Point3 Position;
    }
}
