using Engine.Graphics;

namespace Game.Blocks;

public class PistonHeadBlock : Block
{
    public const int Index = 238;

    public readonly BlockMesh[] BlockMeshesByData = new BlockMesh[48];

    public override void Initialize()
    {
        var model = ContentManager.Get<Model>("Models/Pistons");
        for (var i = 0; i < 2; i++)
        {
            var name = i == 0 ? "PistonHead" : "PistonShaft";
            var modelMesh = model.FindMesh(name)!;
            var boneAbsoluteTransform = BlockMesh.GetBoneAbsoluteTransform(
                modelMesh.ParentBone ??
                throw new InvalidOperationException("Required ModelMesh.ParentBone is null")
            );
            for (var pistonMode = PistonMode.Pushing; pistonMode <= PistonMode.StrictPulling; pistonMode++)
            for (var j = 0; j < 6; j++)
            {
                var num = SetFace(SetMode(SetIsShaft(0, i != 0), pistonMode), j);
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

    public override bool IsFaceTransparent(SubsystemTerrain subsystemTerrain, int face, int value)
    {
        var data = Terrain.ExtractData(value);
        return face != GetFace(data);
    }

    public override int GetShadowStrength(int value)
    {
        if (!GetIsShaft(Terrain.ExtractData(value)))
        {
            return base.GetShadowStrength(value);
        }

        return 0;
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
    }

    public static PistonMode GetMode(int data) => (PistonMode)(data & 3);

    public static int SetMode(int data, PistonMode mode) => (data & -4) | (int)(mode & (PistonMode)3);

    public static bool GetIsShaft(int data) => (data & 4) != 0;

    public static int SetIsShaft(int data, bool isShaft) => (data & -5) | (isShaft ? 4 : 0);

    public static int GetFace(int data) => (data >> 3) & 7;

    public static int SetFace(int data, int face) => (data & -57) | ((face & 7) << 3);

    public override bool IsCollapseSupportBlock(SubsystemTerrain subsystemTerrain, int value) => true;

    public override bool IsMovableByPiston(int value, int pistonFace, int y, out bool isEnd)
    {
        isEnd = false;
        return false;
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
