using Engine.Graphics;

namespace Game.Blocks;

public abstract class SlabBlock(int coloredTextureSlot, int fullBlockIndex) : Block, IPaintableBlock
{
    public BoundingBox[][] CollisionBoxes = new BoundingBox[2][];

    public BlockMesh[] ColoredBlockMeshes = new BlockMesh[2];

    public int ColoredTextureSlot = coloredTextureSlot;

    public int FullBlockIndex = fullBlockIndex;

    public BlockMesh StandaloneColoredBlockMesh = new();

    public BlockMesh StandaloneUncoloredBlockMesh = new();

    public BlockMesh[] UncoloredBlockMeshes = new BlockMesh[2];

    public virtual int? GetPaintColor(int value)
    {
        return GetColor(Terrain.ExtractData(value));
    }

    public virtual int Paint(SubsystemTerrain? terrain, int value, int? color)
    {
        var data = Terrain.ExtractData(value);
        return Terrain.MakeBlockValue(BlockIndex, 0, SetColor(data, color));
    }

    public override void Initialize()
    {
        var model = ContentManager.Get<Model>("Models/Slab");
        var slabMesh = model.FindMesh("Slab")!;
        var meshPart = slabMesh.MeshParts[0];
        var boneAbsoluteTransform = BlockMesh.GetBoneAbsoluteTransform(
            slabMesh.ParentBone ??
            throw new InvalidOperationException("Required SlabMesh.ParentBone is null")
        );
        for (var i = 0; i < 2; i++)
        {
            var matrix = boneAbsoluteTransform * Matrix.CreateTranslation(0.5f, i == 0 ? 0f : 0.5f, 0.5f);
            UncoloredBlockMeshes[i] = new BlockMesh();
            UncoloredBlockMeshes[i].AppendModelMeshPart(meshPart, matrix, false, false, false, false, Color.White);
            UncoloredBlockMeshes[i]
                .TransformTextureCoordinates(Matrix.CreateTranslation(TextureSlot % 16 / 16f,
                    TextureSlot / 16 / 16f, 0f));
            UncoloredBlockMeshes[i].GenerateSidesData();
            ColoredBlockMeshes[i] = new BlockMesh();
            ColoredBlockMeshes[i].AppendModelMeshPart(meshPart, matrix, false, false, false, false, Color.White);
            ColoredBlockMeshes[i].TransformTextureCoordinates(
                Matrix.CreateTranslation(ColoredTextureSlot % 16 / 16f, ColoredTextureSlot / 16 / 16f, 0f));
            ColoredBlockMeshes[i].GenerateSidesData();
        }

        StandaloneUncoloredBlockMesh.AppendModelMeshPart(meshPart,
            boneAbsoluteTransform * Matrix.CreateTranslation(0f, -0.5f, 0f), false, false, false, false, Color.White);
        StandaloneUncoloredBlockMesh.TransformTextureCoordinates(
            Matrix.CreateTranslation(TextureSlot % 16 / 16f, TextureSlot / 16 / 16f, 0f));
        StandaloneColoredBlockMesh.AppendModelMeshPart(meshPart,
            boneAbsoluteTransform * Matrix.CreateTranslation(0f, -0.5f, 0f), false, false, false, false, Color.White);
        StandaloneColoredBlockMesh.TransformTextureCoordinates(
            Matrix.CreateTranslation(ColoredTextureSlot % 16 / 16f, ColoredTextureSlot / 16 / 16f, 0f));
        CollisionBoxes[0] = [new BoundingBox(new Vector3(0f, 0f, 0f), new Vector3(1f, 0.5f, 1f))];
        CollisionBoxes[1] = [new BoundingBox(new Vector3(0f, 0.5f, 0f), new Vector3(1f, 1f, 1f))];
        base.Initialize();
    }

