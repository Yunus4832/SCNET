using Engine.Graphics;

namespace Game.Blocks;

public abstract class StairsBlock(int coloredTextureSlot) : Block, IPaintableBlock
{
    public enum CornerType
    {
        None,
        OneQuarter,
        ThreeQuarters
    }

    public BoundingBox[][] CollisionBoxes = new BoundingBox[24][];

    public BlockMesh[] ColoredBlockMeshes = new BlockMesh[24];

    public int ColoredTextureSlot = coloredTextureSlot;

    public BlockMesh StandaloneColoredBlockMesh = new();

    public BlockMesh StandaloneUncoloredBlockMesh = new();

    public BlockMesh[] UncoloredBlockMeshes = new BlockMesh[24];

    public virtual int? GetPaintColor(int value)
    {
        return GetColor(Terrain.ExtractData(value));
    }

    public virtual int Paint(SubsystemTerrain? terrain, int value, int? color)
    {
        return Terrain.MakeBlockValue(BlockIndex, 0, SetColor(Terrain.ExtractData(value), color));
    }

    public override void Initialize()
    {
        var model = ContentManager.Get<Model>("Models/Stairs");
        var stairsMesh = model.FindMesh("Stairs")!;
        var boneAbsoluteTransform = BlockMesh.GetBoneAbsoluteTransform(
            stairsMesh.ParentBone ??
            throw new InvalidOperationException("Required StairsMesh.ParentBone is null")
        );
        var stairsOuterCornerMesh = model.FindMesh("StairsOuterCorner")!;
        var boneAbsoluteTransform2 = BlockMesh.GetBoneAbsoluteTransform(
            stairsOuterCornerMesh.ParentBone ??
            throw new InvalidOperationException("Required StairsOuterCornerMesh.ParentBone is null")
        );
        var stairsInnerCornerMesh = model.FindMesh("StairsInnerCorner")!;
        var boneAbsoluteTransform3 = BlockMesh.GetBoneAbsoluteTransform(
            stairsInnerCornerMesh.ParentBone ??
            throw new InvalidOperationException("Required StairsInnerCornerMesh.ParentBone is null")
        );
        for (var i = 0; i < 24; i++)
        {
            var rotation = GetRotation(i);
            var isUpsideDown = GetIsUpsideDown(i);
            var cornerType = GetCornerType(i);
            var m = !isUpsideDown
                ? Matrix.CreateRotationY(rotation * (float)Math.PI / 2f) * Matrix.CreateTranslation(0.5f, 0f, 0.5f)
                : Matrix.CreateRotationY(rotation * (float)Math.PI / 2f) * Matrix.CreateTranslation(0.5f, -0.5f, 0.5f) *
                  Matrix.CreateScale(1f, -1f, 1f) * Matrix.CreateTranslation(0f, 0.5f, 0f);
            var blockMesh = new BlockMesh();
            switch (cornerType)
            {
                case CornerType.None:
                    blockMesh.AppendModelMeshPart(stairsMesh.MeshParts[0], boneAbsoluteTransform * m,
                        false, isUpsideDown, false, false, Color.White);
                    break;
                case CornerType.OneQuarter:
                    blockMesh.AppendModelMeshPart(stairsOuterCornerMesh.MeshParts[0],
                        boneAbsoluteTransform2 * m, false, isUpsideDown, false, false, Color.White);
                    break;
                case CornerType.ThreeQuarters:
                    blockMesh.AppendModelMeshPart(stairsInnerCornerMesh.MeshParts[0],
                        boneAbsoluteTransform3 * m, false, isUpsideDown, false, false, Color.White);
                    break;
            }

            float num = isUpsideDown ? rotation : -rotation;
            blockMesh.TransformTextureCoordinates(
                Matrix.CreateTranslation(-0.03125f, -0.03125f, 0f) * Matrix.CreateRotationZ(num * (float)Math.PI / 2f) *
                Matrix.CreateTranslation(0.03125f, 0.03125f, 0f), 16);
            blockMesh.TransformTextureCoordinates(
                Matrix.CreateTranslation(-0.03125f, -0.03125f, 0f) *
                Matrix.CreateRotationZ((0f - num) * (float)Math.PI / 2f) *
                Matrix.CreateTranslation(0.03125f, 0.03125f, 0f), 32);
            if (isUpsideDown)
            {
                blockMesh.TransformTextureCoordinates(Matrix.CreateTranslation(-0.03125f, -0.03125f, 0f) *
                                                      Matrix.CreateScale(1f, -1f, 1f) *
                                                      Matrix.CreateTranslation(0.03125f, 0.03125f, 0f));
            }

            ColoredBlockMeshes[i] = new BlockMesh();
            ColoredBlockMeshes[i].AppendBlockMesh(blockMesh);
            ColoredBlockMeshes[i].TransformTextureCoordinates(
                Matrix.CreateTranslation(ColoredTextureSlot % 16 / 16f, ColoredTextureSlot / 16 / 16f, 0f));
            ColoredBlockMeshes[i].GenerateSidesData();
            UncoloredBlockMeshes[i] = new BlockMesh();
            UncoloredBlockMeshes[i].AppendBlockMesh(blockMesh);
            UncoloredBlockMeshes[i]
                .TransformTextureCoordinates(Matrix.CreateTranslation(TextureSlot % 16 / 16f,
                    TextureSlot / 16 / 16f, 0f));
            UncoloredBlockMeshes[i].GenerateSidesData();
        }

        StandaloneUncoloredBlockMesh.AppendModelMeshPart(stairsMesh.MeshParts[0],
            boneAbsoluteTransform * Matrix.CreateTranslation(0f, -0.5f, 0f), false, false, false, false, Color.White);
        StandaloneUncoloredBlockMesh.TransformTextureCoordinates(
            Matrix.CreateTranslation(TextureSlot % 16 / 16f, TextureSlot / 16 / 16f, 0f));
        StandaloneColoredBlockMesh.AppendModelMeshPart(stairsMesh.MeshParts[0],
            boneAbsoluteTransform * Matrix.CreateTranslation(0f, -0.5f, 0f), false, false, false, false, Color.White);
        StandaloneColoredBlockMesh.TransformTextureCoordinates(
            Matrix.CreateTranslation(ColoredTextureSlot % 16 / 16f, ColoredTextureSlot / 16 / 16f, 0f));
        CollisionBoxes[0] =
        [
            new(new Vector3(0f, 0f, 0f), new Vector3(1f, 1f, 0.5f)),
            new(new Vector3(0f, 0f, 0.5f), new Vector3(1f, 0.5f, 1f))
        ];
        CollisionBoxes[1] =
        [
            new(new Vector3(0f, 0f, 0f), new Vector3(0.5f, 1f, 1f)),
            new(new Vector3(0.5f, 0f, 0f), new Vector3(1f, 0.5f, 1f))
        ];
        CollisionBoxes[2] =
        [
            new(new Vector3(0f, 0f, 0f), new Vector3(1f, 0.5f, 0.5f)),
            new(new Vector3(0f, 0f, 0.5f), new Vector3(1f, 1f, 1f))
        ];
        CollisionBoxes[3] =
        [
            new(new Vector3(0f, 0f, 0f), new Vector3(0.5f, 0.5f, 1f)),
            new(new Vector3(0.5f, 0f, 0f), new Vector3(1f, 1f, 1f))
        ];
        CollisionBoxes[4] =
        [
            new(new Vector3(0f, 0f, 0f), new Vector3(1f, 1f, 0.5f)),
            new(new Vector3(0f, 0.5f, 0.5f), new Vector3(1f, 1f, 1f))
        ];
        CollisionBoxes[5] =
        [
            new(new Vector3(0f, 0f, 0f), new Vector3(0.5f, 1f, 1f)),
            new(new Vector3(0.5f, 0.5f, 0f), new Vector3(1f, 1f, 1f))
        ];
        CollisionBoxes[6] =
        [
            new(new Vector3(0f, 0.5f, 0f), new Vector3(1f, 1f, 0.5f)),
            new(new Vector3(0f, 0f, 0.5f), new Vector3(1f, 1f, 1f))
        ];
        CollisionBoxes[7] =
        [
            new(new Vector3(0f, 0.5f, 0f), new Vector3(0.5f, 1f, 1f)),
            new(new Vector3(0.5f, 0f, 0f), new Vector3(1f, 1f, 1f))
        ];
        CollisionBoxes[8] =
        [
            new(new Vector3(0f, 0f, 0f), new Vector3(1f, 0.5f, 1f)),
            new(new Vector3(0.5f, 0.5f, 0f), new Vector3(1f, 1f, 0.5f))
        ];
        CollisionBoxes[9] =
        [
            new(new Vector3(0f, 0f, 0f), new Vector3(1f, 0.5f, 1f)),
            new(new Vector3(0f, 0.5f, 0f), new Vector3(0.5f, 1f, 0.5f))
        ];
        CollisionBoxes[10] =
        [
            new(new Vector3(0f, 0f, 0f), new Vector3(1f, 0.5f, 1f)),
            new(new Vector3(0f, 0.5f, 0.5f), new Vector3(0.5f, 1f, 1f))
        ];
        CollisionBoxes[11] =
        [
            new(new Vector3(0f, 0f, 0f), new Vector3(1f, 0.5f, 1f)),
            new(new Vector3(0.5f, 0.5f, 0.5f), new Vector3(1f, 1f, 1f))
        ];
        CollisionBoxes[12] =
        [
            new(new Vector3(0f, 0.5f, 0f), new Vector3(1f, 1f, 1f)),
            new(new Vector3(0.5f, 0f, 0f), new Vector3(1f, 0.5f, 0.5f))
        ];
        CollisionBoxes[13] =
        [
            new(new Vector3(0f, 0.5f, 0f), new Vector3(1f, 1f, 1f)),
            new(new Vector3(0f, 0f, 0f), new Vector3(0.5f, 0.5f, 0.5f))
        ];
        CollisionBoxes[14] =
        [
            new(new Vector3(0f, 0.5f, 0f), new Vector3(1f, 1f, 1f)),
            new(new Vector3(0f, 0f, 0.5f), new Vector3(0.5f, 0.5f, 1f))
        ];
        CollisionBoxes[15] =
        [
            new(new Vector3(0f, 0.5f, 0f), new Vector3(1f, 1f, 1f)),
            new(new Vector3(0.5f, 0f, 0.5f), new Vector3(1f, 0.5f, 1f))
        ];
        CollisionBoxes[16] =
        [
            new(new Vector3(0f, 0f, 0f), new Vector3(1f, 0.5f, 1f)),
            new(new Vector3(0f, 0.5f, 0f), new Vector3(1f, 1f, 0.5f)),
            new(new Vector3(0.5f, 0.5f, 0.5f), new Vector3(1f, 1f, 1f))
        ];
        CollisionBoxes[17] =
        [
            new(new Vector3(0f, 0f, 0f), new Vector3(1f, 0.5f, 1f)),
            new(new Vector3(0f, 0.5f, 0f), new Vector3(1f, 1f, 0.5f)),
            new(new Vector3(0f, 0.5f, 0.5f), new Vector3(0.5f, 1f, 1f))
        ];
        CollisionBoxes[18] =
        [
            new(new Vector3(0f, 0f, 0f), new Vector3(1f, 0.5f, 1f)),
            new(new Vector3(0f, 0.5f, 0.5f), new Vector3(1f, 1f, 1f)),
            new(new Vector3(0f, 0.5f, 0f), new Vector3(0.5f, 1f, 0.5f))
        ];
        CollisionBoxes[19] =
        [
            new(new Vector3(0f, 0f, 0f), new Vector3(1f, 0.5f, 1f)),
            new(new Vector3(0f, 0.5f, 0.5f), new Vector3(1f, 1f, 1f)),
            new(new Vector3(0.5f, 0.5f, 0f), new Vector3(1f, 1f, 0.5f))
        ];
        CollisionBoxes[20] =
        [
            new(new Vector3(0f, 0.5f, 0f), new Vector3(1f, 1f, 1f)),
            new(new Vector3(0f, 0f, 0f), new Vector3(1f, 0.5f, 0.5f)),
            new(new Vector3(0.5f, 0f, 0.5f), new Vector3(1f, 0.5f, 1f))
        ];
        CollisionBoxes[21] =
        [
            new(new Vector3(0f, 0.5f, 0f), new Vector3(1f, 1f, 1f)),
            new(new Vector3(0f, 0f, 0f), new Vector3(1f, 0.5f, 0.5f)),
            new(new Vector3(0f, 0f, 0.5f), new Vector3(0.5f, 0.5f, 1f))
        ];
        CollisionBoxes[22] =
        [
            new(new Vector3(0f, 0.5f, 0f), new Vector3(1f, 1f, 1f)),
            new(new Vector3(0f, 0f, 0.5f), new Vector3(1f, 0.5f, 1f)),
            new(new Vector3(0f, 0f, 0f), new Vector3(0.5f, 0.5f, 0.5f))
        ];
        CollisionBoxes[23] =
        [
            new(new Vector3(0f, 0.5f, 0f), new Vector3(1f, 1f, 1f)),
            new(new Vector3(0f, 0f, 0.5f), new Vector3(1f, 0.5f, 1f)),
            new(new Vector3(0.5f, 0f, 0f), new Vector3(1f, 0.5f, 0.5f))
        ];
        base.Initialize();
    }

