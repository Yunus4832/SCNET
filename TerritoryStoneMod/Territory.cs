using Engine.Core;

using Game.Terrains;

namespace TerritoryStoneMod;

public sealed class Territory(Guid owner, Point3 stonePoint)
{
    public Guid Owner { get; } = owner;
    public Point3 StonePoint { get; } = stonePoint;
    public Point2 Origin { get; } = Terrain.ToChunk(stonePoint.X, stonePoint.Z) * 16;
    public bool ApplyToTeam { get; set; }
    public bool ShowBoundary { get; set; } = true;
    public bool RestrictEntry { get; set; } = true;

    public bool Contains(int x, int z) =>
        x >= Origin.X - 16 && x <= Origin.X + 16 && z >= Origin.Y - 16 && z <= Origin.Y + 16;

    public bool IsBorder(int x, int z) => Contains(x, z) &&
        (x == Origin.X - 16 || x == Origin.X + 16 || z == Origin.Y - 16 || z == Origin.Y + 16);

    public bool Contains(Vector3 position) => Contains(Terrain.ToCell(position.X), Terrain.ToCell(position.Z));

    public bool Overlaps(Territory other) =>
        Math.Abs(Origin.X - other.Origin.X) <= 32 && Math.Abs(Origin.Y - other.Origin.Y) <= 32;

    public BoundingBox[] GetBoundaryBoxes(float minY, float maxY) =>
    [
        new(new Vector3(Origin.X - 16, minY, Origin.Y - 16),
            new Vector3(Origin.X - 15, maxY, Origin.Y + 17)),
        new(new Vector3(Origin.X + 16, minY, Origin.Y - 16),
            new Vector3(Origin.X + 17, maxY, Origin.Y + 17)),
        new(new Vector3(Origin.X - 15, minY, Origin.Y - 16),
            new Vector3(Origin.X + 16, maxY, Origin.Y - 15)),
        new(new Vector3(Origin.X - 15, minY, Origin.Y + 16),
            new Vector3(Origin.X + 16, maxY, Origin.Y + 17))
    ];
}
