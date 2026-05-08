using Engine.Graphics;

namespace Game.Blocks;

public class ChristmasTreeBlock : Block, IElectricElementBlock
{
    public const int Index = 63;

    public BlockMesh DecorationsBlockMesh = new();

    public BlockMesh LeavesBlockMesh = new();

    public BlockMesh LitDecorationsBlockMesh = new();

    public BlockMesh StandaloneBlockMesh = new();

    public BlockMesh StandTrunkBlockMesh = new();

    public ElectricElement CreateElectricElement(
        SubsystemElectricity subsystemElectricity,
        int value,
        int x,
        int y,
        int z
    )
    {
        return new ChristmasTreeElectricElement(subsystemElectricity, new CellFace(x, y, z, 4), value);
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
        if (face == 4 && SubsystemElectricity.GetConnectorDirection(4, 0, connectorFace).HasValue)
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
        var model = ContentManager.Get<Model>("Models/ChristmasTree");
        var standTrunkMesh = model.FindMesh("StandTrunk")!;
        var leavesMesh = model.FindMesh("Leaves")!;
        var boneAbsoluteTransform = BlockMesh.GetBoneAbsoluteTransform(
            standTrunkMesh.ParentBone ??
            throw new InvalidOperationException("Required StandTrunkMesh.ParentBone is null")
        );
        var boneAbsoluteTransform2 = BlockMesh.GetBoneAbsoluteTransform(
            leavesMesh.ParentBone ??
            throw new InvalidOperationException("Required LeavesMesh.ParentBone is null")
        );
        var decorationsMesh = model.FindMesh("Decorations")!;
        var boneAbsoluteTransform3 = BlockMesh.GetBoneAbsoluteTransform(
            decorationsMesh.ParentBone ??
            throw new InvalidOperationException("Required DecorationsMesh.ParentBone is null")
        );
        var color = BlockColorsMap.SpruceLeavesColorsMap.Lookup(4, 15);
        LeavesBlockMesh.AppendModelMeshPart(leavesMesh.MeshParts[0],
            boneAbsoluteTransform2 * Matrix.CreateTranslation(0.5f, 0f, 0.5f), false, false, true, false, Color.White);
        StandTrunkBlockMesh.AppendModelMeshPart(standTrunkMesh.MeshParts[0],
            boneAbsoluteTransform * Matrix.CreateTranslation(0.5f, 0f, 0.5f), false, false, false, false, Color.White);
        DecorationsBlockMesh.AppendModelMeshPart(decorationsMesh.MeshParts[0],
            boneAbsoluteTransform3 * Matrix.CreateTranslation(0.5f, 0f, 0.5f), false, false, false, false, Color.White);
        LitDecorationsBlockMesh.AppendModelMeshPart(decorationsMesh.MeshParts[0],
            boneAbsoluteTransform3 * Matrix.CreateTranslation(0.5f, 0f, 0.5f), true, false, false, false, Color.White);
        StandaloneBlockMesh.AppendModelMeshPart(standTrunkMesh.MeshParts[0],
            boneAbsoluteTransform * Matrix.CreateTranslation(0f, -1f, 0f), false, false, false, false, Color.White);
        StandaloneBlockMesh.AppendModelMeshPart(leavesMesh.MeshParts[0],
            boneAbsoluteTransform2 * Matrix.CreateTranslation(0f, -1f, 0f), false, false, true, false, color);
        StandaloneBlockMesh.AppendModelMeshPart(decorationsMesh.MeshParts[0],
            boneAbsoluteTransform3 * Matrix.CreateTranslation(0f, -1f, 0f), false, false, false, false, Color.White);
        base.Initialize();
    }

    public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value,
        int x, int y, int z)
    {
        var color = BlockColorsMap.SpruceLeavesColorsMap.Lookup(generator.Terrain, x, y, z);
        if (GetLightState(Terrain.ExtractData(value)))
        {
            generator.GenerateMeshVertices(this, x, y, z, StandTrunkBlockMesh, Color.White, null,
                geometry.SubsetOpaque);
            generator.GenerateMeshVertices(this, x, y, z, LitDecorationsBlockMesh, Color.White, null,
                geometry.SubsetOpaque);
            generator.GenerateMeshVertices(this, x, y, z, LeavesBlockMesh, color, null, geometry.SubsetAlphaTest);
        }
        else
        {
            generator.GenerateMeshVertices(this, x, y, z, StandTrunkBlockMesh, Color.White, null,
                geometry.SubsetOpaque);
            generator.GenerateMeshVertices(this, x, y, z, DecorationsBlockMesh, Color.White, null,
                geometry.SubsetOpaque);
            generator.GenerateMeshVertices(this, x, y, z, LeavesBlockMesh, color, null, geometry.SubsetAlphaTest);
        }

        generator.GenerateWireVertices(value, x, y, z, 4, 0.01f, Vector2.Zero, geometry.SubsetOpaque);
    }

    public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size,
        ref Matrix matrix, DrawBlockEnvironmentData environmentData)
    {
        BlocksManager.DrawMeshBlock(primitivesRenderer, StandaloneBlockMesh, color, size, ref matrix,
            environmentData);
    }

    public override BlockDebrisParticleSystem CreateDebrisParticleSystem(SubsystemTerrain subsystemTerrain,
        Vector3 position, int value, float strength)
    {
        var color = BlockColorsMap.SpruceLeavesColorsMap.Lookup(subsystemTerrain.Terrain, Terrain.ToCell(position.X),
            Terrain.ToCell(position.Y), Terrain.ToCell(position.Z));
        return new BlockDebrisParticleSystem(subsystemTerrain, position, strength, DestructionDebrisScale, color,
            TextureSlot);
    }

    public override int GetEmittedLightAmount(int value)
    {
        if (!GetLightState(Terrain.ExtractData(value)))
        {
            return 0;
        }

        return EmittedLightAmount;
    }

    public override int GetShadowStrength(int value)
    {
        if (!GetLightState(Terrain.ExtractData(value)))
        {
            return ShadowStrength;
        }

        return -99;
    }

    public static bool GetLightState(int data)
    {
        return (data & 1) != 0;
    }

    public static int SetLightState(int data, bool state)
    {
        if (!state)
        {
            return data & -2;
        }

        return data | 1;
    }
}