    public override bool IsFaceTransparent(SubsystemTerrain subsystemTerrain, int face, int value)
    {
        if (GetIsTop(Terrain.ExtractData(value)))
        {
            return face != 4;
        }

        return face != 5;
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
        var num = GetIsTop(data) ? 1 : 0;
        var color = GetColor(data);
        if (color.HasValue)
        {
            generator.GenerateShadedMeshVertices(this, x, y, z, ColoredBlockMeshes[num],
                SubsystemPalette.GetColor(generator, color), null, [], geometry.SubsetOpaque);
        }
        else
        {
            generator.GenerateShadedMeshVertices(this, x, y, z, UncoloredBlockMeshes[num], Color.White, null, [],
                geometry.SubsetOpaque);
        }
    }

    public override BlockPlacementData GetPlacementValue(
        SubsystemTerrain subsystemTerrain,
        ComponentMiner componentMiner,
        int value,
        TerrainRaycastResult raycastResult
    )
    {
        var num = Terrain.ExtractContents(value);
        var data = Terrain.ExtractData(value);
        var num2 = Terrain.ExtractContents(raycastResult.Value);
        var data2 = Terrain.ExtractData(raycastResult.Value);
        BlockPlacementData result;
        if (num2 == num && ((GetIsTop(data2) && raycastResult.CellFace.Face == 5) ||
                            (!GetIsTop(data2) && raycastResult.CellFace.Face == 4)))
        {
            var value2 = Terrain.MakeBlockValue(FullBlockIndex, 0, 0);
            if (BlocksManager.Blocks[FullBlockIndex] is IPaintableBlock paintableBlock)
            {
                var color = GetColor(data);
                value2 = paintableBlock.Paint(subsystemTerrain, value2, color);
            }

            var cellFace = raycastResult.CellFace;
            cellFace.Point -= CellFace.FaceToPoint3(cellFace.Face);
            result = default;
            result.Value = value2;
            result.CellFace = cellFace;
            return result;
        }

        var isTop = raycastResult.CellFace.Face >= 4
            ? raycastResult.CellFace.Face == 5
            : raycastResult.HitPoint().Y - raycastResult.CellFace.Y > 0.5f;
        result = default;
        result.Value = Terrain.MakeBlockValue(BlockIndex, 0, SetIsTop(data, isTop));
        result.CellFace = raycastResult.CellFace;
        return result;
    }

    public override BoundingBox[] GetCustomCollisionBoxes(SubsystemTerrain terrain, int value)
    {
        var num = GetIsTop(Terrain.ExtractData(value)) ? 1 : 0;
        return CollisionBoxes[num];
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
        if (Terrain.ExtractContents(newValue) != FullBlockIndex)
        {
            var data = Terrain.ExtractData(oldValue);
            var data2 = SetColor(0, GetColor(data));
            var value = Terrain.MakeBlockValue(BlockIndex, 0, data2);
            dropValues.Add(new BlockDropValue
            {
                Value = value,
                Count = 1
            });
            showDebris = true;
        }
        else
        {
            showDebris = false;
        }
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
            GetFaceTextureSlot(0, value));
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
            BlocksManager.DrawMeshBlock(primitivesRenderer, StandaloneColoredBlockMesh,
                color * SubsystemPalette.GetColor(environmentData, color2), size, ref matrix, environmentData);
        }
        else
        {
            BlocksManager.DrawMeshBlock(primitivesRenderer, StandaloneUncoloredBlockMesh, color, size, ref matrix,
                environmentData);
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

    public static bool GetIsTop(int data)
    {
        return (data & 1) != 0;
    }

    public static int SetIsTop(int data, bool isTop)
    {
        return (data & -2) | (isTop ? 1 : 0);
    }

    public static int? GetColor(int data)
    {
        if ((data & 2) != 0)
        {
            return (data >> 2) & 0xF;
        }

        return null;
    }

    public static int SetColor(int data, int? color)
    {
        if (color.HasValue)
        {
            return (data & -63) | 2 | ((color.Value & 0xF) << 2);
        }

        return data & -63;
    }
}
