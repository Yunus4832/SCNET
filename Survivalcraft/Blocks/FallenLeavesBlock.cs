using Engine.Graphics;

namespace Game.Blocks;

public class FallenLeavesBlock : CubeBlock
{
    public const int Index = 261;

    private const float _height = 0.0625f;

    private readonly BoundingBox[] _collisionBoxes = [new(new Vector3(0f, 0f, 0f), new Vector3(1f, 0.0625f, 1f))];

    public override void GenerateTerrainVertices(
        BlockGeometryGenerator generator,
        TerrainGeometry geometry,
        int value,
        int x,
        int y,
        int z
    )
    {
        var sideColor = new Color(180, 170, 160);
        var color = GetColor(x, y, z);
        var color2 = GetColor(x, y, z + 1);
        var color3 = GetColor(x + 1, y, z);
        var color4 = GetColor(x + 1, y, z + 1);
        generator.GenerateCubeVertices(this, value, x, y, z, 0.0625f, 0.0625f, 0.0625f, 0.0625f, sideColor, color,
            color3, color4, color2, -1, geometry.AlphaTestSubsetsByFace);
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
        BlocksManager.DrawCubeBlock(
            primitivesRenderer,
            value,
            new Vector3(size),
            0.0625f,
            ref matrix,
            color,
            color,
            environmentData
        );
    }

    public override BlockDebrisParticleSystem CreateDebrisParticleSystem(
        SubsystemTerrain subsystemTerrain,
        Vector3 position,
        int value,
        float strength
    )
    {
        var color = GetColor(Terrain.ToCell(position.X), Terrain.ToCell(position.Y), Terrain.ToCell(position.Z));
        return new BlockDebrisParticleSystem(
            subsystemTerrain,
            position,
            strength,
            DestructionDebrisScale,
            color,
            GetFaceTextureSlot(4, value)
        );
    }

    public override void GetDropValues(
        SubsystemTerrain subsystemTerrain,
        int oldValue,
        int newValue,
        int toolLevel,
        List<BlockDropValue> dropValues,
        out bool showDebris
    )
    {
        showDebris = true;
    }

    public override BoundingBox[] GetCustomCollisionBoxes(SubsystemTerrain terrain, int value)
    {
        return _collisionBoxes;
    }

    private static Color GetColor(int x, int y, int z)
    {
        var num = (uint)MathUtils.Hash(x + y * 59 + z * 2411);
        return Color.Lerp(new Color(128, 110, 110), new Color(255, 255, 220), num / 4.2949673E+09f);
    }
}
