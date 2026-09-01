using Engine.Graphics;

namespace Game.Blocks;

public class SoilBlock : CubeBlock
{
    public const int Index = 168;

    public const string TypeName = nameof(SoilBlock);

    public static readonly BoundingBox[] CollisionBoxes = [new(new Vector3(0f, 0f, 0f), new Vector3(1f, 0.9375f, 1f))];

    public override int GetFaceTextureSlot(int face, int value)
    {
        var nitrogen = GetNitrogen(Terrain.ExtractData(value));
        if (face != 4)
        {
            return 2;
        }

        return nitrogen <= 0 ? 37 : 53;
    }

    public static bool GetHydration(int data) => (data & 1) != 0;

    public static int GetNitrogen(int data) => (data >> 1) & 3;

    public static int SetHydration(int data, bool hydration)
    {
        if (!hydration)
        {
            return data & -2;
        }

        return data | 1;
    }

    public static int SetNitrogen(int data, int nitrogen)
    {
        nitrogen = MathUtils.Clamp(nitrogen, 0, 3);
        return (data & -7) | ((nitrogen & 3) << 1);
    }

    public override IEnumerable<int> GetCreativeValues()
    {
        yield return Terrain.MakeBlockValue(BlockIndex, 0, SetHydration(0, false));
        yield return Terrain.MakeBlockValue(BlockIndex, 0, SetHydration(0, true));
        yield return Terrain.MakeBlockValue(BlockIndex, 0, SetHydration(SetNitrogen(0, 3), true));
    }

    public override string GetDisplayName(SubsystemTerrain? subsystemTerrain, int value)
    {
        var data = Terrain.ExtractData(value);
        var nitrogen = GetNitrogen(data);
        var hydration = GetHydration(data);
        switch (nitrogen)
        {
            case > 0 when hydration:
                _ = LanguageManager.Get(TypeName, 2);
                return LanguageManager.Get(TypeName, 1);
            case > 0:
                _ = LanguageManager.Get(TypeName, 2);
                return LanguageManager.Get(TypeName, 2);
        }

        return LanguageManager.Get(TypeName, hydration ? 3 : 4);
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
        var color = GetHydration(Terrain.ExtractData(value)) ? new Color(180, 170, 150) : Color.White;
        generator.GenerateCubeVertices(this, value, x, y, z, 0.9375f, 0.9375f, 0.9375f, 0.9375f, color, color, color,
            color, color, -1, geometry.OpaqueSubsetsByFace);
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
        var c = GetHydration(Terrain.ExtractData(value)) ? new Color(180, 170, 150) : Color.White;
        base.DrawBlock(primitivesRenderer, value, color * c, size, ref matrix, environmentData);
    }

    public override BoundingBox[] GetCustomCollisionBoxes(SubsystemTerrain terrain, int value) => CollisionBoxes;

    public override bool IsFaceTransparent(SubsystemTerrain subsystemTerrain, int face, int value) => face != 5;

    public override bool IsCollapseSupportBlock(SubsystemTerrain subsystemTerrain, int value) => true;

    public override bool IsFaceNonAttachable(
        SubsystemTerrain subsystemTerrain,
        int face,
        int value,
        int attachBlockValue
    )
    {
        var block = BlocksManager.Blocks[Terrain.ExtractContents(attachBlockValue)];
        return block is not BasePumpkinBlock &&
               base.IsFaceNonAttachable(subsystemTerrain, face, value, attachBlockValue);
    }
}
