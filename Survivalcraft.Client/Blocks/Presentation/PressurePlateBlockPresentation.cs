using Engine.Graphics;

using Game.Modding.Blocks;

using static Game.Blocks.PressurePlateBlock;

namespace Game.Blocks.Presentation;

public class PressurePlateBlockPresentation(PressurePlateBlock block, BlockPresentationCatalog catalog)
    : MountedElectricElementBlockPresentation(block, catalog)
{
    protected new PressurePlateBlock Block => (PressurePlateBlock)base.Block;

    public readonly BlockMesh[] BlockMeshesByData = new BlockMesh[16];

    public readonly BlockMesh[] StandaloneBlockMeshesByMaterial = new BlockMesh[2];

    public override string GetDisplayName(SubsystemTerrain? subsystemTerrain, int value)
    {
        var material = GetMaterial(Terrain.ExtractData(value));
        return Block.DisplayNamesByMaterial[material];
    }

    public override BlockDebrisParticleSystem? CreateDebrisParticleSystem(
        SubsystemTerrain subsystemTerrain,
        Vector3 position,
        int value,
        float strength
    )
    {
        if (DestructionDebrisScale <= 0f) { return null; }

        var material = GetMaterial(Terrain.ExtractData(value));
        return new BlockDebrisParticleSystem(
            subsystemTerrain,
            position,
            strength,
            DestructionDebrisScale,
            Color.White,
            TextureSlotsByMaterial[material]
        );
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
        var num = Terrain.ExtractData(value);
        if (num >= BlockMeshesByData.Length || BlockMeshesByData[num] == null)
        {
            return;
        }

        generator.GenerateMeshVertices(
            this,
            x,
            y,
            z,
            BlockMeshesByData[num],
            Color.White,
            null,
            geometry.SubsetOpaque
        );
        generator.GenerateWireVertices(
            value,
            x,
            y,
            z,
            Block.GetFace(value),
            0.8125f,
            Vector2.Zero,
            geometry.SubsetOpaque
        );
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
        var material = GetMaterial(Terrain.ExtractData(value));
        BlocksManager.DrawMeshBlock(
            primitivesRenderer,
            StandaloneBlockMeshesByMaterial[material],
            color,
            2f * size,
            ref matrix,
            environmentData
        );
    }

    public readonly int[] TextureSlotsByMaterial =
    [
        4,
        1
    ];

    public override void Initialize()
    {
        var model = ContentManager.Get<Model>("Models/PressurePlate");
        for (var i = 0; i < 2; i++)
        {
            var pressurePlateNode = model.FindNode("PressurePlate")!;
            var nodeAbsoluteTransform = BlockMesh.GetNodeAbsoluteTransform(pressurePlateNode);
            var num = TextureSlotsByMaterial[i];
            for (var j = 0; j < 6; j++)
            {
                var num2 = SetMountingFace(SetMaterial(0, i), j);
                var matrix = j >= 4
                    ? j != 4
                        ? Matrix.CreateRotationX((float)Math.PI) * Matrix.CreateTranslation(0.5f, 1f, 0.5f)
                        : Matrix.CreateTranslation(0.5f, 0f, 0.5f)
                    : Matrix.CreateRotationX((float)Math.PI / 2f) *
                      Matrix.CreateTranslation(0f, 0f, -0.5f) *
                      Matrix.CreateRotationY(j * (float)Math.PI / 2f) *
                      Matrix.CreateTranslation(0.5f, 0.5f, 0.5f);
                BlockMeshesByData[num2] = new BlockMesh();
                BlockMeshesByData[num2].AppendModelPrimitive(
                    pressurePlateNode.RequireMesh().Primitives[0],
                    nodeAbsoluteTransform * matrix,
                    false,
                    false,
                    false,
                    false,
                    Color.White
                );
                BlockMeshesByData[num2]
                    .TransformTextureCoordinates(Matrix.CreateTranslation(num % 16 / 16f, num / 16 / 16f, 0f));
                BlockMeshesByData[num2].GenerateSidesData();
            }

            var identity = Matrix.Identity;
            StandaloneBlockMeshesByMaterial[i] = new BlockMesh();
            StandaloneBlockMeshesByMaterial[i].AppendModelPrimitive(
                pressurePlateNode.RequireMesh().Primitives[0],
                nodeAbsoluteTransform * identity,
                false,
                false,
                false,
                false,
                Color.White
            );
            StandaloneBlockMeshesByMaterial[i]
                .TransformTextureCoordinates(Matrix.CreateTranslation(num % 16 / 16f, num / 16 / 16f, 0f));
        }
    }
}
