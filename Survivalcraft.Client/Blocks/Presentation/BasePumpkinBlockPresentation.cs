using Engine.Graphics;

using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public abstract class BasePumpkinBlockPresentation(BasePumpkinBlock block, BlockPresentationCatalog catalog)
    : BlockPresentation(block, catalog)
{
    protected new BasePumpkinBlock Block => (BasePumpkinBlock)base.Block;

    public BlockMesh[] BlockMeshesBySize = new BlockMesh[8];

    public BlockMesh[] StandaloneBlockMeshesBySize = new BlockMesh[8];

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
        var size = BasePumpkinBlock.GetSize(data);
        var isDead = BasePumpkinBlock.GetIsDead(data);
        if (size >= 1)
        {
            generator.GenerateMeshVertices(
                this,
                x,
                y,
                z,
                BlockMeshesBySize[size],
                Color.White,
                null,
                geometry.SubsetOpaque
            );
        }

        if (size == 0)
        {
            generator.GenerateCrossingFaceVertices(
                this,
                value,
                x,
                y,
                z,
                new Color(160, 160, 160),
                11,
                geometry.SubsetAlphaTest
            );
        }
        else if (size < 7 && !isDead)
        {
            generator.GenerateCrossingFaceVertices(this, value, x, y, z, Color.White, 28, geometry.SubsetAlphaTest);
        }
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
        var size2 = BasePumpkinBlock.GetSize(Terrain.ExtractData(value));
        BlocksManager.DrawMeshBlock(
            primitivesRenderer,
            StandaloneBlockMeshesBySize[size2],
            color,
            2f * size,
            ref matrix,
            environmentData
        );
    }

    public override int GetShadowStrength(int value)
    {
        return BasePumpkinBlock.GetSize(Terrain.ExtractData(value));
    }

    public override BlockDebrisParticleSystem? CreateDebrisParticleSystem(
        SubsystemTerrain subsystemTerrain,
        Vector3 position,
        int value,
        float strength
    )
    {
        if (DestructionDebrisScale <= 0f) { return null; }

        var size = BasePumpkinBlock.GetSize(Terrain.ExtractData(value));
        var num = MathUtils.Lerp(0.2f, 1f, size / 7f);
        var color = size == 7 ? Color.White : new Color(0, 128, 128);
        return new BlockDebrisParticleSystem(
            subsystemTerrain,
            position,
            1.5f * strength,
            DestructionDebrisScale * num,
            color,
            TextureSlot
        );
    }

    public override string GetDisplayName(SubsystemTerrain? subsystemTerrain, int value)
    {
        var size = BasePumpkinBlock.GetSize(Terrain.ExtractData(value));
        if (Block.IsRotten)
        {
            return size >= 7
                ? string.Format(
                    Localization.ClientLanguage.Catalog.Get("BasePumpkinBlock", 2),
                    Localization.ClientLanguage.Catalog.Get("BasePumpkinBlock", 4)
                )
                : string.Format(
                    Localization.ClientLanguage.Catalog.Get("BasePumpkinBlock", 3),
                    Localization.ClientLanguage.Catalog.Get("BasePumpkinBlock", 4),
                    Localization.ClientLanguage.Catalog.Get("BasePumpkinBlock", 5)
                );
        }

        return size >= 7
            ? Localization.ClientLanguage.Catalog.Get("BasePumpkinBlock", 1)
            : string.Format(
                Localization.ClientLanguage.Catalog.Get("BasePumpkinBlock", 2),
                Localization.ClientLanguage.Catalog.Get("BasePumpkinBlock", 5)
            );
    }

    public override void Initialize()
    {
        var model = ContentManager.Get<Model>("Models/Pumpkins");
        var pumpkinNode = model.FindNode("Pumpkin")!;
        var nodeAbsoluteTransform = BlockMesh.GetNodeAbsoluteTransform(pumpkinNode);
        for (var i = 0; i < 8; i++)
        {
            var num = MathUtils.Lerp(0.2f, 1f, i / 7f);
            var num2 = MathUtils.Min(0.3f * num, 0.7f * (1f - num));
            Color color;
            if (Block.IsRotten)
            {
                color = Color.White;
            }
            else
            {
                color = Color.Lerp(new Color(0, 128, 128), new Color(80, 255, 255), i / 7f);
                if (i == 7)
                {
                    color.R = byte.MaxValue;
                }
            }

            BlockMeshesBySize[i] = new BlockMesh();
            if (i >= 1)
            {
                BlockMeshesBySize[i].AppendModelPrimitive(
                    pumpkinNode.RequireMesh().Primitives[0],
                    nodeAbsoluteTransform *
                    Matrix.CreateScale(num) *
                    Matrix.CreateTranslation(0.5f + num2, 0f, 0.5f + num2),
                    false,
                    false,
                    false,
                    false,
                    color
                );
            }

            if (Block.IsRotten)
            {
                BlockMeshesBySize[i].TransformTextureCoordinates(Matrix.CreateTranslation(-0.375f, 0.25f, 0f));
            }

            StandaloneBlockMeshesBySize[i] = new BlockMesh();
            StandaloneBlockMeshesBySize[i].AppendModelPrimitive(
                pumpkinNode.RequireMesh().Primitives[0],
                nodeAbsoluteTransform * Matrix.CreateScale(num) * Matrix.CreateTranslation(0f, -0.23f, 0f),
                false,
                false,
                false,
                false,
                color
            );
        }

        base.Initialize();
    }
}
