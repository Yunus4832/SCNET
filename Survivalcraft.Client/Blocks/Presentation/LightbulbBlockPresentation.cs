using Engine.Graphics;

using Game.Modding.Blocks;
using Game.World.Presentation;

namespace Game.Blocks.Presentation;

public class LightbulbBlockPresentation(LightbulbBlock block, BlockPresentationCatalog catalog)
    : MountedElectricElementBlockPresentation(block, catalog)
{
    protected new LightbulbBlock Block => (LightbulbBlock)base.Block;

    public readonly BlockMesh[] BulbBlockMeshes = new BlockMesh[6];

    public readonly BlockMesh[] BulbBlockMeshesLit = new BlockMesh[6];

    public readonly BlockMesh[] SidesBlockMeshes = new BlockMesh[6];

    public readonly BlockMesh StandaloneBulbBlockMesh = new();

    public readonly BlockMesh StandaloneSidesBlockMesh = new();

    public override string GetDisplayName(SubsystemTerrain? subsystemTerrain, int value)
    {
        var color = LightbulbBlock.GetColor(Terrain.ExtractData(value));
        return PalettePresentation.GetName(
            color,
            Localization.ClientLanguage.Catalog.Get(LightbulbBlock.TypeName, 1)
        );
    }

    public override string GetCategory(int value)
    {
        return !LightbulbBlock.GetColor(Terrain.ExtractData(value)).HasValue ? base.GetCategory(value) : "Painted";
    }

    public override int GetShadowStrength(int value)
    {
        var lightIntensity = LightbulbBlock.GetLightIntensity(Terrain.ExtractData(value));
        return ShadowStrength - 10 * lightIntensity;
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
        var mountingFace = LightbulbBlock.GetMountingFace(data);
        var lightIntensity = LightbulbBlock.GetLightIntensity(data);
        var color = LightbulbBlock.GetColor(data);
        var color2 = color.HasValue
            ? PalettePresentation.GetColor(generator.SubsystemPalette, color)
            : Block.CopperColor;
        if (mountingFace < BulbBlockMeshes.Length)
        {
            if (lightIntensity <= 0)
            {
                generator.GenerateMeshVertices(
                    this,
                    x,
                    y,
                    z,
                    BulbBlockMeshes[mountingFace],
                    Color.White,
                    null,
                    geometry.SubsetAlphaTest
                );
            }
            else
            {
                var r = (byte)(195 + lightIntensity * 4);
                var g = (byte)(180 + lightIntensity * 5);
                var b = (byte)(165 + lightIntensity * 6);
                generator.GenerateMeshVertices(
                    this,
                    x,
                    y,
                    z,
                    BulbBlockMeshesLit[mountingFace],
                    new Color(r, g, b),
                    null,
                    geometry.SubsetOpaque
                );
            }

            generator.GenerateMeshVertices(
                this,
                x,
                y,
                z,
                SidesBlockMeshes[mountingFace],
                color2,
                null,
                geometry.SubsetOpaque
            );
            generator.GenerateWireVertices(value, x, y, z, mountingFace, 0.875f, Vector2.Zero, geometry.SubsetOpaque);
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
        var color2 = LightbulbBlock.GetColor(Terrain.ExtractData(value));
        var c = color2.HasValue
            ? PalettePresentation.GetColor(environmentData.SubsystemTerrain, color2)
            : Block.CopperColor;
        BlocksManager.DrawMeshBlock(
            primitivesRenderer,
            StandaloneSidesBlockMesh,
            color * c,
            2f * size,
            ref matrix,
            environmentData
        );
        BlocksManager.DrawMeshBlock(
            primitivesRenderer,
            StandaloneBulbBlockMesh,
            color,
            2f * size,
            ref matrix,
            environmentData
        );
    }

    public override void Initialize()
    {
        var model = ContentManager.Get<Model>("Models/Lightbulbs");
        var topNode = model.FindNode("Top")!;
        var nodeAbsoluteTransform = BlockMesh.GetNodeAbsoluteTransform(topNode);
        var sidesNode = model.FindNode("Sides")!;
        var nodeAbsoluteTransform2 = BlockMesh.GetNodeAbsoluteTransform(sidesNode);
        for (var i = 0; i < 6; i++)
        {
            var m = i >= 4
                ? i != 4
                    ? Matrix.CreateRotationX((float)Math.PI) * Matrix.CreateTranslation(0.5f, 1f, 0.5f)
                    : Matrix.CreateTranslation(0.5f, 0f, 0.5f)
                : Matrix.CreateRotationX((float)Math.PI / 2f) *
                  Matrix.CreateTranslation(0f, 0f, -0.5f) *
                  Matrix.CreateRotationY(i * (float)Math.PI / 2f) *
                  Matrix.CreateTranslation(0.5f, 0.5f, 0.5f);
            BulbBlockMeshes[i] = new BlockMesh();
            BulbBlockMeshes[i].AppendModelPrimitive(
                topNode.RequireMesh().Primitives[0],
                nodeAbsoluteTransform * m,
                false,
                false,
                false,
                false,
                Color.White
            );
            BulbBlockMeshes[i].TransformTextureCoordinates(Matrix.CreateTranslation(0.1875f, 0.25f, 0f));
            BulbBlockMeshesLit[i] = new BlockMesh();
            BulbBlockMeshesLit[i].AppendModelPrimitive(
                topNode.RequireMesh().Primitives[0],
                nodeAbsoluteTransform * m,
                true,
                false,
                false,
                false,
                new Color(255, 255, 230)
            );
            BulbBlockMeshesLit[i].TransformTextureCoordinates(Matrix.CreateTranslation(0.9375f, 0f, 0f));
            SidesBlockMeshes[i] = new BlockMesh();
            SidesBlockMeshes[i].AppendModelPrimitive(
                sidesNode.RequireMesh().Primitives[0],
                nodeAbsoluteTransform2 * m,
                false,
                false,
                true,
                false,
                Color.White
            );
            SidesBlockMeshes[i].TransformTextureCoordinates(Matrix.CreateTranslation(0.9375f, 0.1875f, 0f));
        }

        var m2 = Matrix.CreateRotationY(-(float)Math.PI / 2f) * Matrix.CreateRotationZ((float)Math.PI / 2f);
        StandaloneBulbBlockMesh.AppendModelPrimitive(
            topNode.RequireMesh().Primitives[0],
            nodeAbsoluteTransform * m2,
            false,
            false,
            true,
            false,
            Color.White
        );
        StandaloneBulbBlockMesh.TransformTextureCoordinates(Matrix.CreateTranslation(0.1875f, 0.25f, 0f));
        StandaloneSidesBlockMesh.AppendModelPrimitive(
            sidesNode.RequireMesh().Primitives[0],
            nodeAbsoluteTransform2 * m2,
            false,
            false,
            true,
            false,
            Color.White
        );
        StandaloneSidesBlockMesh.TransformTextureCoordinates(Matrix.CreateTranslation(0.9375f, 0.1875f, 0f));
    }
}
