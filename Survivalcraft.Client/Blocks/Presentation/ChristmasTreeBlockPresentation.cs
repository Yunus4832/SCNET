using Engine.Graphics;

using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class ChristmasTreeBlockPresentation(ChristmasTreeBlock block, BlockPresentationCatalog catalog)
    : BlockPresentation(block, catalog)
{
    protected new ChristmasTreeBlock Block => (ChristmasTreeBlock)base.Block;

    public BlockMesh DecorationsBlockMesh = new();

    public BlockMesh LeavesBlockMesh = new();

    public BlockMesh LitDecorationsBlockMesh = new();

    public BlockMesh StandaloneBlockMesh = new();

    public BlockMesh StandTrunkBlockMesh = new();

    public override void GenerateTerrainVertices(
        BlockGeometryGenerator generator,
        TerrainGeometry geometry,
        int value,
        int x,
        int y,
        int z
    )
    {
        var color = BlockColorsMap.SpruceLeavesColorsMap.Lookup(generator.Terrain, x, y, z);
        if (ChristmasTreeBlock.GetLightState(Terrain.ExtractData(value)))
        {
            generator.GenerateMeshVertices(
                this,
                x,
                y,
                z,
                StandTrunkBlockMesh,
                Color.White,
                null,
                geometry.SubsetOpaque
            );
            generator.GenerateMeshVertices(
                this,
                x,
                y,
                z,
                LitDecorationsBlockMesh,
                Color.White,
                null,
                geometry.SubsetOpaque
            );
            generator.GenerateMeshVertices(this, x, y, z, LeavesBlockMesh, color, null, geometry.SubsetAlphaTest);
        }
        else
        {
            generator.GenerateMeshVertices(
                this,
                x,
                y,
                z,
                StandTrunkBlockMesh,
                Color.White,
                null,
                geometry.SubsetOpaque
            );
            generator.GenerateMeshVertices(
                this,
                x,
                y,
                z,
                DecorationsBlockMesh,
                Color.White,
                null,
                geometry.SubsetOpaque
            );
            generator.GenerateMeshVertices(this, x, y, z, LeavesBlockMesh, color, null, geometry.SubsetAlphaTest);
        }

        generator.GenerateWireVertices(value, x, y, z, 4, 0.01f, Vector2.Zero, geometry.SubsetOpaque);
    }

    public override void DrawBlock(
        PrimitivesRenderer3D primitivesRenderer,
        int value,
        Color color,
        float size,
        ref Matrix matrix,
        DrawBlockEnvironmentData environmentData
    )
    {
        BlocksManager.DrawMeshBlock(
            primitivesRenderer,
            StandaloneBlockMesh,
            color,
            size,
            ref matrix,
            environmentData
        );
    }

    public override BlockDebrisParticleSystem? CreateDebrisParticleSystem(
        SubsystemTerrain subsystemTerrain,
        Vector3 position,
        int value,
        float strength
    )
    {
        if (DestructionDebrisScale <= 0f) { return null; }

        var color = BlockColorsMap.SpruceLeavesColorsMap.Lookup(
            subsystemTerrain.Terrain,
            Terrain.ToCell(position.X),
            Terrain.ToCell(position.Y),
            Terrain.ToCell(position.Z)
        );
        return new BlockDebrisParticleSystem(
            subsystemTerrain,
            position,
            strength,
            DestructionDebrisScale,
            color,
            TextureSlot
        );
    }

    public override int GetShadowStrength(int value)
    {
        if (!ChristmasTreeBlock.GetLightState(Terrain.ExtractData(value)))
        {
            return ShadowStrength;
        }

        return -99;
    }

    public override void Initialize()
    {
        var model = ContentManager.Get<Model>("Models/ChristmasTree");
        var standTrunkNode = model.FindNode("StandTrunk")!;
        var leavesNode = model.FindNode("Leaves")!;
        var nodeAbsoluteTransform = BlockMesh.GetNodeAbsoluteTransform(standTrunkNode);
        var nodeAbsoluteTransform2 = BlockMesh.GetNodeAbsoluteTransform(leavesNode);
        var decorationsNode = model.FindNode("Decorations")!;
        var nodeAbsoluteTransform3 = BlockMesh.GetNodeAbsoluteTransform(decorationsNode);
        var color = BlockColorsMap.SpruceLeavesColorsMap.Lookup(4, 15);
        LeavesBlockMesh.AppendModelPrimitive(
            leavesNode.RequireMesh().Primitives[0],
            nodeAbsoluteTransform2 * Matrix.CreateTranslation(0.5f, 0f, 0.5f),
            false,
            false,
            true,
            false,
            Color.White
        );
        StandTrunkBlockMesh.AppendModelPrimitive(
            standTrunkNode.RequireMesh().Primitives[0],
            nodeAbsoluteTransform * Matrix.CreateTranslation(0.5f, 0f, 0.5f),
            false,
            false,
            false,
            false,
            Color.White
        );
        DecorationsBlockMesh.AppendModelPrimitive(
            decorationsNode.RequireMesh().Primitives[0],
            nodeAbsoluteTransform3 * Matrix.CreateTranslation(0.5f, 0f, 0.5f),
            false,
            false,
            false,
            false,
            Color.White
        );
        LitDecorationsBlockMesh.AppendModelPrimitive(
            decorationsNode.RequireMesh().Primitives[0],
            nodeAbsoluteTransform3 * Matrix.CreateTranslation(0.5f, 0f, 0.5f),
            true,
            false,
            false,
            false,
            Color.White
        );
        StandaloneBlockMesh.AppendModelPrimitive(
            standTrunkNode.RequireMesh().Primitives[0],
            nodeAbsoluteTransform * Matrix.CreateTranslation(0f, -1f, 0f),
            false,
            false,
            false,
            false,
            Color.White
        );
        StandaloneBlockMesh.AppendModelPrimitive(
            leavesNode.RequireMesh().Primitives[0],
            nodeAbsoluteTransform2 * Matrix.CreateTranslation(0f, -1f, 0f),
            false,
            false,
            true,
            false,
            color
        );
        StandaloneBlockMesh.AppendModelPrimitive(
            decorationsNode.RequireMesh().Primitives[0],
            nodeAbsoluteTransform3 * Matrix.CreateTranslation(0f, -1f, 0f),
            false,
            false,
            false,
            false,
            Color.White
        );
        base.Initialize();
    }
}
