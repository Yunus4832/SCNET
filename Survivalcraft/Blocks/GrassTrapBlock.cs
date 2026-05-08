using Engine.Graphics;

namespace Game.Blocks;

public class GrassTrapBlock : Block
{
    public const int Index = 87;

    public readonly BlockMesh BlockMesh = new();

    public readonly BoundingBox[] CollisionBoxes = new BoundingBox[1];

    public readonly BlockMesh StandaloneBlockMesh = new();

    public override void Initialize()
    {
        var model = ContentManager.Get<Model>("Models/GrassTrap");
        var grassTrapMesh = model.FindMesh("GrassTrap")!;
        var boneAbsoluteTransform = BlockMesh.GetBoneAbsoluteTransform(
            grassTrapMesh.ParentBone ??
            throw new InvalidOperationException("Required GrassTrapMesh.ParentBone is null")
        );
        var color = BlockColorsMap.GrassColorsMap.Lookup(8, 15);
        BlockMesh.AppendModelMeshPart(grassTrapMesh.MeshParts[0],
            boneAbsoluteTransform * Matrix.CreateTranslation(0.5f, 0.75f, 0.5f), false, false, false, false,
            Color.White);
        StandaloneBlockMesh.AppendModelMeshPart(grassTrapMesh.MeshParts[0], boneAbsoluteTransform,
            false, false, false, false, color);
        CollisionBoxes[0] = new BoundingBox(new Vector3(0f, 0.75f, 0f), new Vector3(1f, 0.95f, 1f));
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
        generator.GenerateShadedMeshVertices(this, x, y, z, BlockMesh,
            BlockColorsMap.GrassColorsMap.Lookup(generator.Terrain, x, y, z), null, [], geometry.SubsetAlphaTest);
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

    public override BlockDebrisParticleSystem CreateDebrisParticleSystem(
        SubsystemTerrain subsystemTerrain,
        Vector3 position,
        int value,
        float strength
    )
    {
        var color = BlockColorsMap.GrassColorsMap.Lookup(subsystemTerrain.Terrain, Terrain.ToCell(position.X),
            Terrain.ToCell(position.Y), Terrain.ToCell(position.Z));
        return new BlockDebrisParticleSystem(subsystemTerrain, position, strength, DestructionDebrisScale, color,
            GetFaceTextureSlot(4, value));
    }

    public override BoundingBox[] GetCustomCollisionBoxes(SubsystemTerrain terrain, int value)
    {
        return CollisionBoxes;
    }
}
