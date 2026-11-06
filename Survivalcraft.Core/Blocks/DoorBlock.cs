using Game.Blocks.Collision;

namespace Game.Blocks;

public abstract class DoorBlock(string modelName, float pivotDistance) : Block, IElectricElementBlock
{
    public readonly BoundingBox[][] CollisionBoxesByData = new BoundingBox[16][];


    public readonly float PivotDistance = pivotDistance;


    public ElectricElement CreateElectricElement(
        SubsystemElectricity subsystemElectricity,
        int value,
        int x,
        int y,
        int z
    )
    {
        var data = Terrain.ExtractData(value);
        return new DoorElectricElement(subsystemElectricity, new CellFace(x, y, z, GetHingeFace(data)));
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
        var hingeFace = GetHingeFace(Terrain.ExtractData(value));
        if (face != hingeFace)
        {
            return null;
        }

        var connectorDirection = SubsystemElectricity.GetConnectorDirection(hingeFace, 0, connectorFace);
        if (connectorDirection is ElectricConnectorDirection.Right
            or ElectricConnectorDirection.Left
            or ElectricConnectorDirection.In)
        {
            return ElectricConnectorType.Input;
        }


        return null;
    }

    public int GetConnectionMask(int value)
    {
        return int.MaxValue;
    }

    public override ResourceId? CollisionModelAsset { get; } = ResourceId.Parse("game:" + modelName + ".glb");

    public override void Initialize()
    {
        var blockPositionsByData = new CollisionGeometryBuilder[16];
        var model = Catalog.CollisionGeometry.Get(CollisionModelAsset!.Value);
        var doorNode = model.GetNode("Door")!;
        var nodeAbsoluteTransform = doorNode.AbsoluteTransform;
        for (var i = 0; i < 16; i++)
        {
            var rotation = GetRotation(i);
            var open = GetOpen(i);
            var rightHanded = GetRightHanded(i);
            float num = !rightHanded ? 1 : -1;
            blockPositionsByData[i] = new CollisionGeometryBuilder();
            var identity = Matrix.Identity;
            identity *= Matrix.CreateScale(0f - num, 1f, 1f);
            identity *= Matrix.CreateTranslation((0.5f - PivotDistance) * num, 0f, 0f) *
                        Matrix.CreateRotationY(open ? num * (float)Math.PI / 2f : 0f) *
                        Matrix.CreateTranslation((0f - (0.5f - PivotDistance)) * num, 0f, 0f);
            identity *= Matrix.CreateTranslation(0f, 0f, 0.5f - PivotDistance) *
                        Matrix.CreateRotationY(rotation * (float)Math.PI / 2f) *
                        Matrix.CreateTranslation(0.5f, 0f, 0.5f);
            blockPositionsByData[i].Append(doorNode.Primitives[0], nodeAbsoluteTransform * identity);
            var boundingBox = blockPositionsByData[i].GetBounds();
            var max = boundingBox.Max;
            max.Y = 1f;
            boundingBox.Max = max;
            CollisionBoxesByData[i] = [boundingBox];
        }

        base.Initialize();
    }


