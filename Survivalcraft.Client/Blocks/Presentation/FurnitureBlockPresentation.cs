using Engine.Graphics;

using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class FurnitureBlockPresentation(FurnitureBlock block, BlockPresentationCatalog catalog)
    : BlockPresentation(block, catalog)
{
    protected new FurnitureBlock Block => (FurnitureBlock)base.Block;

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
        var designIndex = FurnitureBlock.GetDesignIndex(data);
        var rotation = FurnitureBlock.GetRotation(data);
        var design = generator.SubsystemFurnitureBlockBehavior.GetDesign(designIndex);
        if (design == null)
        {
            return;
        }

        var geometry2 = Catalog.Furniture.GetGeometry(design, generator.SubsystemTerrain);
        var mountingFacesMask = design.MountingFacesMask;
        for (var i = 0; i < 6; i++)
        {
            var num = CellFace.OppositeFace(i < 4 ? (i + rotation) % 4 : i);
            var b = (byte)(LightingManager.LightIntensityByLightValueAndFace[15 + 16 * num] * 255f);
            var color = new Color(b, b, b);
            generator.GenerateShadedMeshVertices(
                this,
                x,
                y,
                z,
                geometry2.SubsetOpaqueByFace[i],
                color,
                Block.Matrices[rotation],
                Block.FacesMaps[rotation],
                geometry.OpaqueSubsetsByFace[num]
            );
            generator.GenerateShadedMeshVertices(
                this,
                x,
                y,
                z,
                geometry2.SubsetAlphaTestByFace[i],
                color,
                Block.Matrices[rotation],
                Block.FacesMaps[rotation],
                geometry.AlphaTestSubsetsByFace[num]
            );
            var num2 = CellFace.OppositeFace(i < 4 ? (i - rotation + 4) % 4 : i);
            if ((mountingFacesMask & (1 << num2)) != 0)
            {
                generator.GenerateWireVertices(value, x, y, z, i, 0f, Vector2.Zero, geometry.SubsetOpaque);
            }
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
        if (environmentData.SubsystemTerrain == null)
        {
            return;
        }

        var designIndex = FurnitureBlock.GetDesignIndex(Terrain.ExtractData(value));
        var design = environmentData.SubsystemTerrain.SubsystemFurnitureBlockBehavior.GetDesign(designIndex);
        if (design == null)
        {
            return;
        }

        Vector3 v = default;
        v.X = -0.5f * (design.Box.Left + design.Box.Right) / design.Resolution;
        v.Y = -0.5f * (design.Box.Top + design.Box.Bottom) / design.Resolution;
        v.Z = -0.5f * (design.Box.Near + design.Box.Far) / design.Resolution;
        var matrix2 = Matrix.CreateTranslation(v * size) * matrix;
        var geometry = Catalog.Furniture.GetGeometry(design, environmentData.SubsystemTerrain);
        for (var i = 0; i < 6; i++)
        {
            var s = LightingManager.LightIntensityByLightValueAndFace[
                environmentData.Light + 16 * CellFace.OppositeFace(i)];
            var color2 = Color.MultiplyColorOnly(color, s);
            BlocksManager.DrawMeshBlock(
                primitivesRenderer,
                geometry.SubsetOpaqueByFace[i],
                color2,
                size,
                ref matrix2,
                environmentData
            );
            BlocksManager.DrawMeshBlock(
                primitivesRenderer,
                geometry.SubsetAlphaTestByFace[i],
                color2,
                size,
                ref matrix2,
                environmentData
            );
        }
    }

    public override int GetShadowStrength(int value)
    {
        var data = Terrain.ExtractData(value);
        if (FurnitureBlock.GetIsLightEmitter(data))
        {
            return -99;
        }

        return FurnitureBlock.GetShadowStrengthFactor(data) * 3 + 1;
    }

    public override string GetDisplayName(SubsystemTerrain? subsystemTerrain, int value)
    {
        var designIndex = FurnitureBlock.GetDesignIndex(Terrain.ExtractData(value));
        var design = subsystemTerrain?.SubsystemFurnitureBlockBehavior.GetDesign(designIndex);
        if (design == null)
        {
            return Localization.ClientLanguage.Catalog.GetBlock("FurnitureBlock:0", "DisplayName");
        }

        return !string.IsNullOrEmpty(design.Name) ? design.Name : Catalog.Furniture.GetDefaultName(design);
    }

    public override BlockDebrisParticleSystem? CreateDebrisParticleSystem(
        SubsystemTerrain subsystemTerrain,
        Vector3 position,
        int value,
        float strength
    )
    {
        if (DestructionDebrisScale <= 0f) { return null; }

        var faceTextureSlot = GetFaceTextureSlot(4, value);
        var designIndex = FurnitureBlock.GetDesignIndex(Terrain.ExtractData(value));
        var design = subsystemTerrain.SubsystemFurnitureBlockBehavior.GetDesign(designIndex);
        if (design == null)
        {
            return new BlockDebrisParticleSystem(
                subsystemTerrain,
                position,
                strength,
                DestructionDebrisScale,
                Color.White,
                faceTextureSlot
            );
        }

        var mainValue = design.MainValue;
        var num = Terrain.ExtractContents(mainValue);
        return Catalog.Get(num)
            .CreateDebrisParticleSystem(subsystemTerrain, position, mainValue, strength);
    }

    public override float GetIconViewScale(int value, DrawBlockEnvironmentData environmentData)
    {
        if (environmentData.SubsystemTerrain == null)
        {
            return base.GetIconViewScale(value, environmentData);
        }

        var designIndex = FurnitureBlock.GetDesignIndex(Terrain.ExtractData(value));
        var design = environmentData.SubsystemTerrain.SubsystemFurnitureBlockBehavior.GetDesign(designIndex);
        if (design == null)
        {
            return base.GetIconViewScale(value, environmentData);
        }

        var num = design.Resolution /
                  (float)MathUtils.Max(design.Box.Width, design.Box.Height, design.Box.Depth);
        return IconViewScale * num;
    }
}
