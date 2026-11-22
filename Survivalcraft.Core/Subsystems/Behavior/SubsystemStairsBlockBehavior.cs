namespace Game.Subsystems.Behavior;

public class SubsystemStairsBlockBehavior : SubsystemBlockBehavior
{
    public override ResourceId[] HandledBlocks => WorldContentBinding.For(Project).Catalog.Registry
        .ById.Values
        .Where(entry => entry.Block is StairsBlock).Select(entry => entry.Id).ToArray();

    public override void OnNeighborBlockChanged(int x, int y, int z, int neighborX, int neighborY, int neighborZ)
    {
        var cellValue = SubsystemTerrain.Terrain.GetCellValue(x, y, z);
        UpdateIsCorner(cellValue, x, y, z, true);
    }

    public override void OnBlockGenerated(int value, int x, int y, int z, bool isLoaded)
    {
        UpdateIsCorner(value, x, y, z, false);
    }

    public override void OnBlockAdded(int value, int oldValue, int x, int y, int z)
    {
        UpdateIsCorner(value, x, y, z, true);
    }

    private void UpdateIsCorner(int value, int x, int y, int z, bool updateModificationCounter)
    {
        var value2 = Terrain.ExtractContents(value);
        if (WorldContentBinding.For(Project).Catalog.Blocks[value2] is not StairsBlock)
        {
            return;
        }

        var data = Terrain.ExtractData(value);
        if (StairsBlock.GetCornerType(data) != 0)
        {
            return;
        }

        var rotation = StairsBlock.GetRotation(data);
        var isUpsideDown = StairsBlock.GetIsUpsideDown(data);
        var point = StairsBlock.RotationToDirection(rotation);
        var cellValue = SubsystemTerrain.Terrain.GetCellValue(x + point.X, y + point.Y, z + point.Z);
        var contents = Terrain.ExtractContents(cellValue);
        if (WorldContentBinding.For(Project).Catalog.Blocks[contents] is StairsBlock)
        {
            var data2 = Terrain.ExtractData(cellValue);
            var isUpsideDown2 = StairsBlock.GetIsUpsideDown(data2);
            var cornerType = StairsBlock.GetCornerType(data2);
            var quarterRotation = -1;
            if (isUpsideDown2 == isUpsideDown)
            {
                var rotation2 = StairsBlock.GetRotation(data2);
                if (rotation == 0 && rotation2 == 1 && cornerType != StairsBlock.CornerType.ThreeQuarters)
                {
                    quarterRotation = 1;
                }

                if (rotation == 0 && rotation2 == 3 && cornerType != StairsBlock.CornerType.ThreeQuarters)
                {
                    quarterRotation = 0;
                }

                if (rotation == 1 && rotation2 == 0 && cornerType != StairsBlock.CornerType.ThreeQuarters)
                {
                    quarterRotation = 1;
                }

                if (rotation == 1 && rotation2 == 2 && cornerType != StairsBlock.CornerType.ThreeQuarters)
                {
                    quarterRotation = 2;
                }

                if (rotation == 2 && rotation2 == 1 && cornerType != StairsBlock.CornerType.ThreeQuarters)
                {
                    quarterRotation = 2;
                }

                if (rotation == 2 && rotation2 == 3 && cornerType != StairsBlock.CornerType.ThreeQuarters)
                {
                    quarterRotation = 3;
                }

                if (rotation == 3 && rotation2 == 0 && cornerType != StairsBlock.CornerType.ThreeQuarters)
                {
                    quarterRotation = 0;
                }

                if (rotation == 3 && rotation2 == 2 && cornerType != StairsBlock.CornerType.ThreeQuarters)
                {
                    quarterRotation = 3;
                }
            }

            if (quarterRotation < 0)
            {
                return;
            }

            var data3 = StairsBlock.SetRotation(
                StairsBlock.SetCornerType(data, StairsBlock.CornerType.OneQuarter),
                quarterRotation
            );
            var value3 = Terrain.ReplaceData(value, data3);
            SubsystemTerrain.ChangeCellNet(x, y, z, value3, updateModificationCounter);

            return;
        }

        cellValue = SubsystemTerrain.Terrain.GetCellValue(x - point.X, y - point.Y, z - point.Z);
        contents = Terrain.ExtractContents(cellValue);
        if (WorldContentBinding.For(Project).Catalog.Blocks[contents] is not StairsBlock)
        {
            return;
        }

        var data4 = Terrain.ExtractData(cellValue);
        var isUpsideDown3 = StairsBlock.GetIsUpsideDown(data4);
        var cornerType2 = StairsBlock.GetCornerType(data4);
        var cornerRotation = -1;
        if (isUpsideDown3 == isUpsideDown)
        {
            var rotation3 = StairsBlock.GetRotation(data4);
            if (rotation == 0 && rotation3 == 1 && cornerType2 == StairsBlock.CornerType.None)
            {
                cornerRotation = 1;
            }

            if (rotation == 0 && rotation3 == 3 && cornerType2 == StairsBlock.CornerType.None)
            {
                cornerRotation = 0;
            }

            if (rotation == 0 && rotation3 == 2 && cornerType2 == StairsBlock.CornerType.ThreeQuarters)
            {
                cornerRotation = 1;
            }

            if (rotation == 0 && rotation3 == 3 && cornerType2 == StairsBlock.CornerType.ThreeQuarters)
            {
                cornerRotation = 0;
            }

            if (rotation == 1 && rotation3 == 0 && cornerType2 == StairsBlock.CornerType.None)
            {
                cornerRotation = 1;
            }

            if (rotation == 1 && rotation3 == 2 && cornerType2 == StairsBlock.CornerType.None)
            {
                cornerRotation = 2;
            }

            if (rotation == 1 && rotation3 == 3 && cornerType2 == StairsBlock.CornerType.ThreeQuarters)
            {
                cornerRotation = 2;
            }

            if (rotation == 1 && rotation3 == 0 && cornerType2 == StairsBlock.CornerType.ThreeQuarters)
            {
                cornerRotation = 1;
            }

            if (rotation == 2 && rotation3 == 1 && cornerType2 == StairsBlock.CornerType.None)
            {
                cornerRotation = 2;
            }

            if (rotation == 2 && rotation3 == 3 && cornerType2 == StairsBlock.CornerType.None)
            {
                cornerRotation = 3;
            }

            if (rotation == 2 && rotation3 == 0 && cornerType2 == StairsBlock.CornerType.ThreeQuarters)
            {
                cornerRotation = 3;
            }

            if (rotation == 2 && rotation3 == 1 && cornerType2 == StairsBlock.CornerType.ThreeQuarters)
            {
                cornerRotation = 2;
            }

            if (rotation == 3 && rotation3 == 0 && cornerType2 == StairsBlock.CornerType.None)
            {
                cornerRotation = 0;
            }

            if (rotation == 3 && rotation3 == 2 && cornerType2 == StairsBlock.CornerType.None)
            {
                cornerRotation = 3;
            }

            if (rotation == 3 && rotation3 == 2 && cornerType2 == StairsBlock.CornerType.ThreeQuarters)
            {
                cornerRotation = 3;
            }

            if (rotation == 3 && rotation3 == 1 && cornerType2 == StairsBlock.CornerType.ThreeQuarters)
            {
                cornerRotation = 0;
            }
        }

        if (cornerRotation < 0)
        {
            return;
        }

        var data5 = StairsBlock.SetRotation(
            StairsBlock.SetCornerType(data, StairsBlock.CornerType.ThreeQuarters),
            cornerRotation
        );
        var value4 = Terrain.ReplaceData(value, data5);
        SubsystemTerrain.ChangeCellNet(x, y, z, value4, updateModificationCounter);
    }
}
