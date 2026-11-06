using Engine.Core.Mathematics;

using Game.Protocol;
using Game.Subsystems;
using Game.Subsystems.Behavior;
using Game.Terrains;

namespace BeaconMod;

public sealed class BeaconBlockBehavior : SubsystemBlockBehavior
{
    private readonly HashSet<Point3> _beacons = [];

    public override ResourceId[] HandledBlocks => [BeaconBlock.Id];

    public IEnumerable<Point3> Beacons
    {
        get
        {
            foreach (var point in _beacons) { yield return point; }
        }
    }

    public event Action<Point3>? InteractionPerformed;

    public override void OnBlockGenerated(int value, int x, int y, int z, bool isLoaded) => Track(x, y, z);

    public override void OnBlockAdded(int value, int oldValue, int x, int y, int z) => Track(x, y, z);

    public override void OnBlockRemoved(int value, int newValue, int x, int y, int z) =>
        _beacons.Remove(new Point3(x, y, z));

    private void Track(int x, int y, int z) => _beacons.Add(new Point3(x, y, z));

    public override void OnChunkDiscarding(TerrainChunk chunk) =>
        _beacons.RemoveWhere(point => (point.X >> 4) == chunk.Coords.X && (point.Z >> 4) == chunk.Coords.Y);

    public void NotifyInteraction(Point3 point) => InteractionPerformed?.Invoke(point);

    public override void Dispose() => _beacons.Clear();
}