    public override IEnumerable<int> GetCreativeValues()
    {
        yield return Terrain.MakeBlockValue(BlockIndex, 0, SetColor(0, null));
        var i = 0;
        while (i < 16)
        {
            yield return Terrain.MakeBlockValue(BlockIndex, 0, SetColor(0, i));
            var num = i + 1;
            i = num;
        }
    }

    public override bool IsFaceTransparent(SubsystemTerrain subsystemTerrain, int face, int value)
    {
        var data = Terrain.ExtractData(value);
        var isUpsideDown = GetIsUpsideDown(data);
        switch (face)
        {
            case 4:
                return !isUpsideDown;
            case 5:
                return isUpsideDown;
            default:
                switch (GetCornerType(data))
                {
                    case CornerType.None:
                    {
                        var rotation2 = GetRotation(data);
                        return face != ((rotation2 + 2) & 3);
                    }
                    case CornerType.OneQuarter:
                        return true;
                    default:
                    {
                        var rotation = GetRotation(data);
                        if (face != ((rotation + 1) & 3))
                        {
                            return face != ((rotation + 2) & 3);
                        }

                        return false;
                    }
                }
        }
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
        var color = GetColor(data);
        if (color.HasValue)
        {
            generator.GenerateShadedMeshVertices(this, x, y, z, ColoredBlockMeshes[GetVariant(data)],
                SubsystemPalette.GetColor(generator, color), null, [], geometry.SubsetOpaque);
        }
        else
        {
            generator.GenerateShadedMeshVertices(this, x, y, z, UncoloredBlockMeshes[GetVariant(data)], Color.White,
                null, [], geometry.SubsetOpaque);
        }
    }

