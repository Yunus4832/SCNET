using Engine.Graphics;

namespace Game.Blocks;

public class ArrowBlock : Block
{
    public enum ArrowType
    {
        WoodenArrow,
        StoneArrow,
        IronArrow,
        DiamondArrow,
        FireArrow,
        IronBolt,
        DiamondBolt,
        ExplosiveBolt,
        CopperArrow
    }

    public const int Index = 192;

    public static int[] Order = [0, 1, 8, 2, 3, 4, 5, 6, 7];

    public static string[] TipNames =
    [
        "ArrowTip",
        "ArrowTip",
        "ArrowTip",
        "ArrowTip",
        "ArrowFireTip",
        "BoltTip",
        "BoltTip",
        "BoltExplosiveTip",
        "ArrowTip"
    ];

    public static int[] TipTextureSlots = [47, 1, 63, 182, 62, 63, 182, 183, 79];

    public static string[] ShaftNames =
    [
        "ArrowShaft",
        "ArrowShaft",
        "ArrowShaft",
        "ArrowShaft",
        "ArrowShaft",
        "BoltShaft",
        "BoltShaft",
        "BoltShaft",
        "ArrowShaft"
    ];

    public static int[] ShaftTextureSlots = [4, 4, 4, 4, 4, 63, 63, 63, 4];

    public static string[] StabilizerNames =
    [
        "ArrowStabilizer",
        "ArrowStabilizer",
        "ArrowStabilizer",
        "ArrowStabilizer",
        "ArrowStabilizer",
        "BoltStabilizer",
        "BoltStabilizer",
        "BoltStabilizer",
        "ArrowStabilizer"
    ];

    public static int[] StabilizerTextureSlots = [15, 15, 15, 15, 15, 63, 63, 63, 15];

    public static string[] DisplayNames =
    [
        "木尖箭头",
        "石尖箭头",
        "铁尖箭头",
        "钻石尖箭头",
        "火尖箭头",
        "铁螺栓",
        "钻石尖螺栓",
        "爆炸螺栓",
        "铜尖箭头"
    ];

    public static float[] Offsets = [-0.5f, -0.5f, -0.5f, -0.5f, -0.5f, -0.3f, -0.3f, -0.3f, -0.5f];

    public static float[] WeaponPowers = [5f, 7f, 14f, 18f, 4f, 28f, 36f, 8f, 10f];

    public static float[] IconViewScales = [0.8f, 0.8f, 0.8f, 0.8f, 0.8f, 1.1f, 1.1f, 1.1f, 0.8f];

    public static float[] ExplosionPressures = [0f, 0f, 0f, 0f, 0f, 0f, 0f, 40f, 0f];

    public List<BlockMesh> StandaloneBlockMeshes = [];

