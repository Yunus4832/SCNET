using Engine.Graphics;

namespace Game.Blocks;

public class CompassBlock : Block
{
    public const int Index = 117;

    public BlockMesh CaseMesh = new();

    public BlockMesh PointerMesh = new();

    public override void Initialize()
    {
        var model = ContentManager.Get<Model>("Models/Compass");
        var caseMesh = model.FindMesh("Case")!;
        var pointerMesh = model.FindMesh("Pointer")!;
        var boneAbsoluteTransform = BlockMesh.GetBoneAbsoluteTransform(
            caseMesh.ParentBone ??
            throw new InvalidOperationException("Required CaseMesh.ParentBone is null")
        );
        var boneAbsoluteTransform2 = BlockMesh.GetBoneAbsoluteTransform(
            pointerMesh.ParentBone ??
            throw new InvalidOperationException("Required PointerMesh.ParentBone is null")
        );
        CaseMesh.AppendModelMeshPart(caseMesh.MeshParts[0],
            boneAbsoluteTransform * Matrix.CreateTranslation(0f, -0.01f, 0f), false, false, true, false, Color.White);
        PointerMesh.AppendModelMeshPart(pointerMesh.MeshParts[0],
            boneAbsoluteTransform2 * Matrix.CreateTranslation(0f, -0.01f, 0f), false, false, false, false, Color.White);
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
        var radians = 0f;
        if (environmentData is { SubsystemTerrain: not null })
        {
            var forward = environmentData.InWorldMatrix.Forward;
            var translation = environmentData.InWorldMatrix.Translation;
            var v = environmentData.SubsystemTerrain.Project
                .FindSubsystem<SubsystemMagnetBlockBehavior>(true)!
                .FindNearestCompassTarget(translation);
            var vector = translation - v;
            radians = Vector2.Angle(v2: new Vector2(forward.X, forward.Z), v1: new Vector2(vector.X, vector.Z));
        }

        var matrix2 = matrix;
        var matrix3 = Matrix.CreateRotationY(radians) * matrix;
        BlocksManager.DrawMeshBlock(primitivesRenderer, CaseMesh, color, size * 6f, ref matrix2, environmentData);
        BlocksManager.DrawMeshBlock(primitivesRenderer, PointerMesh, color, size * 6f, ref matrix3, environmentData);
    }
}
