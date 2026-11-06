using Engine.Core.Mathematics;
using Engine.Graphics;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Cameras;
using Game.Rendering;
using Game.Subsystems;
using Game.Terrains;
using Game.World;

namespace BeaconMod.Client.Presentation;

public sealed class BeaconPresentation : Subsystem, IDrawable
{
    private BeaconBlockBehavior _behavior = null!;

    private SubsystemTerrain _terrain = null!;

    private SubsystemAudio _audio = null!;

    private bool _subscribed;

    private bool _disposed;

    private PrimitivesRenderer3D Renderer => field ??= new();

    public int[] DrawOrders => [101];

    public override void Load(ValuesDictionary valuesDictionary)
    {
        if (!WorldRuntimeContext.For(Project).HasPresentation)
        {
            throw new InvalidOperationException("Beacon presentation requires a client presentation world.");
        }

        _behavior = Project.FindSubsystem<BeaconBlockBehavior>(true)!;
        _terrain = Project.FindSubsystem<SubsystemTerrain>(true)!;
        _audio = Project.FindSubsystem<SubsystemAudio>(true)!;
        Subscribe();
    }

    private void Subscribe()
    {
        if (_subscribed) { return; }

        _behavior.InteractionPerformed += OnInteraction;
        _subscribed = true;
    }

    private void OnInteraction(Point3 point)
    {
        if (_disposed || !WorldRuntimeContext.IsProjectActive(Project)) { return; }

        _audio.PlaySound("Audio/Click", 1, 0, new Vector3(point), 2, true);
    }

    public void Draw(Camera camera, int drawOrder)
    {
        if (_disposed || !WorldRuntimeContext.IsProjectActive(Project)) { return; }

        var catalog = _terrain.Catalog;
        var index = catalog.GetBlock(BeaconBlock.Id).BlockIndex;
        var batch = Renderer.FlatBatch(
            0,
            DepthStencilState.DepthRead,
            RasterizerState.CullNone,
            BlendState.NonPremultiplied
        );
        foreach (var point in _behavior.Beacons)
        {
            var value = _terrain.Terrain.GetCellValue(point.X, point.Y, point.Z);
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
                var above = _terrain.Terrain.GetCellValue(point.X, y, point.Z);
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

            var color = BeaconPalette.GetColor(data);
            var brightness = BeaconBlock.GetIntensity(data) / 15f;
            // Alpha compositing preserves the chosen hue instead of saturating overlapping layers to white.
            AddColumn(batch, start, top, 0.24f, new Color(color.R, color.G, color.B, (byte)(115 * brightness)));
            // The luminous center matches the near-white emitter while the outer column carries the hue.
            var emitter = BeaconPalette.GetEmitterColor(data);
            var coreColor = new Color(emitter.R, emitter.G, emitter.B, (byte)(230 * brightness));
            AddColumn(batch, start, top, 0.13f, coreColor);
        }

        // Camera-relative vertices retain precision far from the world's origin.
        Renderer.Flush(Matrix.CreateTranslation(camera.ViewPosition) * camera.ViewProjectionMatrix);

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

    public override void Dispose()
    {
        if (_disposed) { return; }

        _disposed = true;
        if (_subscribed)
        {
            _behavior.InteractionPerformed -= OnInteraction;
            _subscribed = false;
        }
    }
}
