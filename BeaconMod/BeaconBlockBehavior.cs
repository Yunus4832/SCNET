using Engine.Core.Mathematics;
using Engine.Core.Runtime;
using Engine.Graphics;

using Game.Cameras;
using Game.Modding;
using Game.Protocol;
using Game.Rendering;
using Game.Subsystems;
using Game.Terrains;

namespace BeaconMod;

public sealed class BeaconBlockBehavior : SubsystemBlockBehavior, IDrawable
{
    private readonly HashSet<Point3> _beacons = [];

    private readonly PrimitivesRenderer3D _renderer = new();

    public override ResourceId[] HandledBlocks => [BeaconBlock.Id];

    public int[] DrawOrders => [101];

    public override void OnBlockGenerated(int value, int x, int y, int z, bool isLoaded) => Track(x, y, z);
    public override void OnBlockAdded(int value, int oldValue, int x, int y, int z) => Track(x, y, z);

    public override void OnBlockRemoved(int value, int newValue, int x, int y, int z) =>
        _beacons.Remove(new Point3(x, y, z));

    private void Track(int x, int y, int z)
    {
        if (RunMode.Value == RunModeType.Gui)
        {
            _beacons.Add(new Point3(x, y, z));
        }
    }

    public override void OnChunkDiscarding(TerrainChunk chunk) =>
        _beacons.RemoveWhere(point => (point.X >> 4) == chunk.Coords.X && (point.Z >> 4) == chunk.Coords.Y);

    public void Draw(Camera camera, int drawOrder)
    {
        var catalog = CurrentModRuntime.Value!.RequireWorldCatalog();
        var index = catalog.GetBlock(BeaconBlock.Id).BlockIndex;
        var batch = _renderer.FlatBatch(
            0,
            DepthStencilState.DepthRead,
            RasterizerState.CullNone,
            BlendState.NonPremultiplied
        );
        foreach (var point in _beacons)
        {
            var value = SubsystemTerrain.Terrain.GetCellValue(point.X, point.Y, point.Z);
            var data = Terrain.ExtractData(value);
            if (Terrain.ExtractContents(value) != index || !BeaconBlock.IsEnabled(data))
            {
                continue;
            }

            var start = new Vector3(point.X + 0.5f, point.Y + 0.1f, point.Z + 0.5f);
            if (Vector2.DistanceSquared(
                    new Vector2(start.X, start.Z),
                    new Vector2(camera.ViewPosition.X, camera.ViewPosition.Z)
                ) >
                512 * 512)
            {
                continue;
            }

            var top = 1024;
            for (var y = point.Y + 1; y < 256; y++)
            {
                var above = SubsystemTerrain.Terrain.GetCellValue(point.X, y, point.Z);
                if (!catalog.Blocks[Terrain.ExtractContents(above)].IsTransparent(above))
                {
                    top = y;
                    break;
                }
            }

            if (top <= start.Y ||
                !camera.ViewFrustum.Intersection(
                    new BoundingBox(
                        start - new Vector3(0.24f, 0, 0.24f),
                        new Vector3(start.X + 0.24f, top, start.Z + 0.24f)
                    )
                ))
            {
                continue;
            }

            var color = BeaconBlock.GetColor(data);
            var brightness = BeaconBlock.GetIntensity(data) / 15f;
            // Alpha compositing preserves the chosen hue instead of saturating overlapping layers to white.
            AddColumn(batch, start, top, 0.24f, new Color(color.R, color.G, color.B, (byte)(115 * brightness)));
            // The luminous center matches the near-white emitter while the outer column carries the hue.
            var emitter = BeaconBlock.GetEmitterColor(data);
            var coreColor = new Color(emitter.R, emitter.G, emitter.B, (byte)(230 * brightness));
            AddColumn(batch, start, top, 0.13f, coreColor);
        }

        // Camera-relative vertices retain precision far from the world's origin.
        _renderer.Flush(Matrix.CreateTranslation(camera.ViewPosition) * camera.ViewProjectionMatrix);

        void AddColumn(FlatBatch3D target, Vector3 start, float top, float radius, Color color)
        {
            var center = start - camera.ViewPosition;
            var height = top - start.Y;
            const int sides = 20;
            for (var side = 0; side < sides; side++)
            {
                var a = side * (2 * MathUtils.PI) / sides;
                var b = (side + 1) * (2 * MathUtils.PI) / sides;
                var p = center + new Vector3(MathUtils.Cos(a) * radius, 0, MathUtils.Sin(a) * radius);
                var q = center + new Vector3(MathUtils.Cos(b) * radius, 0, MathUtils.Sin(b) * radius);
                // Match the hemisphere's equator: every point on the emitter's outline projects upward.
                target.QueueQuad(
                    p,
                    q,
                    q + new Vector3(0, height, 0),
                    p + new Vector3(0, height, 0),
                    color
                );
            }
        }
    }

    public override void Dispose() => _beacons.Clear();
}
