using Engine.Core;

namespace TerritoryStoneMod;

public sealed class Territory
{
    public Territory(Guid owner, Point3 stonePoint, TerritorySettings? settings = null)
    {
        Owner = owner;
        StonePoint = stonePoint;
        Settings = settings ?? new TerritorySettings();
        if (!Settings.IsValid || stonePoint.Y is < 0 or > 255 ||
            stonePoint.X is < int.MinValue + TerritorySettings.MaximumSize or > int.MaxValue - TerritorySettings.MaximumSize - 1 ||
            stonePoint.Z is < int.MinValue + TerritorySettings.MaximumSize or > int.MaxValue - TerritorySettings.MaximumSize - 1)
        {
            throw new InvalidDataException("Invalid territory size or stone coordinates.");
        }
    }

    private readonly Dictionary<Guid, TerritoryGrant> _grants = new();

    public IReadOnlyDictionary<Guid, TerritoryGrant> Grants => _grants;

    public TerritoryPermission PermissionsFor(Guid player) =>
        _grants.TryGetValue(player, out var grant) ? grant.Permissions : TerritoryPermission.None;

    public void SetGrant(Guid player, string name, TerritoryPermission permissions)
    {
        if (player == Guid.Empty || player == Owner || !TerritoryPermissions.IsValid(permissions))
        {
            throw new InvalidDataException("Invalid territory grant.");
        }
        if (permissions == TerritoryPermission.None)
        {
            _grants.Remove(player);
            return;
        }
        if (!_grants.ContainsKey(player) && _grants.Count >= 128)
        {
            throw new InvalidDataException("Too many territory grants.");
        }
        _grants[player] = new TerritoryGrant(name, permissions);
    }

    public Guid Owner { get; }
    public Point3 StonePoint { get; }
    public TerritorySettings Settings { get; private set; }
    public int MinX => StonePoint.X - Settings.Size;
    public int MaxX => StonePoint.X + Settings.Size + 1;
    public int MinZ => StonePoint.Z - Settings.Size;
    public int MaxZ => StonePoint.Z + Settings.Size + 1;
    public BoundingBox Bounds => new(new Vector3(MinX, 0, MinZ), new Vector3(MaxX, 256, MaxZ));

    internal void ApplySettings(TerritorySettings settings) => Settings = settings;

    public bool Contains(int x, int z) => x >= MinX && x < MaxX && z >= MinZ && z < MaxZ;
    public bool Contains(Vector3 position) =>
        position.X >= MinX && position.X < MaxX && position.Z >= MinZ && position.Z < MaxZ;

    public bool Overlaps(Territory other) =>
        MinX < other.MaxX && MaxX > other.MinX && MinZ < other.MaxZ && MaxZ > other.MinZ;

    public bool BlocksCrossing(Vector3 position) =>
        Contains(position) ? !Settings.AllowExit : !Settings.AllowEntry;

    public IEnumerable<BoundingBox> GetBlockingBoundaryBoxes(float minY, float maxY, Vector3 movement, bool exiting)
    {
        var boxes = GetBoundaryBoxes(minY, maxY, exiting);
        var outwardMovement = new[] { -movement.X, movement.X, -movement.Z, movement.Z };
        for (var face = 0; face < boxes.Length; face++)
        {
            // Only motion into the forbidden side is a collision. Overlap left after
            // an allowed crossing, vertical motion and sliding along a face are not.
            if (exiting ? outwardMovement[face] > 0 : outwardMovement[face] < 0)
            {
                yield return boxes[face];
            }
        }
    }

    public BoundingBox[] GetBoundaryBoxes(float minY, float maxY, bool blockExit = false)
    {
        var minX = blockExit ? MinX - 1f : MinX;
        var maxX = blockExit ? MaxX + 1f : MaxX;
        var minZ = blockExit ? MinZ - 1f : MinZ;
        var maxZ = blockExit ? MaxZ + 1f : MaxZ;
        return
        [
            new(new Vector3(minX, minY, minZ), new Vector3(minX + 1, maxY, maxZ)),
            new(new Vector3(maxX - 1, minY, minZ), new Vector3(maxX, maxY, maxZ)),
            new(new Vector3(minX, minY, minZ), new Vector3(maxX, maxY, minZ + 1)),
            new(new Vector3(minX, minY, maxZ - 1), new Vector3(maxX, maxY, maxZ))
        ];
    }
}
