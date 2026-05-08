using Engine.Graphics;

namespace Game.Blocks;

public class PistonBlock : Block, IElectricElementBlock
{
    public const int Index = 237;

    public readonly BlockMesh[] BlockMeshesByData = new BlockMesh[48];

    public readonly BlockMesh[] StandaloneBlockMeshes = new BlockMesh[4];

    public ElectricElement CreateElectricElement(
        SubsystemElectricity subsystemElectricity,
        int value,
        int x,
        int y,
        int z
    )
    {
        return new PistonElectricElement(subsystemElectricity, new Point3(x, y, z));
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
        return ElectricConnectorType.Input;
    }

    public int GetConnectionMask(int value)
    {
        return int.MaxValue;
    }

    public override void Initialize()
    {
        var model = ContentManager.Get<Model>("Models/Pistons");
        for (var pistonMode = PistonMode.Pushing; pistonMode <= PistonMode.StrictPulling; pistonMode++)
        {
            for (var i = 0; i < 2; i++)
            {
                var name = i == 0 ? "PistonRetracted" : "PistonExtended";
                var modelMesh = model.FindMesh(name)!;
                var boneAbsoluteTransform = BlockMesh.GetBoneAbsoluteTransform(
                    modelMesh.ParentBone ??
                    throw new InvalidOperationException("Required ModelMesh.ParentBone is null")
                );
                for (var j = 0; j < 6; j++)
                {
                    var num = SetFace(SetIsExtended(SetMode(0, pistonMode), i != 0), j);
                    var m = j < 4
                        ? Matrix.CreateTranslation(0f, -0.5f, 0f) *
                          Matrix.CreateRotationY(j * (float)Math.PI / 2f + (float)Math.PI) *
                          Matrix.CreateTranslation(0.5f, 0.5f, 0.5f)
                        : j != 4
                            ? Matrix.CreateTranslation(0f, -0.5f, 0f) * Matrix.CreateRotationX(-(float)Math.PI / 2f) *
                              Matrix.CreateTranslation(0.5f, 0.5f, 0.5f)
                            : Matrix.CreateTranslation(0f, -0.5f, 0f) * Matrix.CreateRotationX((float)Math.PI / 2f) *
                              Matrix.CreateTranslation(0.5f, 0.5f, 0.5f);
                    BlockMeshesByData[num] = new BlockMesh();
                    BlockMeshesByData[num].AppendModelMeshPart(modelMesh.MeshParts[0],
                        boneAbsoluteTransform * m, false, false, false, false, Color.White);
                    if (i == 0)
                    {
                        switch (pistonMode)
                        {
                            case PistonMode.Pulling:
                                BlockMeshesByData[num]
                                    .TransformTextureCoordinates(Matrix.CreateTranslation(0f, 0.0625f, 0f), 1 << j);
                                break;
                            case PistonMode.StrictPulling:
                                BlockMeshesByData[num]
                                    .TransformTextureCoordinates(Matrix.CreateTranslation(0f, 0.125f, 0f), 1 << j);
                                break;
                        }
                    }
                }
            }

            var boneAbsoluteTransform2 =
                BlockMesh.GetBoneAbsoluteTransform(
                    model.FindMesh("PistonRetracted")!.ParentBone ??
                    throw new InvalidOperationException("Required PistonRetractedMesh.ParentBone is null")
                );
            StandaloneBlockMeshes[(int)pistonMode] = new BlockMesh();
            StandaloneBlockMeshes[(int)pistonMode].AppendModelMeshPart(model.FindMesh("PistonRetracted")!.MeshParts[0],
                boneAbsoluteTransform2 * Matrix.CreateTranslation(0f, -0.5f, 0f), false, false, false, false,
                Color.White);
            switch (pistonMode)
            {
                case PistonMode.Pulling:
                    StandaloneBlockMeshes[(int)pistonMode]
                        .TransformTextureCoordinates(Matrix.CreateTranslation(0f, 0.0625f, 0f), 4);
                    break;
                case PistonMode.StrictPulling:
                    StandaloneBlockMeshes[(int)pistonMode]
                        .TransformTextureCoordinates(Matrix.CreateTranslation(0f, 0.125f, 0f), 4);
                    break;
            }
        }
    }

    public override bool IsCollapseSupportBlock(SubsystemTerrain subsystemTerrain, int value)
    {
        return !IsFaceTransparent(subsystemTerrain, 4, value);
    }

