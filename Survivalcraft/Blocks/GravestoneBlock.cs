using System.Globalization;

using Engine.Graphics;

namespace Game.Blocks;

public class GravestoneBlock : Block
{
    public const int Index = 189;

    public BlockMesh[] BlockMeshes = new BlockMesh[16];

    public BoundingBox[][] CollisionBoxes = new BoundingBox[16][];

    public BlockMesh[] StandaloneBlockMeshes = new BlockMesh[16];

    public override void Initialize()
    {
        var model = ContentManager.Get<Model>("Models/Graves");
        for (var i = 0; i < 16; i++)
        {
            var variant = GetVariant(i);
            var radians = GetRotation(i) == 0 ? 0f : (float)Math.PI / 2f;
            var name = "Grave" + (variant % 4 + 1).ToString(CultureInfo.InvariantCulture);
            var num = variant >= 4;
            var modelMesh = model.FindMesh(name)!;
            var boneAbsoluteTransform = BlockMesh.GetBoneAbsoluteTransform(
                modelMesh.ParentBone ??
                throw new InvalidOperationException("Required ModelMesh.ParentBone is null")
            );
            BlockMeshes[i] = new BlockMesh();
            BlockMeshes[i].AppendModelMeshPart(modelMesh.MeshParts[0],
                boneAbsoluteTransform * Matrix.CreateRotationY(radians) * Matrix.CreateTranslation(0.5f, 0f, 0.5f),
                false, false, false, false, Color.White);
            StandaloneBlockMeshes[i] = new BlockMesh();
            StandaloneBlockMeshes[i].AppendModelMeshPart(modelMesh.MeshParts[0],
                boneAbsoluteTransform * Matrix.CreateTranslation(0f, -0.5f, 0f), false, false, false, false,
                Color.White);
            if (num)
            {
                var boneAbsoluteTransform2 = BlockMesh.GetBoneAbsoluteTransform(
                    model.FindMesh("Plinth")!.ParentBone ??
                    throw new InvalidOperationException("Required PlinthMesh.ParentBone is null")
                );
                BlockMeshes[i].AppendModelMeshPart(model.FindMesh("Plinth")!.MeshParts[0],
                    boneAbsoluteTransform2 * Matrix.CreateRotationY(radians) * Matrix.CreateTranslation(0.5f, 0f, 0.5f),
                    false, false, false, false, Color.White);
                StandaloneBlockMeshes[i].AppendModelMeshPart(model.FindMesh("Plinth")!.MeshParts[0],
                    boneAbsoluteTransform2 * Matrix.CreateTranslation(0f, -0.5f, 0f), false, false, false, false,
                    Color.White);
            }

            CollisionBoxes[i] = [BlockMeshes[i].CalculateBoundingBox()];
        }

        base.Initialize();
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
        if (num >= BlockMeshes.Length)
        {
            return;
        }

        var num2Value = y > 0 ? generator.Terrain.GetCellValueFast(x, y - 1, z) : 0;
        var num2 = Terrain.ExtractContents(num2Value);
        var num3 = BlocksManager.Blocks[num2].GetBlockDigMethod(num2Value) != BlockDigMethod.Shovel;
        var flag = num2 is 7 or 4 or 52;
        var num4 = (int)(MathUtils.Hash((uint)(x + 172 * y + 18271 * z)) & 0xFFFF);
        var value2 = Matrix.Identity;
        if (!num3)
        {
            var radians = 0.2f * (num4 % 16 / 7.5f - 1f);
            var radians2 = 0.1f * ((num4 >> 4) % 16 / 7.5f - 1f);
            value2 = GetRotation(num) != 0
                ? Matrix.CreateTranslation(-0.5f, 0f, -0.5f) * Matrix.CreateRotationZ(radians) *
                  Matrix.CreateRotationY(radians2) * Matrix.CreateTranslation(0.5f, 0f, 0.5f)
                : Matrix.CreateTranslation(-0.5f, 0f, -0.5f) * Matrix.CreateRotationX(radians) *
                  Matrix.CreateRotationY(radians2) * Matrix.CreateTranslation(0.5f, 0f, 0.5f);
        }

        var f = num3 ? 0f : MathUtils.Sqr((num4 >> 8) % 16 / 15f);
        generator.GenerateMeshVertices(
            color: !flag
                ? Color.Lerp(Color.White, new Color(255, 233, 199), f)
                : Color.Lerp(new Color(217, 206, 123), new Color(229, 206, 123), f), block: this, x: x, y: y, z: z,
            blockMesh: BlockMeshes[num], matrix: value2, subset: geometry.SubsetOpaque);
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
        var num = Terrain.ExtractData(value);
        if (num < BlockMeshes.Length)
        {
            BlocksManager.DrawMeshBlock(primitivesRenderer, StandaloneBlockMeshes[num], color, size, ref matrix,
                environmentData);
        }
    }

    public override BoundingBox[] GetCustomCollisionBoxes(SubsystemTerrain terrain, int value)
    {
        var num = Terrain.ExtractData(value);
        if (num < CollisionBoxes.Length)
        {
            return CollisionBoxes[num];
        }

        return base.GetCustomCollisionBoxes(terrain, value);
    }

    public override BlockPlacementData GetPlacementValue(
        SubsystemTerrain subsystemTerrain,
        ComponentMiner componentMiner,
        int value,
        TerrainRaycastResult raycastResult
    )
    {
        var data = Terrain.ExtractData(value);
        var forward = Matrix.CreateFromQuaternion(componentMiner.ComponentCreature.ComponentCreatureModel.EyeRotation)
            .Forward;
        var num = MathUtils.Abs(Vector3.Dot(forward, Vector3.UnitX));
        BlockPlacementData result;
        if (MathUtils.Abs(Vector3.Dot(forward, Vector3.UnitZ)) > num)
        {
            result = default;
            result.Value = Terrain.MakeBlockValue(189, 0, SetRotation(data, 0));
            result.CellFace = raycastResult.CellFace;
            return result;
        }

        result = default;
        result.Value = Terrain.MakeBlockValue(189, 0, SetRotation(data, 1));
        result.CellFace = raycastResult.CellFace;
        return result;
    }

    public override IEnumerable<int> GetCreativeValues()
    {
        var i = 0;
        while (i < 8)
        {
            var data = SetVariant(0, i);
            yield return Terrain.MakeBlockValue(189, 0, data);
            var num = i + 1;
            i = num;
        }
    }

    public override void GetDropValues(SubsystemTerrain subsystemTerrain, int oldValue, int newValue, int toolLevel,
        List<BlockDropValue> dropValues, out bool showDebris)
    {
        showDebris = true;
        dropValues.Add(new BlockDropValue
        {
            Value = Terrain.MakeBlockValue(189, 0, Terrain.ExtractData(oldValue)),
            Count = 1
        });
    }

    public static int GetRotation(int data)
    {
        return (data & 8) >> 3;
    }

    public static int SetRotation(int data, int rotation)
    {
        return (data & -9) | ((rotation << 3) & 8);
    }

    public static int GetVariant(int data)
    {
        return data & 7;
    }

    public static int SetVariant(int data, int variant)
    {
        return (data & -8) | (variant & 7);
    }
}
