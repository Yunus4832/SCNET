using Game.Network;
using Game.Network.Enums;

namespace Game.Subsystems;

public class SubsystemBottomSuckerBlockBehavior : SubsystemInWaterBlockBehavior
{
    public override int[] HandledBlocks => [];

    public override void OnNeighborBlockChanged(int x, int y, int z, int neighborX, int neighborY, int neighborZ)
    {
        base.OnNeighborBlockChanged(x, y, z, neighborX, neighborY, neighborZ);
        var face = BottomSuckerBlock.GetFace(Terrain.ExtractData(SubsystemTerrain.Terrain.GetCellValue(x, y, z)));
        var point = CellFace.FaceToPoint3(CellFace.OppositeFace(face));
        var cellValue = SubsystemTerrain.Terrain.GetCellValue(x + point.X, y + point.Y, z + point.Z);
        if (!IsSupport(cellValue, face))
        {
            SubsystemTerrain.DestroyCell(0, x, y, z, 0, false, false);
        }
    }

    public override void OnCollide(CellFace cellFace, float velocity, ComponentBody componentBody)
    {
        if (Terrain.ExtractContents(SubsystemTerrain.Terrain.GetCellValue(cellFace.X, cellFace.Y, cellFace.Z)) != 226)
        {
            return;
        }

        if (CommonLib.WorkType != WorkType.Client)
        {
            componentBody.Entity.FindComponent<ComponentCreature>()?.ComponentHealth
                .Injure(0.01f * MathUtils.Abs(velocity), null, false, "Spiked by a sea creature");
        }
    }

    public bool IsSupport(int value, int face)
    {
        var block = BlocksManager.Blocks[Terrain.ExtractContents(value)];
        if (block.Collidable)
        {
            return !block.IsFaceTransparent(SubsystemTerrain, CellFace.OppositeFace(face), value);
        }

        return false;
    }
}