    public override BlockPlacementData GetPlacementValue(
        SubsystemTerrain subsystemTerrain,
        ComponentMiner componentMiner,
        int value,
        TerrainRaycastResult raycastResult
    )
    {
        var forward = Matrix.CreateFromQuaternion(componentMiner.ComponentCreature.ComponentCreatureModel.EyeRotation)
            .Forward;
        var num = Vector3.Dot(forward, Vector3.UnitZ);
        var num2 = Vector3.Dot(forward, Vector3.UnitX);
        var num3 = Vector3.Dot(forward, -Vector3.UnitZ);
        var num4 = Vector3.Dot(forward, -Vector3.UnitX);
        var rotation = 0;
        if (num.CloseTo(MathUtils.Max(num, num2, num3, num4)))
        {
            rotation = 2;
        }
        else if (num2.CloseTo(MathUtils.Max(num, num2, num3, num4)))
        {
            rotation = 3;
        }
        else if (num3.CloseTo(MathUtils.Max(num, num2, num3, num4)))
        {
            rotation = 0;
        }
        else if (num4.CloseTo(MathUtils.Max(num, num2, num3, num4)))
        {
            rotation = 1;
        }

        var isUpsideDown = raycastResult.CellFace.Face == 5;
        var data = Terrain.ExtractData(value);
        BlockPlacementData result = default;
        result.Value =
            Terrain.MakeBlockValue(BlockIndex, 0, SetIsUpsideDown(SetRotation(data, rotation), isUpsideDown));
        result.CellFace = raycastResult.CellFace;
        return result;
    }

