using Engine.Core.Mathematics;
using Engine.Graphics;

using Game.Blocks.Presentation;
using Game.Blocks.Presentation.Geometry;
using Game.Managers;
using Game.Modding.Blocks;
using Game.Terrains;

namespace TerritoryStoneMod.Client.Presentation;

/// <summary>使用界碑模型与专属贴图绘制地形和物品。</summary>
public sealed class TerritoryBlockPresentation(TerritoryBlock block, BlockPresentationCatalog catalog)
    : BlockPresentation(block, catalog)
{
    private readonly BlockMesh _placed = new();

    private readonly BlockMesh _item = new();

    private Texture2D _texture = null!;

    public override void Dispose()
    {
        _texture?.Dispose();
        base.Dispose();
    }

    public override void Initialize()
    {
        base.Initialize();
        using var stream =
            typeof(TerritoryBlockPresentation).Assembly.GetManifestResourceStream(
                "TerritoryStoneMod.Client.Resources.Models.TerritoryStone.glb"
            ) ??
            throw new InvalidOperationException("Missing territory stone model.");
        using var model = Model.Load(stream);
        foreach (var node in model.MeshNodes)
        {
            foreach (var primitive in node.RequireMesh().Primitives)
            {
                _placed.AppendModelPrimitive(
                    primitive,
                    BlockMesh.GetNodeAbsoluteTransform(node),
                    false,
                    false,
                    false,
                    false,
                    Color.White
                );
            }
        }

        using var textureStream =
            typeof(TerritoryBlockPresentation).Assembly.GetManifestResourceStream(
                "TerritoryStoneMod.Client.Resources.Models.TerritoryStone.png"
            ) ??
            throw new InvalidOperationException("Missing territory stone texture.");
        _texture = Texture2D.Load(textureStream);

        _item.AppendBlockMesh(_placed);
        _item.TransformPositions(Matrix.CreateTranslation(0, -0.49f, 0));
        _placed.TransformPositions(Matrix.CreateTranslation(0.5f, 0, 0.5f));
    }

    public override void GenerateTerrainVertices(
        BlockGeometryGenerator generator,
        TerrainGeometry geometry,
        int value,
        int x,
        int y,
        int z
    ) =>
        generator.GenerateMeshVertices(
            this,
            x,
            y,
            z,
            _placed,
            Color.White,
            null,
            geometry.GetGeometry(_texture).SubsetOpaque
        );

    public override void DrawBlock(
        PrimitivesRenderer3D renderer,
        int value,
        Color color,
        float size,
        ref Matrix matrix,
        DrawBlockEnvironmentData environmentData
    ) =>
        BlocksManager.DrawMeshBlock(renderer, _item, _texture, color, size, ref matrix, environmentData);
}
