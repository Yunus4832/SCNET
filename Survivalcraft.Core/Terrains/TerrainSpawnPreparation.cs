namespace Game.Terrains;

/// <summary>由权威端持有的出生准备范围，独立于已批准的对端关注范围。</summary>
public sealed class TerrainSpawnPreparation : IDisposable
{
    private TerrainDemandManager? _owner;

    public Guid CharacterId { get; }

    internal TerrainUpdateLocation Location { get; private set; }

    internal TerrainSpawnPreparation(TerrainDemandManager owner, Guid characterId, Vector2 center, float radius)
    {
        _owner = owner;
        CharacterId = characterId;
        SetLocation(center, radius);
    }

    public void Update(Vector2 center, float radius) => Owner.UpdateSpawnPreparation(this, center, radius);

    public float GetProgress(float visibilityDistance, float contentDistance) =>
        Owner.GetSpawnPreparationProgress(this, visibilityDistance, contentDistance);

    private TerrainDemandManager Owner => _owner ?? throw new ObjectDisposedException(nameof(TerrainSpawnPreparation));

    internal void SetLocation(Vector2 center, float radius) => Location = new TerrainUpdateLocation
    {
        Center = center, VisibilityDistance = radius, ContentDistance = radius
    };

    internal void Invalidate() => Interlocked.Exchange(ref _owner, null);

    public void Dispose() => Interlocked.Exchange(ref _owner, null)?.ReleaseSpawnPreparation(this);
}