    public override void Initialize()
    {
        var model = ContentManager.Get<Model>("Models/Arrows");
        foreach (var enumValue in EnumUtils.GetEnumValues(typeof(ArrowType)))
        {
            if (enumValue > 15)
            {
                throw new InvalidOperationException("Too many arrow types.");
            }

            var shaftNameMesh = model.FindMesh(ShaftNames[enumValue])!;
            var stabilizerNameMesh = model.FindMesh(StabilizerNames[enumValue])!;
            var tipNameMesh = model.FindMesh(TipNames[enumValue])!;
            var boneAbsoluteTransform = BlockMesh.GetBoneAbsoluteTransform(
                shaftNameMesh.ParentBone ??
                throw new InvalidOperationException("Required ShaftNameMesh.ParentBone is null")
            );
            var boneAbsoluteTransform2 = BlockMesh.GetBoneAbsoluteTransform(
                stabilizerNameMesh.ParentBone ??
                throw new InvalidOperationException("Required StabilizerNameMesh.ParentBone is null")
            );
            var boneAbsoluteTransform3 = BlockMesh.GetBoneAbsoluteTransform(
                tipNameMesh.ParentBone ??
                throw new InvalidOperationException("Required TipNameMesh.ParentBone is null")
            );
            var blockMesh = new BlockMesh();
            blockMesh.AppendModelMeshPart(tipNameMesh.MeshParts[0],
                boneAbsoluteTransform3 * Matrix.CreateTranslation(0f, Offsets[enumValue], 0f), false, false, false,
                false, Color.White);
            blockMesh.TransformTextureCoordinates(Matrix.CreateTranslation(TipTextureSlots[enumValue] % 16 / 16f,
                TipTextureSlots[enumValue] / 16 / 16f, 0f));
            var blockMesh2 = new BlockMesh();
            blockMesh2.AppendModelMeshPart(shaftNameMesh.MeshParts[0],
                boneAbsoluteTransform * Matrix.CreateTranslation(0f, Offsets[enumValue], 0f), false, false, false,
                false, Color.White);
            blockMesh2.TransformTextureCoordinates(Matrix.CreateTranslation(ShaftTextureSlots[enumValue] % 16 / 16f,
                ShaftTextureSlots[enumValue] / 16 / 16f, 0f));
            var blockMesh3 = new BlockMesh();
            blockMesh3.AppendModelMeshPart(stabilizerNameMesh.MeshParts[0],
                boneAbsoluteTransform2 * Matrix.CreateTranslation(0f, Offsets[enumValue], 0f), false, false, true,
                false, Color.White);
            blockMesh3.TransformTextureCoordinates(Matrix.CreateTranslation(
                StabilizerTextureSlots[enumValue] % 16 / 16f, StabilizerTextureSlots[enumValue] / 16 / 16f, 0f));
            var blockMesh4 = new BlockMesh();
            blockMesh4.AppendBlockMesh(blockMesh);
            blockMesh4.AppendBlockMesh(blockMesh2);
            blockMesh4.AppendBlockMesh(blockMesh3);
            StandaloneBlockMeshes.Add(blockMesh4);
        }

        base.Initialize();
    }

    public override void GenerateTerrainVertices(
        BlockGeometryGenerator generator,
        TerrainGeometry geometry,
        int value,
        int x,
        int y,
        int z)
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
        var arrowType = (int)GetArrowType(Terrain.ExtractData(value));
        if (arrowType >= 0 && arrowType < StandaloneBlockMeshes.Count)
        {
            BlocksManager.DrawMeshBlock(primitivesRenderer, StandaloneBlockMeshes[arrowType], color, 2f * size,
                ref matrix,
                environmentData);
        }
    }

    public override float GetProjectilePower(int value)
    {
        var arrowType = (int)GetArrowType(Terrain.ExtractData(value));
        if (arrowType < 0 || arrowType >= WeaponPowers.Length)
        {
            return 0f;
        }

        return WeaponPowers[arrowType];
    }

    public override float GetExplosionPressure(int value)
    {
        var arrowType = (int)GetArrowType(Terrain.ExtractData(value));
        if (arrowType < 0 || arrowType >= ExplosionPressures.Length)
        {
            return 0f;
        }

        return ExplosionPressures[arrowType];
    }

    public override float GetIconViewScale(int value, DrawBlockEnvironmentData environmentData)
    {
        var arrowType = (int)GetArrowType(Terrain.ExtractData(value));
        if (arrowType < 0 || arrowType >= IconViewScales.Length)
        {
            return 1f;
        }

        return IconViewScales[arrowType];
    }

    public override IEnumerable<int> GetCreativeValues()
    {
        var i = 0;
        while (i < Order.Length)
        {
            yield return Terrain.MakeBlockValue(192, 0, SetArrowType(0, (ArrowType)Order[i]));
            var num = i + 1;
            i = num;
        }
    }

    public override string GetDisplayName(SubsystemTerrain? subsystemTerrain, int value)
    {
        var arrowType = (int)GetArrowType(Terrain.ExtractData(value));
        if (arrowType < 0 || arrowType >= DisplayNames.Length)
        {
            return string.Empty;
        }

        return DisplayNames[arrowType];
    }

    public static ArrowType GetArrowType(int data)
    {
        return (ArrowType)(data & 0xF);
    }

    public static int SetArrowType(int data, ArrowType arrowType)
    {
        return (data & -16) | (int)(arrowType & (ArrowType)15);
    }
}
