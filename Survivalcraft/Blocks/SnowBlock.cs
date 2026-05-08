namespace Game.Blocks;

public class SnowBlock : CubeBlock
{
    public const int Index = 61;

    public BoundingBox[] CollisionBoxes = [new(new Vector3(0f, 0f, 0f), new Vector3(1f, 0.125f, 1f))];

    public override bool IsFaceTransparent(SubsystemTerrain subsystemTerrain, int face, int value)
    {
        return face != 5;
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
        generator.GenerateCubeVertices(
            this,
            value,
            x,
            y,
            z,
            0.125f,
            0.125f,
            0.125f,
            0.125f,
            Color.White,
            Color.White,
            Color.White,
            Color.White,
            Color.White,
            -1,
            geometry.OpaqueSubsetsByFace
        );
    }

    public override void GetDropValues(
        SubsystemTerrain subsystemTerrain,
        int oldValue,
        int newValue,
        int toolLevel,
        List<BlockDropValue> dropValues,
        out bool showDebris
    )
    {
        showDebris = true;
        if (toolLevel < RequiredToolLevel)
        {
            return;
        }

        var num = Random.Int(1, 3);
        for (var i = 0; i < num; i++)
        {
            dropValues.Add(new BlockDropValue
            {
                Value = Terrain.MakeBlockValue(85),
                Count = 1
            });
        }
    }

    public override BoundingBox[] GetCustomCollisionBoxes(SubsystemTerrain terrain, int value)
    {
        return CollisionBoxes;
    }
}
