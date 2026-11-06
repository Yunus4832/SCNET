using Engine.Core.Mathematics;
using Engine.Graphics;

using Game.Blocks;
using Game.Blocks.Presentation;
using Game.Blocks.Presentation.Geometry;
using Game.Managers;
using Game.Modding.Blocks;
using Game.Protocol;
using Game.Terrains;

using static BeaconMod.BeaconBlock;

namespace BeaconMod.Client.Presentation;

public sealed class BeaconBlockPresentation(BeaconBlock block, BlockPresentationCatalog catalog)
    : BlockPresentation(block, catalog)
{
    private readonly BlockMesh _baseMesh = new();

    private readonly BlockMesh _glassMesh = new();

    private readonly BlockMesh[] _cores = new BlockMesh[16];

    private readonly BlockMesh[] _standalone = new BlockMesh[16];

    public override void Initialize()
    {
        base.Initialize();

        using var stream =
            typeof(BeaconClientContentModule).Assembly.GetManifestResourceStream(
                "BeaconMod.Client.Resources.Models.Beacon.glb"
            ) ??
            throw new InvalidOperationException("Missing client beacon model resource.");
        using var model = Model.Load(stream);
        var clay = (ClayBlockPresentation)Catalog.Get(ResourceId.Parse("game:clay"));
        var baseSlot = clay.GetFaceTextureSlot(0, 0);
        var emissiveSlot = clay.ColoredTextureSlot;
        // Use the existing glass tile, including its transparent interior and edge highlights.
        var glassSlot = Catalog.Get(ResourceId.Parse("game:glass")).GetFaceTextureSlot(0, 0);
        AppendPart(_baseMesh, model, "base", new Color(85, 90, 100), false, baseSlot, false);
        AppendPart(_glassMesh, model, "glass", Color.White, false, glassSlot, true);
        for (var data = 0; data < 16; data++)
        {
            var core = new BlockMesh();
            AppendPart(
                core,
                model,
                "core",
                (data & 8) != 0 ? BeaconPalette.GetEmitterColor(data) : new Color(65, 75, 80),
                (data & 8) != 0,
                emissiveSlot,
                false
            );
            _cores[data] = core;
            var standalone = new BlockMesh();
            standalone.AppendBlockMesh(_baseMesh);
            standalone.AppendBlockMesh(core);
            standalone.AppendBlockMesh(_glassMesh);
            standalone.TransformPositions(Matrix.CreateTranslation(-0.5f, -0.25f, -0.5f));
            _standalone[data] = standalone;
        }
    }

    private static void AppendPart(
        BlockMesh target,
        Model model,
        string name,
        Color color,
        bool emissive,
        int slot,
        bool glass
    )
    {
        var node = model.FindNode(name)!;
        foreach (var part in node.RequireMesh().Primitives)
        {
            target.AppendModelPrimitive(
                part,
                BlockMesh.GetNodeAbsoluteTransform(node),
                emissive,
                false,
                glass,
                false,
                color
            );
        }

        var bounds = target.CalculateBoundingBox();
        for (var i = 0; i < target.Vertices.Count; i++)
        {
            var vertex = target.Vertices.Array[i];
            var p = (vertex.Position - bounds.Min) / (bounds.Max - bounds.Min);
            var uv = vertex.Face switch
            {
                0 or 2 => new Vector2(p.X, 1 - p.Y),
                1 or 3 => new Vector2(p.Z, 1 - p.Y),
                _ => new Vector2(p.X, p.Z)
            };
            // A flat white texel for solid parts; full glass tile for the cover.
            if (!glass)
            {
                uv = new Vector2(0.5f);
            }

            vertex.TextureCoordinates = (new Vector2(slot % 16, slot / 16) +
                                         new Vector2(0.01f) +
                                         uv * 0.98f) /
                                        16f;
            vertex.Position += new Vector3(0.5f, 0, 0.5f);
            target.Vertices.Array[i] = vertex;
        }
    }

    public override void GenerateTerrainVertices(
        BlockGeometryGenerator generator,
        TerrainGeometry geometry,
        int value,
        int x,
        int y,
        int z
    )
    {
        var data = Terrain.ExtractData(value);
        var meshIndex = (data & 7) | (IsEnabled(data) ? 8 : 0);
        generator.GenerateMeshVertices(this, x, y, z, _baseMesh, Color.White, null, geometry.SubsetOpaque);
        var brightness = IsEnabled(data) ? GetIntensity(data) / 15f : 1f;
        generator.GenerateMeshVertices(
            this,
            x,
            y,
            z,
            _cores[meshIndex],
            Color.MultiplyColorOnly(Color.White, brightness),
            null,
            geometry.SubsetOpaque
        );
        generator.GenerateWireVertices(value, x, y, z, 4, 0.01f, Vector2.Zero, geometry.SubsetOpaque);
        generator.GenerateMeshVertices(this, x, y, z, _glassMesh, Color.White, null, geometry.SubsetAlphaTest);
    }

    public override void DrawBlock(
        PrimitivesRenderer3D renderer,
        int value,
        Color color,
        float size,
        ref Matrix matrix,
        DrawBlockEnvironmentData environmentData
    ) => BlocksManager.DrawMeshBlock(
        renderer,
        _standalone[0],
        color,
        size,
        ref matrix,
        environmentData
    );
}
