using Engine.Graphics;

namespace Game.Blocks;

public class SpikedPlankBlock : MountedElectricElementBlock
{
    public const int Index = 86;

    public BlockMesh[] BlockMeshesByData = new BlockMesh[12];

    public BoundingBox[][] CollisionBoxesByData = new BoundingBox[12][];

    public BlockMesh StandaloneBlockMesh = new();

    public override void Initialize()
    {
        var model = ContentManager.Get<Model>("Models/SpikedPlanks");
        var array = new[]
        {
            "SpikedPlankRetracted",
            "SpikedPlank"
        };
        for (var i = 0; i < 2; i++)
        {
            var name = array[i];
            var modelMesh = model.FindMesh(name)!;
            var boneAbsoluteTransform = BlockMesh.GetBoneAbsoluteTransform(
                modelMesh.ParentBone ??
                throw new InvalidOperationException("Required ModelMesh.Parent is null")
            );
            for (var j = 0; j < 6; j++)
            {
                var num = SetMountingFace(SetSpikesState(0, i != 0), j);
                var m = j >= 4
                    ? j != 4
                        ? Matrix.CreateRotationX((float)Math.PI) * Matrix.CreateTranslation(0.5f, 1f, 0.5f)
                        : Matrix.CreateTranslation(0.5f, 0f, 0.5f)
                    : Matrix.CreateRotationX((float)Math.PI / 2f) * Matrix.CreateTranslation(0f, 0f, -0.5f) *
                      Matrix.CreateRotationY(j * (float)Math.PI / 2f) * Matrix.CreateTranslation(0.5f, 0.5f, 0.5f);
                BlockMeshesByData[num] = new BlockMesh();
                BlockMeshesByData[num].AppendModelMeshPart(modelMesh.MeshParts[0],
                    boneAbsoluteTransform * m, false, false, false, false, Color.White);
                CollisionBoxesByData[num] = [BlockMeshesByData[num].CalculateBoundingBox()];
            }

            var identity = Matrix.Identity;
            StandaloneBlockMesh.AppendModelMeshPart(modelMesh.MeshParts[0],
                boneAbsoluteTransform * identity, false, false, false, false, Color.White);
        }
    }

    public override bool IsFaceTransparent(SubsystemTerrain subsystemTerrain, int face, int value)
    {
        var mountingFace = GetMountingFace(Terrain.ExtractData(value));
        return face != CellFace.OppositeFace(mountingFace);
    }

    public override bool ShouldAvoid(int value)
    {
        return GetSpikesState(Terrain.ExtractData(value));
    }

    public static bool GetSpikesState(int data)
    {
        return (data & 1) == 0;
    }

    public static int SetSpikesState(int data, bool spikesState)
    {
        if (spikesState)
        {
            return data & -2;
        }

        return data | 1;
    }

    public static int GetMountingFace(int data)
    {
        return ((data >> 1) + 4) % 6;
    }

    public static int SetMountingFace(int data, int face)
    {
        data &= -15;
        data |= (((face + 2) % 6) & 7) << 1;
        return data;
    }

    public override int GetFace(int value)
    {
        return GetMountingFace(Terrain.ExtractData(value));
    }

    public override BlockPlacementData GetPlacementValue(SubsystemTerrain subsystemTerrain,
        ComponentMiner componentMiner, int value, TerrainRaycastResult raycastResult)
    {
        var data = SetMountingFace(SetSpikesState(Terrain.ExtractData(value), true), raycastResult.CellFace.Face);
        BlockPlacementData result = default;
        result.Value = Terrain.ReplaceData(value, data);
        result.CellFace = raycastResult.CellFace;
        return result;
    }

    public override BoundingBox[] GetCustomCollisionBoxes(SubsystemTerrain terrain, int value)
    {
        var num = Terrain.ExtractData(value);
        if (num >= CollisionBoxesByData.Length)
        {
            return base.GetCustomCollisionBoxes(terrain, value);
        }

        return CollisionBoxesByData[num];
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
        if (num >= BlockMeshesByData.Length || BlockMeshesByData[num] == null)
        {
            return;
        }

        generator.GenerateShadedMeshVertices(this, x, y, z, BlockMeshesByData[num], Color.White, null, [],
            geometry.SubsetOpaque);
        generator.GenerateWireVertices(value, x, y, z, GetFace(value), 1f, Vector2.Zero, geometry.SubsetOpaque);
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
            1f * size,
            ref matrix,
            environmentData
        );
    }

    public override ElectricElement CreateElectricElement(
        SubsystemElectricity subsystemElectricity,
        int value,
        int x,
        int y,
        int z
    )
    {
        return new SpikedPlankElectricElement(subsystemElectricity, new CellFace(x, y, z, GetFace(value)));
    }

    public override ElectricConnectorType? GetConnectorType(
        SubsystemTerrain terrain,
        int value,
        int face,
        int connectorFace,
        int x,
        int y,
        int z
    )
    {
        var face2 = GetFace(value);
        if (face == face2 && SubsystemElectricity.GetConnectorDirection(face2, 0, connectorFace).HasValue)
        {
            return ElectricConnectorType.Input;
        }

        return null;
    }
}