    public override BoundingBox[] GetCustomCollisionBoxes(SubsystemTerrain terrain, int value)
    {
        var data = Terrain.ExtractData(value);
        return CollisionBoxes[GetVariant(data)];
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
        var color2 = GetColor(Terrain.ExtractData(value));
        if (color2.HasValue)
        {
            BlocksManager.DrawMeshBlock(
                primitivesRenderer,
                StandaloneColoredBlockMesh,
                color * SubsystemPalette.GetColor(environmentData, color2),
                size,
                ref matrix,
                environmentData
            );
        }
        else
        {
            BlocksManager.DrawMeshBlock(
                primitivesRenderer,
                StandaloneUncoloredBlockMesh,
                color,
                size,
                ref matrix,
                environmentData
            );
        }
    }

    public override string GetDisplayName(SubsystemTerrain? subsystemTerrain, int value)
    {
        var color = GetColor(Terrain.ExtractData(value));
        return SubsystemPalette.GetName(color, base.GetDisplayName(subsystemTerrain, value));
    }

    public override string GetCategory(int value)
    {
        return !GetColor(Terrain.ExtractData(value)).HasValue ? base.GetCategory(value) : "Painted";
    }

    public override void GetDropValues(SubsystemTerrain subsystemTerrain, int oldValue, int newValue, int toolLevel,
        List<BlockDropValue> dropValues, out bool showDebris)
    {
        showDebris = true;
        var data = Terrain.ExtractData(oldValue);
        var data2 = SetColor(0, GetColor(data));
        var value = Terrain.MakeBlockValue(BlockIndex, 0, data2);
        dropValues.Add(new BlockDropValue
        {
            Value = value,
            Count = 1
        });
    }

