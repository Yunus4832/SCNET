namespace Game.Terrains;

/// <summary>Authority-owned spawn preparation, separate from approved peer interest.</summary>
public sealed class TerrainSpawnPreparation : IDisposable
{
    private TerrainUpdater? _owner;

    public Guid CharacterId { get; }

    internal TerrainUpdateLocation Location { get; private set; }

    internal TerrainSpawnPreparation(TerrainUpdater owner, Guid characterId, Vector2 center, float radius)
    {
        _owner = owner;
        CharacterId = characterId;
        SetLocation(center, radius);
    }

    public void Update(Vector2 center, float radius) => Owner.UpdateSpawnPreparation(this, center, radius);

    public float GetProgress(float visibilityDistance, float contentDistance) =>
        Owner.GetSpawnPreparationProgress(this, visibilityDistance, contentDistance);

    private TerrainUpdater Owner => _owner ?? throw new ObjectDisposedException(nameof(TerrainSpawnPreparation));

    internal void SetLocation(Vector2 center, float radius) => Location = new TerrainUpdateLocation
    {
        Center = center, VisibilityDistance = radius, ContentDistance = radius
    };

    internal void Invalidate() => Interlocked.Exchange(ref _owner, null);

    public void Dispose() => Interlocked.Exchange(ref _owner, null)?.ReleaseSpawnPreparation(this);
}
