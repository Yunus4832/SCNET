using System.Collections.ObjectModel;

using Game.Terrains.Distribution;

namespace Game.Terrains;

/// <summary>Owns world-local locations and temporary spawn demand, independently of execution.</summary>
public sealed class TerrainDemandManager(Terrain terrain, TerrainContentRole role) : IDisposable
{
    private readonly Terrain _terrain = terrain ?? throw new ArgumentNullException(nameof(terrain));

    private readonly Dictionary<int, TerrainUpdateLocation?> _pending = [];

    private readonly Dictionary<Guid, TerrainSpawnPreparation> _preparations = [];

    private IReadOnlyDictionary<int, TerrainUpdateLocation> _locations =
        new ReadOnlyDictionary<int, TerrainUpdateLocation>(new Dictionary<int, TerrainUpdateLocation>());

    private long _revision;

    private long _reconciledRevision;

    private bool _disposed;

    public IReadOnlyDictionary<int, TerrainUpdateLocation> Locations => _locations;

    public bool HasChanges => _revision != _reconciledRevision;

    public int PendingLocationCount => _pending.Count;

    internal sealed record Snapshot(
        long Revision,
        IReadOnlyDictionary<int, TerrainUpdateLocation> Locations,
        TerrainUpdateLocation[] EffectiveLocations
    );

