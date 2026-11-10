using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Network.Chunks;
using Game.Network.Diagnostics;

namespace Game.Subsystems;

public sealed class SubsystemNetworkInterest : Subsystem, IUpdateable
{
    public UpdateExecutionSide UpdateSide => UpdateExecutionSide.Authority;

    private const float _fallbackContentDistance = 64f;

    private NetworkInterestPolicy? _policy;

    private bool _loaded;

    public NetworkInterestPolicy Policy =>
        _policy ?? throw new InvalidOperationException("Network interest policy is not configured.");

    public void ConfigurePolicy(NetworkInterestPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        if (_loaded)
        {
            throw new InvalidOperationException("Network interest policy must be configured before loading the world.");
        }

        _policy = policy;
    }

    private SubsystemCharacters _subsystemCharacters = null!;

    private SubsystemTerrain _subsystemTerrain = null!;

    private SubsystemBlockEntities _subsystemBlockEntities = null!;

    public EntityInterestTracker Entities { get; } = new();

    public NetworkInterestStatistics Statistics { get; private set; }

    private readonly ChunkInterestTracker _chunks = new();

    private readonly SpatialInterestIndex _spatial = new();

    private readonly Dictionary<WorldConnection, TerrainUpdateLocation> _requestedLocations =
        new(ReferenceEqualityComparer.Instance);

    public event Action<WorldConnection, Point2>? ChunkEntered;

    public UpdateOrder UpdateOrder => UpdateOrder.Default - 1;

    public void Update(float dt)
    {
        if (!WorldRuntimeContext.For(Project).IsAuthority || !Time.PeriodicEvent(0.25, 0))
        {
            return;
        }

        var allocatedBytes = GC.GetAllocatedBytesForCurrentThread();
        var started = Stopwatch.GetTimestamp();
        var clients = WorldConnections.For(Project).Members.Values
            .Where(client => client.IsConnected)
            .ToArray();
        Entities.RetainClients(clients);
        foreach (var client in _requestedLocations.Keys.Where(client =>
                     !clients.Any(connected => ReferenceEquals(connected, client))
                 ).ToArray())
        {
            _requestedLocations.Remove(client);
        }

        foreach (var (client, requested) in _requestedLocations)
        {
            if (_subsystemCharacters.Characters.Find(player => ReferenceEquals(player.ControllerConnection, client)) is
                { } player)
            {
                ApplyRequestedLocation(client, player, requested);
            }
        }

        var locatedClients = new List<WorldConnection>();
        foreach (var client in clients)
        {
            if (!ReferenceEquals(client, WorldConnections.For(Project).Self) &&
                TryGetLocation(client, out var center, out var distance))
            {
                _spatial.SetLocation(client, center, distance);
                locatedClients.Add(client);
            }
        }

        _spatial.RetainClients(locatedClients);
        SynchronizeBlockEntities(clients);
        _chunks.RetainClients(clients);
        var relevant = new Dictionary<WorldConnection, HashSet<Point2>>(ReferenceEqualityComparer.Instance);
        foreach (var chunk in _subsystemTerrain.Terrain.AllocatedChunks)
        {
            foreach (var client in GetChunkObservers(chunk.Coords))
            {
                if (!relevant.TryGetValue(client, out var chunks))
                {
                    chunks = [];
                    relevant.Add(client, chunks);
                }

                chunks.Add(chunk.Coords);
            }
        }

        foreach (var client in clients.Where(client =>
                     client != WorldConnections.For(Project).Self &&
                     client.State == ClientState.Playing
                 ))
        {
            var current = relevant.TryGetValue(client, out var chunks) ? chunks : [];
            var changes = _chunks.Synchronize(client, current);
            foreach (var chunk in changes.Left)
            {
                NetworkSender.SendTo(client, new ChunkStateResetPackage(chunk));
            }

            foreach (var chunk in changes.Entered)
            {
                NetworkSender.SendTo(client, new ChunkStateResetPackage(chunk));
                ChunkEntered?.Invoke(client, chunk);
            }
        }

        Statistics = Statistics.AddSample(
            Stopwatch.GetElapsedTime(started).TotalMilliseconds,
            GC.GetAllocatedBytesForCurrentThread() - allocatedBytes
        );
    }

