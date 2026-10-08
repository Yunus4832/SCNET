namespace Game.Modding;

public sealed class CellIgnitingContext(SubsystemTerrain terrain, Point3 point, ComponentMiner? miner)
{
    public SubsystemTerrain Terrain { get; } = terrain;
    public Point3 Point { get; } = point;
    public ComponentMiner? Miner { get; } = miner;
    public bool Cancel { get; set; }
}