    /// <summary>
    ///     获取放置参数
    /// </summary>
    /// <param name="subsystemTerrain"> 地形子系统 </param>
    /// <param name="eyeForward"> 视线方向 </param>
    /// <param name="value"> 方块 Data 值，在 DoorBlock 门方块中，该值未使用 </param>
    /// <param name="raycastResult"> 光线投射结果 </param>
    /// <returns> 方块放置参数 </returns>
    public override BlockPlacementData GetPlacementValue(
        SubsystemTerrain subsystemTerrain,
        Vector3 eyeForward,
        int value,
        TerrainRaycastResult raycastResult
    )
    {
        // 获取矿工（这是应该是玩家）的面朝向量，即前面的方向
        var forward = eyeForward;

        // 计算面朝向量在各个轴方向上的投影值
        var projectionZPositive = Vector3.Dot(forward, Vector3.UnitZ); // 面朝向量在 +Z 方向的投影
        var projectionXPositive = Vector3.Dot(forward, Vector3.UnitX); // 面朝向量在 +X 方向的投影
        var projectionZNegative = Vector3.Dot(forward, -Vector3.UnitZ); // 面朝向量在 -Z 方向的投影
        var projectionXNegative = Vector3.Dot(forward, -Vector3.UnitX); // 面朝向量在 -X 方向的投影

        // 计算最大投影长度以确定门最终的朝向
        var maxProjection = MathUtils.Max(
            projectionZPositive,
            projectionXPositive,
            projectionZNegative,
            projectionXNegative
        );

        // 门的朝向，0-前 1-右 2-后 3-左
        //          2-back
        //          +----+
        //  3-left  |    | 1-right
        //          +----+
        //         0-forward
        var rotation = 0;

        // 门的朝向根据 x, z 轴投影长度最大值确定
        if (maxProjection.CloseTo(projectionZPositive))
        {
            rotation = 2;
        }
        else if (maxProjection.CloseTo(projectionXPositive))
        {
            rotation = 3;
        }
        else if (maxProjection.CloseTo(projectionZNegative))
        {
            rotation = 0;
        }
        else if (maxProjection.CloseTo(projectionXNegative))
        {
            rotation = 1;
        }

        // 通过 CellFace 获取放置门方块的坐标
        var point = CellFace.FaceToPoint3(raycastResult.CellFace.Face);
        var x = raycastResult.CellFace.X + point.X;
        var y = raycastResult.CellFace.Y + point.Y;
        var z = raycastResult.CellFace.Z + point.Z;

        // 获取左边相邻方块的 x, z 坐标
        var leftNeighborX = 0;
        var leftNeighborY = 0;
        switch (rotation)
        {
            case 0:
                leftNeighborX = x - 1;
                leftNeighborY = z;
                break;
            case 1:
                leftNeighborX = x;
                leftNeighborY = z + 1;
                break;
            case 2:
                leftNeighborX = x + 1;
                leftNeighborY = z;
                break;
            case 3:
                leftNeighborX = x;
                leftNeighborY = z - 1;
                break;
        }

        var cellValue = subsystemTerrain.Terrain.GetCellValue(leftNeighborX, y, leftNeighborY);
        var rightHanded = Catalog.Blocks[Terrain.ExtractContents(cellValue)].IsNonAttachable(cellValue);

        var data = SetRightHanded(SetOpen(SetRotation(0, rotation), false), rightHanded);
        var result = new BlockPlacementData
        {
            Value = Terrain.ReplaceData(Terrain.ReplaceContents(0, BlockIndex), data),
            CellFace = raycastResult.CellFace
        };
        return result;
    }

    public override BoundingBox[] GetCustomCollisionBoxes(SubsystemTerrain terrain, int value)
    {
        var num = Terrain.ExtractData(value);
        return num < CollisionBoxesByData.Length ? CollisionBoxesByData[num] : [];
    }

    public override bool ShouldAvoid(int value)
    {
        return !GetOpen(Terrain.ExtractData(value));
    }

    public override bool IsHeatBlocker(int value)
    {
        return !GetOpen(Terrain.ExtractData(value));
    }

    public static int GetRotation(int data)
    {
        return data & 3;
    }

    public static bool GetOpen(int data)
    {
        return (data & 4) != 0;
    }

    public static bool GetRightHanded(int data)
    {
        return (data & 8) == 0;
    }

    public static int SetRotation(int data, int rotation)
    {
        return (data & -4) | (rotation & 3);
    }

    public static int SetOpen(int data, bool open)
    {
        if (!open)
        {
            return data & -5;
        }

        return data | 4;
    }

    public static int SetRightHanded(int data, bool rightHanded)
    {
        if (rightHanded)
        {
            return data & -9;
        }

        return data | 8;
    }

    public static bool IsTopPart(Terrain terrain, BlockContentCatalog catalog, int x, int y, int z)
    {
        return catalog.Blocks[terrain.GetCellContents(x, y - 1, z)] is DoorBlock;
    }

    public static bool IsBottomPart(Terrain terrain, BlockContentCatalog catalog, int x, int y, int z)
    {
        return catalog.Blocks[terrain.GetCellContents(x, y + 1, z)] is DoorBlock;
    }

    public static int GetHingeFace(int data)
    {
        var rotation = GetRotation(data);
        var num = rotation - 1 < 0 ? 3 : rotation - 1;
        if (!GetRightHanded(data))
        {
            num = CellFace.OppositeFace(num);
        }

        return num;
    }

    public override bool IsMovableByPiston(int value, int pistonFace, int y, out bool isEnd)
    {
        isEnd = false;
        return false;
    }
}