    public override void Load(ValuesDictionary valuesDictionary)
    {
        _ = Policy;
        _loaded = true;
        _subsystemCharacters = Project.FindSubsystem<SubsystemCharacters>(true)!;
        _subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true)!;
        _subsystemBlockEntities = Project.FindSubsystem<SubsystemBlockEntities>(true)!;
    }

    public bool SetRequestedLocation(WorldConnection client, CharacterData player, TerrainUpdateLocation requested)
    {
        if (!ApplyRequestedLocation(client, player, requested))
        {
            return false;
        }

        _requestedLocations[client] = requested;
        return true;
    }

    private bool ApplyRequestedLocation(WorldConnection client, CharacterData player, TerrainUpdateLocation requested)
    {
        if (!NetworkTerrainPolicy.TryClampClientUpdateLocation(
                requested,
                Policy.MaxClientVisibilityRange,
                player.ComponentCharacter?.ComponentBody.Position.XZ ?? player.SpawnPosition.XZ,
                out var approved
            ))
        {
            return false;
        }

        var demands = _subsystemTerrain.Demands;
        // Reconcile the original camera request after movement arrives on another channel.
        // LastChunksUpdateCenter remains owned by the authority demand manager.
        demands.SetLocation(
            player.CharacterIndex,
            approved.Center,
            approved.VisibilityDistance,
            approved.ContentDistance
        );
        _spatial.SetLocation(client, approved.Center, approved.ContentDistance);
        var distribution = _subsystemTerrain.ChunkDistribution;
        if (distribution != null &&
            (!distribution.TryGetClientLocation(client, out var center, out var distance) ||
             center != approved.Center ||
             distance != approved.ContentDistance))
        {
            distribution.UpdateClientLocation(client, approved.Center, approved.ContentDistance);
        }

        return true;
    }

    private void SynchronizeBlockEntities(IEnumerable<WorldConnection> clients)
    {
        var blocks = _subsystemBlockEntities.BlockEntities.Values
            .ToDictionary(block => block.Entity.EntityId);
        var relevant = new Dictionary<WorldConnection, HashSet<int>>(ReferenceEqualityComparer.Instance);
        foreach (var block in blocks.Values)
        {
            foreach (var client in GetChunkObservers(new Point2(block.Coordinates.X >> 4, block.Coordinates.Z >> 4)))
            {
                if (!relevant.TryGetValue(client, out var ids))
                {
                    ids = [];
                    relevant.Add(client, ids);
                }

                ids.Add(block.Entity.EntityId);
            }
        }

        foreach (var client in clients.Where(client => client.State == ClientState.Playing &&
                                                       !ReferenceEquals(
                                                           client,
                                                           WorldConnections.For(Project).Self
                                                       )
                 ))
        {
            var current = relevant.TryGetValue(client, out var ids) ? ids : [];
            var changes = Entities.Synchronize(
                client,
                EntityInterestGroup.BlockEntities,
                current,
                blocks.ContainsKey,
                1
            );
            if (changes.Entered.Count > 0)
            {
                NetworkSender.SendTo(
                    client,
                    EntityPackageBuilder.Capture(Project, changes.Entered.Select(id => blocks[id].Entity).ToList())
                );
            }

            foreach (var id in changes.Left)
            {
                NetworkSender.SendTo(client, new EntityRemovedPackage { EntityId = id });
            }
        }
    }

    public bool IsPositionRelevant(WorldConnection client, Vector2 position)
    {
        if (!TryGetLocation(client, out var center, out var contentDistance))
        {
            return false;
        }

        return Vector2.DistanceSquared(center, position) <= MathUtils.Sqr(contentDistance);
    }

    public IEnumerable<WorldConnection> GetObservers(Vector2 position)
    {
        return _spatial.GetObservers(position).Where(client => client.IsConnected &&
                                                               !ReferenceEquals(
                                                                   client,
                                                                   WorldConnections.For(Project).Self
                                                               )
        );
    }

    internal Dictionary<WorldConnection, HashSet<int>> GetPointCandidates(
        IEnumerable<(int Id, Vector2 Position)> points
    )
    {
        return _spatial.GetPointCandidates(points);
    }

    public IEnumerable<WorldConnection> GetObserversWithin(Vector2 position, float radius)
    {
        return _spatial.GetObserversWithin(position, radius).Where(client => client.IsConnected &&
                                                                             !ReferenceEquals(
                                                                                 client,
                                                                                 WorldConnections.For(Project).Self
                                                                             )
        );
    }

    public IEnumerable<WorldConnection> GetChunkObservers(Point2 chunk)
    {
        return _spatial.GetChunkObservers(chunk).Where(client => client.IsConnected &&
                                                                 !ReferenceEquals(
                                                                     client,
                                                                     WorldConnections.For(Project).Self
                                                                 )
        );
    }

    private bool IsChunkRelevant(WorldConnection client, Point2 chunk)
    {
        if (!TryGetLocation(client, out var center, out var contentDistance))
        {
            return false;
        }

        return NetworkTerrainPolicy.IsTerrainChunkRelevant(chunk, center, contentDistance);
    }

    public bool ShouldIncludeInInitialSnapshot(WorldConnection client, Entity entity)
    {
        if (entity.FindComponent<ComponentBlockEntity>() is { } block)
        {
            // Bootstrap can precede the first shared indexing tick; authorize its single recipient directly.
            return IsChunkRelevant(client, new Point2(block.Coordinates.X >> 4, block.Coordinates.Z >> 4));
        }

        var body = entity.FindComponent<ComponentBody>();
        return body is null || IsBodyRelevant(client, body);
    }

    public void EnsureBlockEntityObserved(WorldConnection client, ComponentBlockEntity block)
    {
        if (Entities.GetObservers(EntityInterestGroup.BlockEntities, block.Entity.EntityId)
            .Any(observer => ReferenceEquals(observer, client)))
        {
            return;
        }

        NetworkSender.SendTo(client, EntityPackageBuilder.Capture(block.Entity));
        Entities.AddObserved(client, EntityInterestGroup.BlockEntities, block.Entity.EntityId);
    }

    public void EnsureBodyGroupObserved(WorldConnection client, ComponentBody body)
    {
        var missing = GetBodyGroup(body)
            .Where(member => !Entities.GetObservers(EntityInterestGroup.Creatures, member.Entity.EntityId)
                .Any(observer => ReferenceEquals(observer, client))
            )
            .Select(member => member.Entity)
            .ToList();
        if (missing.Count == 0)
        {
            return;
        }

        NetworkSender.SendTo(client, EntityPackageBuilder.Capture(Project, missing));
        foreach (var entity in missing)
        {
            Entities.AddObserved(client, EntityInterestGroup.Creatures, entity.EntityId);
        }
    }

    public bool IsBodyRelevant(WorldConnection client, ComponentBody body)
    {
        return GetBodyGroup(body).Any(member =>
            ReferenceEquals(member.Character?.CharacterData.ControllerConnection, client) ||
            IsPositionRelevant(client, member.Position.XZ)
        );
    }

    internal IEnumerable<WorldConnection> GetBodyCandidates(ComponentBody body)
    {
        var candidates = new HashSet<WorldConnection>(ReferenceEqualityComparer.Instance);
        foreach (var member in GetBodyGroup(body))
        {
            candidates.UnionWith(_spatial.GetObservers(member.Position.XZ));
            if (member.Character?.CharacterData.ControllerConnection is { } owner)
            {
                candidates.Add(owner);
            }
        }

        return candidates;
    }

    public static IEnumerable<ComponentBody> GetBodyGroup(ComponentBody body)
    {
        var root = body;
        while (root.ParentBody is not null)
        {
            root = root.ParentBody;
        }

        return GetBodyTree(root);
    }

    private static IEnumerable<ComponentBody> GetBodyTree(ComponentBody body)
    {
        yield return body;
        foreach (var child in body.ChildBodies)
        {
            foreach (var member in GetBodyTree(child))
            {
                yield return member;
            }
        }
    }

    public void SeedInitialSnapshot(WorldConnection client, IEnumerable<Entity> entities)
    {
        var creatureIds = entities
            .Select(entity => entity.FindComponent<ComponentBody>())
            .Where(body => body is not null)
            .Select(body => body!.Entity.EntityId);
        Entities.Seed(client, EntityInterestGroup.Creatures, creatureIds);
        Entities.Seed(
            client,
            EntityInterestGroup.BlockEntities,
            entities
                .Where(entity => entity.FindComponent<ComponentBlockEntity>() is not null)
                .Select(entity => entity.EntityId)
        );
        Entities.Seed(client, EntityInterestGroup.Pickables, []);
        Entities.Seed(client, EntityInterestGroup.Projectiles, []);
        Entities.Seed(client, EntityInterestGroup.MovingBlocks, []);
        Entities.Seed(client, EntityInterestGroup.Inventories, []);
    }

    private bool TryGetLocation(WorldConnection client, out Vector2 center, out float contentDistance)
    {
        if (_subsystemTerrain.ChunkDistribution is { } distribution &&
            distribution.TryGetClientLocation(client, out center, out contentDistance))
        {
            return true;
        }

        var characterData =
            _subsystemCharacters.Characters.Find(player => ReferenceEquals(player.ControllerConnection, client));
        if (characterData is null)
        {
            center = default;
            contentDistance = 0f;
            return false;
        }

        if (_subsystemTerrain.Demands.Locations.TryGetValue(
                characterData.CharacterIndex,
                out var updateLocation
            ))
        {
            center = updateLocation.Center;
            contentDistance = updateLocation.ContentDistance;
            return true;
        }

        if (characterData.ComponentCharacter is not { } player)
        {
            center = default;
            contentDistance = 0f;
            return false;
        }

        center = player.ComponentBody.Position.XZ;
        contentDistance = _fallbackContentDistance;
        return true;
    }
}