    public override bool IsFaceTransparent(SubsystemTerrain subsystemTerrain, int face, int value)
    {
        var data = Terrain.ExtractData(value);
        var face2 = GetFace(data);
        if (!GetIsExtended(data))
        {
            return false;
        }

        if (face != face2)
        {
            return face != CellFace.OppositeFace(face2);
        }

        return false;
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
        var num = Terrain.ExtractData(value) & 0x3F;
        if (num < BlockMeshesByData.Length && BlockMeshesByData[num] != null)
        {
            generator.GenerateShadedMeshVertices(this, x, y, z, BlockMeshesByData[num], Color.White, null, [],
                geometry.SubsetOpaque);
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
        var mode = (int)GetMode(Terrain.ExtractData(value));
        if (mode < StandaloneBlockMeshes.Length && StandaloneBlockMeshes[mode] != null)
        {
            BlocksManager.DrawMeshBlock(
                primitivesRenderer,
                StandaloneBlockMeshes[mode],
                color,
                1f * size,
                ref matrix,
                environmentData
            );
        }
    }

    public override IEnumerable<int> GetCreativeValues()
    {
        yield return Terrain.MakeBlockValue(237, 0, SetMode(SetMaxExtension(0, 7), PistonMode.Pushing));
        yield return Terrain.MakeBlockValue(237, 0, SetMode(SetMaxExtension(0, 7), PistonMode.Pulling));
        yield return Terrain.MakeBlockValue(237, 0, SetMode(SetMaxExtension(0, 7), PistonMode.StrictPulling));
    }

    public override string GetDisplayName(SubsystemTerrain? subsystemTerrain, int value)
    {
        return GetMode(Terrain.ExtractData(value)) switch
        {
            PistonMode.Pulling => "粘性活塞",
            PistonMode.StrictPulling => "严格粘性活塞",
            _ => "活塞"
        };
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
        var num = float.PositiveInfinity;
        var face = 0;
        for (var i = 0; i < 6; i++)
        {
            var num2 = Vector3.Dot(CellFace.FaceToVector3(i), forward);
            if (!(num2 < num))
            {
                continue;
            }

            num = num2;
            face = i;
        }

        var data = Terrain.ExtractData(value);
        BlockPlacementData result = default;
        result.Value = Terrain.MakeBlockValue(BlockIndex, 0, SetFace(data, face));
        result.CellFace = raycastResult.CellFace;
        return result;
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
        var data = Terrain.ExtractData(oldValue);
        dropValues.Add(new BlockDropValue
        {
            Value = Terrain.MakeBlockValue(237, 0, SetFace(SetIsExtended(data, false), 0)),
            Count = 1
        });
        showDebris = true;
    }

    public static bool GetIsExtended(int data) => (data & 1) != 0;

    public static int SetIsExtended(int data, bool isExtended) => (data & -2) | (isExtended ? 1 : 0);

    public static PistonMode GetMode(int data) => (PistonMode)((data >> 1) & 3);

    public static int SetMode(int data, PistonMode mode) => (data & -7) | ((int)(mode & (PistonMode)3) << 1);

    public static int GetFace(int data) => (data >> 3) & 7;

    public static int SetFace(int data, int face) => (data & -57) | ((face & 7) << 3);

    public static int GetMaxExtension(int data) => (data >> 6) & 7;

    public static int SetMaxExtension(int data, int maxExtension) => (data & -449) | ((maxExtension & 7) << 6);

    public static int GetPullCount(int data) => (data >> 9) & 7;

    public static int SetPullCount(int data, int pullCount) => (data & -3585) | ((pullCount & 7) << 9);

    public static int GetSpeed(int data) => (data >> 12) & 7;

    public static int SetSpeed(int data, int speed) => (data & -12289) | ((speed & 3) << 12);

    public static bool GetWaitingForTerrain(int data) => data >> 14 != 0;

    public static int SetWaitingForTerrain(int data, bool value) => (data & ~(1 << 14)) | ((value ? 1 : 0) << 14);

    public override bool IsMovableByPiston(int value, int pistonFace, int y, out bool isEnd)
    {
        var data = Terrain.ExtractData(value);
        isEnd = false;
        return !GetIsExtended(data);
    }

    public override bool IsFaceNonAttachable(
        SubsystemTerrain subsystemTerrain,
        int face,
        int value,
        int attachBlockValue
    )
    {
        return IsFaceTransparent(subsystemTerrain, face, value);
    }
}
