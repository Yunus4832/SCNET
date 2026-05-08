using Engine.Graphics;

namespace Game.Blocks;

public class BowBlock : Block
{
    public const int Index = 191;

    public BlockMesh[] StandaloneBlockMeshes = new BlockMesh[16];

    public override void Initialize()
    {
        var model = ContentManager.Get<Model>("Models/Bows");
        var bowRelaxedMesh = model.FindMesh("BowRelaxed")!;
        var stringRelaxedMesh = model.FindMesh("StringRelaxed")!;
        var bowTensedMesh = model.FindMesh("BowTensed")!;
        var stringTensedMesh = model.FindMesh("StringTensed")!;
        var boneAbsoluteTransform = BlockMesh.GetBoneAbsoluteTransform(
            bowRelaxedMesh.ParentBone ??
            throw new InvalidOperationException("Required BowRelaxedMesh.ParentBone is null")
        );
        var boneAbsoluteTransform2 = BlockMesh.GetBoneAbsoluteTransform(
            stringRelaxedMesh.ParentBone ??
            throw new InvalidOperationException("Required StringRelaxedMesh.ParentBone is null")
        );
        var boneAbsoluteTransform3 = BlockMesh.GetBoneAbsoluteTransform(
            bowTensedMesh.ParentBone ??
            throw new InvalidOperationException("Required BowTensedMesh.ParentBone is null")
        );
        var boneAbsoluteTransform4 = BlockMesh.GetBoneAbsoluteTransform(
            stringTensedMesh.ParentBone ??
            throw new InvalidOperationException("Required  StringTensedMesh.ParentBone is null")
        );
        var blockMesh = new BlockMesh();
        blockMesh.AppendModelMeshPart(bowRelaxedMesh.MeshParts[0],
            boneAbsoluteTransform * Matrix.CreateTranslation(0f, -0.5f, 0f), false, false, false, false, Color.White);
        blockMesh.AppendModelMeshPart(stringRelaxedMesh.MeshParts[0],
            boneAbsoluteTransform2 * Matrix.CreateTranslation(0f, -0.5f, 0f), false, false, false, false, Color.White);
        var blockMesh2 = new BlockMesh();
        blockMesh2.AppendModelMeshPart(bowTensedMesh.MeshParts[0],
            boneAbsoluteTransform3 * Matrix.CreateTranslation(0f, -0.5f, 0f), false, false, false, false, Color.White);
        blockMesh2.AppendModelMeshPart(stringTensedMesh.MeshParts[0],
            boneAbsoluteTransform4 * Matrix.CreateTranslation(0f, -0.5f, 0f), false, false, false, false, Color.White);
        for (var i = 0; i < 16; i++)
        {
            var factor = i / 15f;
            StandaloneBlockMeshes[i] = new BlockMesh();
            StandaloneBlockMeshes[i].AppendBlockMesh(blockMesh);
            StandaloneBlockMeshes[i].BlendBlockMesh(blockMesh2, factor);
        }

        base.Initialize();
    }

    public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value,
        int x, int y, int z)
    {
    }

    public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size,
        ref Matrix matrix, DrawBlockEnvironmentData environmentData)
    {
        var data = Terrain.ExtractData(value);
        var draw = GetDraw(data);
        var arrowType = GetArrowType(data);
        BlocksManager.DrawMeshBlock(primitivesRenderer, StandaloneBlockMeshes[draw], color, 2f * size, ref matrix,
            environmentData);
        if (arrowType.HasValue)
        {
            var num = MathUtils.Lerp(0.14f, 0.68f, draw / 15f);
            var matrix2 = Matrix.CreateRotationX(-(float)Math.PI / 2f) *
                          Matrix.CreateTranslation(0f, 0.4f * size, (-1f + 2f * num) * size) * matrix;
            var value2 = Terrain.MakeBlockValue(192, 0, ArrowBlock.SetArrowType(0, arrowType.Value));
            BlocksManager.Blocks[192].DrawBlock(primitivesRenderer, value2, color, size, ref matrix2, environmentData);
        }
    }

    public override int GetDamage(int value)
    {
        return (Terrain.ExtractData(value) >> 8) & 0xFF;
    }

    public override int SetDamage(int value, int damage)
    {
        var num = Terrain.ExtractData(value);
        num &= -65281;
        num |= MathUtils.Clamp(damage, 0, 255) << 8;
        return Terrain.ReplaceData(value, num);
    }

    public override bool IsSwapAnimationNeeded(int oldValue, int newValue)
    {
        var num = Terrain.ExtractContents(oldValue);
        var data = Terrain.ExtractData(oldValue);
        var data2 = Terrain.ExtractData(newValue);
        if (num == 191 && GetArrowType(data) == GetArrowType(data2))
        {
            return false;
        }

        return true;
    }

    public static ArrowBlock.ArrowType? GetArrowType(int data)
    {
        var num = (data >> 4) & 0xF;
        if (num != 0)
        {
            return (ArrowBlock.ArrowType)(num - 1);
        }

        return null;
    }

    public static int SetArrowType(int data, ArrowBlock.ArrowType? arrowType)
    {
        var num = (int)(arrowType.HasValue ? arrowType.Value + 1 : ArrowBlock.ArrowType.WoodenArrow);
        return (data & -241) | ((num & 0xF) << 4);
    }

    public static int GetDraw(int data)
    {
        return data & 0xF;
    }

    public static int SetDraw(int data, int draw)
    {
        return (data & -16) | (draw & 0xF);
    }
}