    public bool SetLocation(int index, Vector2 center, float visibility, float content)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ValidateCenter(center);
        if (!float.IsFinite(visibility) || visibility < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(visibility));
        }

        if (!float.IsFinite(content) || content < 0) { throw new ArgumentOutOfRangeException(nameof(content)); }

        content = MathUtils.Max(content, visibility);
        var location = ResolveLocation(index, _locations, _pending);
        if (content.CloseTo(location.ContentDistance) &&
            visibility.CloseTo(location.VisibilityDistance) &&
            location.LastChunksUpdateCenter.HasValue &&
            location.Center == center) { return false; }

        location.Center = center;
        location.VisibilityDistance = visibility;
        location.ContentDistance = content;
        location.LastChunksUpdateCenter = center;
        _pending[index] = location;
        _revision++;
        return true;
    }

    public TerrainUpdateLocation GetRequestedLocation(int index) => ResolveLocation(index, _locations, _pending);

    public void RemoveLocation(int index)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _pending[index] = null;
        _revision++;
    }

    public bool IsLocationCovered(int index, Vector2 center, float radius)
    {
        if (_pending.TryGetValue(index, out var pending)) { return pending is { } queued && Covers(queued); }

        return _locations.TryGetValue(index, out var current) && Covers(current);

        bool Covers(TerrainUpdateLocation location) =>
            Vector2.Distance(location.Center, center) + radius <= location.ContentDistance;
    }

    public TerrainSpawnPreparation BeginSpawnPreparation(Guid characterId, Vector2 center, float radius)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (role != TerrainContentRole.Authority)
        {
            throw new InvalidOperationException("Spawn preparation requires authority terrain.");
        }

        if (characterId == Guid.Empty)
        {
            throw new ArgumentException("A character owner is required.", nameof(characterId));
        }

        ValidatePreparation(center, radius);
        if (_preparations.ContainsKey(characterId))
        {
            throw new InvalidOperationException("Character already owns a spawn preparation.");
        }

        var preparation = new TerrainSpawnPreparation(this, characterId, center, radius);
        _preparations.Add(characterId, preparation);
        _revision++;
        return preparation;
    }

    internal void UpdateSpawnPreparation(TerrainSpawnPreparation preparation, Vector2 center, float radius)
    {
        RequirePreparation(preparation);
        ValidatePreparation(center, radius);
        if (preparation.Location.Center == center && preparation.Location.ContentDistance == radius) { return; }

        preparation.SetLocation(center, radius);
        _revision++;
    }

    internal void ReleaseSpawnPreparation(TerrainSpawnPreparation preparation)
    {
        if (_preparations.GetValueOrDefault(preparation.CharacterId) != preparation) { return; }

        _preparations.Remove(preparation.CharacterId);
        _revision++;
    }

    internal float GetSpawnPreparationProgress(TerrainSpawnPreparation preparation, float visibility, float content)
    {
        RequirePreparation(preparation);
        return GetProgress(preparation.Location, visibility, content);
    }

    public float GetProgress(int index, float visibility, float content)
    {
        return _locations.TryGetValue(index, out var location) ? GetProgress(location, visibility, content) : 0f;
    }

    private float GetProgress(TerrainUpdateLocation location, float visibility, float content)
    {
        visibility = MathUtils.Max(MathUtils.Min(visibility, location.VisibilityDistance) - 8.1f, 0f);
        content = MathUtils.Max(MathUtils.Min(content, location.ContentDistance) - 8.1f, 0f);
        var distance = MathUtils.Max(visibility, content);
        var minimum = Terrain.ToChunk(location.Center - new Vector2(distance));
        var maximum = Terrain.ToChunk(location.Center + new Vector2(distance));
        var ready = 0;
        var missing = 0;
        for (var x = minimum.X; x <= maximum.X; x++)
        {
            for (var z = minimum.Y; z <= maximum.Y; z++)
            {
                var squared = Vector2.DistanceSquared(location.Center, new Vector2((x + 0.5f) * 16f, (z + 0.5f) * 16f));
                var chunk = _terrain.GetChunkAtCoords(x, z);
                if (visibility > 0 && squared <= visibility * visibility)
                {
                    if (chunk is { HasCurrentLight: true }) { ready++; }
                    else { missing++; }
                }
                else if (content > 0 && squared <= content * content)
                {
                    if (chunk is { HasContents: true }) { ready++; }
                    else { missing++; }
                }
            }
        }

        return missing == 0 ? 1f : ready / (float)(ready + missing);
    }

    internal Snapshot CapturePending()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var locations = new Dictionary<int, TerrainUpdateLocation>(_locations);
        foreach (var (index, pending) in _pending)
        {
            if (pending is { } location) { locations[index] = location; }
            else { locations.Remove(index); }
        }

        var readOnly = new ReadOnlyDictionary<int, TerrainUpdateLocation>(locations);
        return new Snapshot(
            _revision,
            readOnly,
            locations.Values.Concat(_preparations.Values.Select(p => p.Location)).ToArray()
        );
    }

    internal void Apply(Snapshot snapshot) => _locations = snapshot.Locations;

    internal void Reconciled(Snapshot snapshot)
    {
        // A callback may change demand while residency is being reconciled.
        if (snapshot.Revision != _revision) { return; }

        _pending.Clear();
        _reconciledRevision = snapshot.Revision;
    }

    internal TerrainUpdateLocation[] CaptureEffectiveLocations() => CapturePending().EffectiveLocations;

    internal static TerrainUpdateLocation ResolveLocation(
        int index,
        IReadOnlyDictionary<int, TerrainUpdateLocation> current,
        IReadOnlyDictionary<int, TerrainUpdateLocation?> pending
    )
    {
        return pending.TryGetValue(index, out var queued)
            ? queued.GetValueOrDefault()
            : current.GetValueOrDefault(index);
    }

    public void Dispose()
    {
        if (_disposed) { return; }

        _disposed = true;
        foreach (var preparation in _preparations.Values) { preparation.Invalidate(); }

        _preparations.Clear();
        _pending.Clear();
        _locations = new ReadOnlyDictionary<int, TerrainUpdateLocation>(new Dictionary<int, TerrainUpdateLocation>());
    }

    private void RequirePreparation(TerrainSpawnPreparation preparation)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_preparations.GetValueOrDefault(preparation.CharacterId) != preparation)
        {
            throw new InvalidOperationException("Spawn preparation no longer belongs to this terrain lifetime.");
        }
    }

    private static void ValidateCenter(Vector2 center)
    {
        if (!float.IsFinite(center.X) || !float.IsFinite(center.Y))
        {
            throw new ArgumentOutOfRangeException(nameof(center));
        }
    }

    private static void ValidatePreparation(Vector2 center, float radius)
    {
        ValidateCenter(center);
        if (!float.IsFinite(radius) || radius <= 0) { throw new ArgumentOutOfRangeException(nameof(radius)); }
    }
}
