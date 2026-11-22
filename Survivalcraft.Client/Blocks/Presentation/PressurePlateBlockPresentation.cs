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
        return Localization.ClientLanguage.Catalog.Get(nameof(PressurePlateBlock), material);
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
        var meshIndex = Terrain.ExtractData(value);
        if (meshIndex >= BlockMeshesByData.Length || BlockMeshesByData[meshIndex] == null)
        {
            return;
        }

        generator.GenerateMeshVertices(
            this,
            x,
            y,
            z,
            BlockMeshesByData[meshIndex],
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
            var materialTextureSlot = TextureSlotsByMaterial[i];
            for (var j = 0; j < 6; j++)
            {
                var meshIndex = SetMountingFace(SetMaterial(0, i), j);
                var matrix = j >= 4
                    ? j != 4
                        ? Matrix.CreateRotationX((float)Math.PI) * Matrix.CreateTranslation(0.5f, 1f, 0.5f)
                        : Matrix.CreateTranslation(0.5f, 0f, 0.5f)
                    : Matrix.CreateRotationX((float)Math.PI / 2f) *
                      Matrix.CreateTranslation(0f, 0f, -0.5f) *
                      Matrix.CreateRotationY(j * (float)Math.PI / 2f) *
                      Matrix.CreateTranslation(0.5f, 0.5f, 0.5f);
                BlockMeshesByData[meshIndex] = new BlockMesh();
                BlockMeshesByData[meshIndex].AppendModelPrimitive(
                    pressurePlateNode.RequireMesh().Primitives[0],
                    nodeAbsoluteTransform * matrix,
                    false,
                    false,
                    false,
                    false,
                    Color.White
                );
                BlockMeshesByData[meshIndex]
                    .TransformTextureCoordinates(
                        Matrix.CreateTranslation(materialTextureSlot % 16 / 16f, materialTextureSlot / 16 / 16f, 0f)
                    );
                BlockMeshesByData[meshIndex].GenerateSidesData();
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
                .TransformTextureCoordinates(
                    Matrix.CreateTranslation(materialTextureSlot % 16 / 16f, materialTextureSlot / 16 / 16f, 0f)
                );
        }
    }
}
