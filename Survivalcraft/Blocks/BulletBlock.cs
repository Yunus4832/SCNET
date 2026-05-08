using Engine.Graphics;

namespace Game.Blocks;

public class BulletBlock : FlatBlock
{
    public enum BulletType
    {
        MusketBall,
        Buckshot,
        BuckshotBall
    }

    public const int Index = 214;

    public static readonly string[] DisplayNames =
    [
        "枪弹",
        "铅弹",
        "铅弹球"
    ];

    public static readonly float[] Sizes =
    [
        1f,
        1f,
        0.33f
    ];

    public static readonly int[] TextureSlots =
    [
        229,
        231,
        229
    ];

    public static readonly float[] WeaponPowers =
    [
        80f,
        0f,
        3.6f
    ];

    public static readonly float[] ExplosionPressures = new float[3];

    public override void GenerateTerrainVertices(
        BlockGeometryGenerator generator,
        TerrainGeometry geometry,
        int value,
        int x,
        int y,
        int z
    )
    {
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
        var bulletType = (int)GetBulletType(Terrain.ExtractData(value));
        var size2 = bulletType >= 0 && bulletType < Sizes.Length ? size * Sizes[bulletType] : size;
        BlocksManager.DrawFlatOrImageExtrusionBlock(
            primitivesRenderer,
            value,
            size2,
            ref matrix,
            null,
            color,
            false,
            environmentData
        );
    }

    public override float GetProjectilePower(int value)
    {
        var bulletType = (int)GetBulletType(Terrain.ExtractData(value));
        if (bulletType < 0 || bulletType >= WeaponPowers.Length)
        {
            return 0f;
        }

        return WeaponPowers[bulletType];
    }

    public override float GetExplosionPressure(int value)
    {
        var bulletType = (int)GetBulletType(Terrain.ExtractData(value));
        if (bulletType < 0 || bulletType >= ExplosionPressures.Length)
        {
            return 0f;
        }

        return ExplosionPressures[bulletType];
    }

    public override IEnumerable<int> GetCreativeValues()
    {
        return EnumUtils.GetEnumValues(typeof(BulletType)).Select(enumValue =>
            Terrain.MakeBlockValue(214, 0, SetBulletType(0, (BulletType)enumValue)));
    }

    public override string GetDisplayName(SubsystemTerrain? subsystemTerrain, int value)
    {
        var bulletType = (int)GetBulletType(Terrain.ExtractData(value));
        if (bulletType < 0 || bulletType >= DisplayNames.Length)
        {
            return string.Empty;
        }

        return DisplayNames[bulletType];
    }

    public override int GetFaceTextureSlot(int face, int value)
    {
        var bulletType = (int)GetBulletType(Terrain.ExtractData(value));
        if (bulletType < 0 || bulletType >= TextureSlots.Length)
        {
            return 229;
        }

        return TextureSlots[bulletType];
    }

    public static BulletType GetBulletType(int data)
    {
        return (BulletType)(data & 0xF);
    }

    public static int SetBulletType(int data, BulletType bulletType)
    {
        return (data & -16) | (int)(bulletType & (BulletType)15);
    }
}
