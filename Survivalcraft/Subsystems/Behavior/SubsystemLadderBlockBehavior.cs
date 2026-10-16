namespace Game.Subsystems;

public class SubsystemLadderBlockBehavior : SubsystemBlockBehavior
{
    public override ResourceId[] HandledBlocks => [ResourceId.Parse("game:wooden_ladder"), ResourceId.Parse("game:iron_ladder")];

    public override void OnNeighborBlockChanged(int x, int y, int z, int neighborX, int neighborY, int neighborZ)
    {
        var face = LadderBlock.GetFace(Terrain.ExtractData(SubsystemTerrain.Terrain.GetCellValue(x, y, z)));
        var point = CellFace.FaceToPoint3(face);
        var cellValue = SubsystemTerrain.Terrain.GetCellValue(x - point.X, y - point.Y, z - point.Z);
        var num = Terrain.ExtractContents(cellValue);
        if (CurrentModRuntime.Value!.RequireWorldCatalog().Blocks[num].IsFaceTransparent(SubsystemTerrain, face, cellValue))
        {
            SubsystemTerrain.DestroyCell(0, x, y, z, 0, false, false);
        }
    }
}
