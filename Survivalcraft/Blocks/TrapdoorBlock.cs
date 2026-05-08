using Engine.Graphics;

namespace Game.Blocks;

public abstract class TrapdoorBlock(string modelName) : Block, IElectricElementBlock
{
    public readonly BlockMesh[] BlockMeshesByData = new BlockMesh[16];

    public readonly BoundingBox[][] CollisionBoxesByData = new BoundingBox[16][];

    public string ModelName = modelName;

    public readonly BlockMesh StandaloneBlockMesh = new();

    public ElectricElement CreateElectricElement(
        SubsystemElectricity subsystemElectricity,
        int value,
        int x,
        int y,
        int z
    )
    {
        var data = Terrain.ExtractData(value);
        return new TrapDoorElectricElement(subsystemElectricity, new CellFace(x, y, z, GetMountingFace(data)));
    }

    public ElectricConnectorType? GetConnectorType(
        SubsystemTerrain terrain,
        int value,
        int face,
        int connectorFace,
        int x,
        int y,
        int z
    )
    {
        var data = Terrain.ExtractData(value);
        if (face != GetMountingFace(data))
        {
            return null;
        }

        var rotation = GetRotation(data);
        if (SubsystemElectricity.GetConnectorDirection(4, (4 - rotation) % 4, connectorFace) ==
            ElectricConnectorDirection.Top)
        {
            return ElectricConnectorType.Input;
        }

        return null;
    }

    public int GetConnectionMask(int value)
    {
        return int.MaxValue;
    }

    public override void Initialize()
    {
        var model = ContentManager.Get<Model>(ModelName);
        var trapdoorMesh = model.FindMesh("Trapdoor")!;
        var boneAbsoluteTransform = BlockMesh.GetBoneAbsoluteTransform(
            trapdoorMesh.ParentBone ??
            throw new InvalidOperationException("Required TrapdoorMesh.ParentBone is null")
        );
        for (var i = 0; i < 16; i++)
        {
            var rotation = GetRotation(i);
            var open = GetOpen(i);
            var upsideDown = GetUpsideDown(i);
            BlockMeshesByData[i] = new BlockMesh();
            var identity = Matrix.Identity;
            identity *= Matrix.CreateTranslation(0f, -0.0625f, 0.4375f) *
                        Matrix.CreateRotationX(open ? -(float)Math.PI / 2f : 0f) *
                        Matrix.CreateTranslation(0f, 0.0625f, -0.4375f);
            identity *= Matrix.CreateRotationZ(upsideDown ? (float)Math.PI : 0f);
            identity *= Matrix.CreateRotationY(rotation * (float)Math.PI / 2f);
            identity *= Matrix.CreateTranslation(new Vector3(0.5f, upsideDown ? 1 : 0, 0.5f));
            BlockMeshesByData[i].AppendModelMeshPart(trapdoorMesh.MeshParts[0],
                boneAbsoluteTransform * identity, false, false, false, false, Color.White);
            BlockMeshesByData[i].GenerateSidesData();
            CollisionBoxesByData[i] = [BlockMeshesByData[i].CalculateBoundingBox()];
        }

        StandaloneBlockMesh.AppendModelMeshPart(trapdoorMesh.MeshParts[0],
            boneAbsoluteTransform * Matrix.CreateTranslation(0f, 0f, 0f), false, false, false, false, Color.White);
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
        if (num < BlockMeshesByData.Length)
        {
            generator.GenerateShadedMeshVertices(this, x, y, z, BlockMeshesByData[num], Color.White, null, [],
                geometry.SubsetAlphaTest);
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
        BlocksManager.DrawMeshBlock(
            primitivesRenderer,
            StandaloneBlockMesh,
            color,
            size,
            ref matrix,
            environmentData
        );
    }

    public override BlockPlacementData GetPlacementValue(
        SubsystemTerrain subsystemTerrain,
        ComponentMiner componentMiner,
        int value,
        TerrainRaycastResult raycastResult
    )
    {
        int rotation;
        bool upsideDown;
        if (raycastResult.CellFace.Face < 4)
        {
            rotation = raycastResult.CellFace.Face;
            upsideDown = raycastResult.HitPoint().Y - raycastResult.CellFace.Y > 0.5f;
        }
        else
        {
            var forward = Matrix
                .CreateFromQuaternion(componentMiner.ComponentCreature.ComponentCreatureModel.EyeRotation).Forward;
            var num = Vector3.Dot(forward, Vector3.UnitZ);
            var num2 = Vector3.Dot(forward, Vector3.UnitX);
            var num3 = Vector3.Dot(forward, -Vector3.UnitZ);
            var num4 = Vector3.Dot(forward, -Vector3.UnitX);
            rotation = num.CloseTo(MathUtils.Max(num, num2, num3, num4))
                ? 2
                : num2.CloseTo(MathUtils.Max(num, num2, num3, num4))
                    ? 3
                    : num3.CloseTo(MathUtils.Max(num, num2, num3, num4))
                        ? num4.CloseTo(MathUtils.Max(num, num2, num3, num4)) ? 1 : 0
                        : 0;
            upsideDown = raycastResult.CellFace.Face == 5;
        }

        var data = SetOpen(SetRotation(SetUpsideDown(0, upsideDown), rotation), false);
        BlockPlacementData result = default;
        result.Value = Terrain.ReplaceData(Terrain.ReplaceContents(0, BlockIndex), data);
        result.CellFace = raycastResult.CellFace;
        return result;
    }

    public override BoundingBox[] GetCustomCollisionBoxes(SubsystemTerrain terrain, int value)
    {
        var num = Terrain.ExtractData(value);
        return num < CollisionBoxesByData.Length
            ? CollisionBoxesByData[num]
            : base.GetCustomCollisionBoxes(terrain, value);
    }

    public static int GetRotation(int data) => data & 3;

    public static bool GetOpen(int data) => (data & 4) != 0;

    public static bool GetUpsideDown(int data) => (data & 8) != 0;

    public static int SetRotation(int data, int rotation) => (data & -4) | (rotation & 3);

    public static int SetOpen(int data, bool open)
    {
        if (!open)
        {
            return data & -5;
        }

        return data | 4;
    }

    public static int SetUpsideDown(int data, bool upsideDown)
    {
        if (!upsideDown)
        {
            return data & -9;
        }

        return data | 8;
    }

    public override bool IsCollapseSupportBlock(SubsystemTerrain subsystemTerrain, int value)
    {
        var data = Terrain.ExtractData(value);
        if (GetUpsideDown(data))
        {
            return !GetOpen(data);
        }

        return false;
    }

    public override bool IsCollapseDestructibleBlock(int value)
    {
        var data = Terrain.ExtractData(value);
        return !GetUpsideDown(data)
               || !GetOpen(data);
    }

    public static int GetMountingFace(int data) => !GetUpsideDown(data) ? 4 : 5;

    public override bool IsMovableByPiston(int value, int pistonFace, int y, out bool isEnd)
    {
        isEnd = false;
        return false;
    }
}