    public override BlockDebrisParticleSystem CreateDebrisParticleSystem(
        SubsystemTerrain subsystemTerrain,
        Vector3 position,
        int value,
        float strength
    )
    {
        var color = GetColor(Terrain.ExtractData(value));
        if (color.HasValue)
        {
            return new BlockDebrisParticleSystem(subsystemTerrain, position, strength, DestructionDebrisScale,
                SubsystemPalette.GetColor(subsystemTerrain, color), ColoredTextureSlot);
        }

        return new BlockDebrisParticleSystem(subsystemTerrain, position, strength, DestructionDebrisScale, Color.White,
            base.GetFaceTextureSlot(0, value));
    }

    public static Point3 RotationToDirection(int rotation)
    {
        return CellFace.FaceToPoint3((rotation + 2) % 4);
    }

    public static int GetRotation(int data)
    {
        return data & 3;
    }

    public static int SetRotation(int data, int rotation)
    {
        return (data & -4) | (rotation & 3);
    }

    public static bool GetIsUpsideDown(int data)
    {
        return (data & 4) != 0;
    }

    public static int SetIsUpsideDown(int data, bool isUpsideDown)
    {
        if (isUpsideDown)
        {
            return data | 4;
        }

        return data & -5;
    }

    public static CornerType GetCornerType(int data)
    {
        return (CornerType)((data >> 3) & 3);
    }

    public static int SetCornerType(int data, CornerType cornerType)
    {
        return (data & -25) | ((int)(cornerType & (CornerType)3) << 3);
    }

    public static int? GetColor(int data)
    {
        if ((data & 0x20) != 0)
        {
            return (data >> 6) & 0xF;
        }

        return null;
    }

    public static int SetColor(int data, int? color)
    {
        if (color.HasValue)
        {
            return (data & -993) | 0x20 | ((color.Value & 0xF) << 6);
        }

        return data & -993;
    }

    public static int GetVariant(int data)
    {
        return data & 0x1F;
    }
}
