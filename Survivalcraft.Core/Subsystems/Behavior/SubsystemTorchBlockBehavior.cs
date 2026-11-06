namespace Game.Subsystems.Behavior;

public class SubsystemTorchBlockBehavior : SubsystemBlockBehavior
{
    public override ResourceId[] HandledBlocks =>
        [ResourceId.Parse("game:torch"), ResourceId.Parse("game:wicker_lamp"), ResourceId.Parse("game:jack_o_lantern")];

    public override void OnNeighborBlockChanged(int x, int y, int z, int neighborX, int neighborY, int neighborZ)
    {
        var cellValueFast = SubsystemTerrain.Terrain.GetCellValueFast(x, y, z);
        switch (Terrain.ExtractContents(cellValueFast))
        {
            case var torchContents when torchContents == BuiltInBlockRegistry.GetIndex<TorchBlock>():
            {
                var point = CellFace.FaceToPoint3(Terrain.ExtractData(cellValueFast));
                var x2 = x - point.X;
                var y2 = y - point.Y;
                var z2 = z - point.Z;
                var cellContents2 = SubsystemTerrain.Terrain.GetCellContents(x2, y2, z2);
                if (!WorldContentBinding.For(Project).Catalog.Blocks[cellContents2].Collidable)
                {
                    SubsystemTerrain.DestroyCell(0, x, y, z, 0, false, false);
                }

                break;
            }
            case var jackOLanternContents
                when jackOLanternContents == BuiltInBlockRegistry.GetIndex<JackOLanternBlock>():
            {
                var cellContents = SubsystemTerrain.Terrain.GetCellContents(x, y - 1, z);
                if (!WorldContentBinding.For(Project).Catalog.Blocks[cellContents].Collidable)
                {
                    SubsystemTerrain.DestroyCell(0, x, y, z, 0, false, false);
                }

                break;
            }
        }
    }
}
