using Engine.Graphics;

namespace Game.Blocks;

public class SeaUrchinBlock : BottomSuckerBlock
{
    public new const int Index = 226;

    public static Color[] Colors =
    [
        new(20, 20, 20),
        new(50, 20, 20),
        new(80, 30, 30),
        new(20, 20, 40)
    ];

    public static Vector2[] Offsets =
    [
        0.15f * new Vector2(-0.8f, -1f),
        0.15f * new Vector2(1f, -0.75f),
        0.15f * new Vector2(-0.65f, 1f),
        0.15f * new Vector2(0.9f, 0.7f)
    ];

    public BlockMesh[] BlockMeshes = new BlockMesh[24];

    public BoundingBox[][] CollisionBoxes = new BoundingBox[24][];

    public BlockMesh StandaloneBlockMesh = new();

    public override void Initialize()
    {
        var model = ContentManager.Get<Model>("Models/SeaUrchin");
        var urchinMesh = model.FindMesh("Urchin")!;
        var boneAbsoluteTransform = BlockMesh.GetBoneAbsoluteTransform(
            urchinMesh.ParentBone ??
            throw new InvalidOperationException("Required UrchinMesh.ParentBone is null")
        );
        var bottomMesh = model.FindMesh("Bottom")!;
        var boneAbsoluteTransform2 = BlockMesh.GetBoneAbsoluteTransform(
            bottomMesh.ParentBone ??
            throw new InvalidOperationException("Required BottomMesh.ParentBone is null")
        );
        for (var i = 0; i < 6; i++)
        {
            for (var j = 0; j < 4; j++)
            {
                var zero = Vector2.Zero;
                if (i < 4)
                {
                    zero.Y = i * (float)Math.PI / 2f;
                }
                else
                {
                    zero.X = i == 4 ? -(float)Math.PI / 2f : (float)Math.PI / 2f;
                }

                var m = Matrix.CreateRotationX((float)Math.PI / 2f) * Matrix.CreateRotationZ(0.3f + 2f * j) *
                        Matrix.CreateTranslation(Offsets[j].X, Offsets[j].Y, -0.49f) * Matrix.CreateRotationX(zero.X) *
                        Matrix.CreateRotationY(zero.Y) * Matrix.CreateTranslation(0.5f, 0.5f, 0.5f);
                var num = 4 * i + j;
                BlockMeshes[num] = new BlockMesh();
                BlockMeshes[num].AppendModelMeshPart(urchinMesh.MeshParts[0], boneAbsoluteTransform * m,
                    false, false, false, false, Color.White);
                CollisionBoxes[num] = [BlockMeshes[num].CalculateBoundingBox()];
            }
        }

        StandaloneBlockMesh = new BlockMesh();
        StandaloneBlockMesh.AppendModelMeshPart(urchinMesh.MeshParts[0],
            boneAbsoluteTransform * Matrix.CreateTranslation(0f, -0.1f, 0f), false, false, false, false, Color.White);
        StandaloneBlockMesh.AppendModelMeshPart(bottomMesh.MeshParts[0],
            boneAbsoluteTransform2 * Matrix.CreateTranslation(0f, -0.1f, 0f), false, false, false, false, Color.White);
        base.Initialize();
    }

    public override BoundingBox[] GetCustomCollisionBoxes(SubsystemTerrain terrain, int value)
    {
        var data = Terrain.ExtractData(value);
        var face = GetFace(data);
        var subvariant = GetSubvariant(data);
        return CollisionBoxes[4 * face + subvariant];
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
        var face = GetFace(data);
        var subvariant = GetSubvariant(data);
        var color = Colors[subvariant];
        generator.GenerateMeshVertices(this, x, y, z, BlockMeshes[4 * face + subvariant], color, null,
            geometry.SubsetOpaque);
        base.GenerateTerrainVertices(generator, geometry, value, x, y, z);
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
            color * new Color(40, 40, 40),
            3f * size,
            ref matrix,
            environmentData
        );
    }

    public override BlockDebrisParticleSystem CreateDebrisParticleSystem(
        SubsystemTerrain subsystemTerrain,
        Vector3 position,
        int value,
        float strength
    )
    {
        return new BlockDebrisParticleSystem(subsystemTerrain, position, 0.75f * strength, DestructionDebrisScale,
            new Color(64, 64, 64), TextureSlot);
    }
}
