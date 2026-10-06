using System.Diagnostics;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Network;
using Game.Network.Enums;
using Game.Network.Packages;

namespace Game.Subsystems;

public sealed class SubsystemNetworkInterest : Subsystem, IUpdateable
{
    private const float _fallbackContentDistance = 64f;

    private SubsystemPlayers _subsystemPlayers = null!;

    private SubsystemTerrain _subsystemTerrain = null!;

    private SubsystemBlockEntities _subsystemBlockEntities = null!;

    public EntityInterestTracker Entities { get; } = new();

    public NetworkInterestStatistics Statistics { get; private set; }

    private readonly ChunkInterestTracker _chunks = new();

    private readonly SpatialInterestIndex _spatial = new();

    private readonly Dictionary<Client, TerrainUpdater.UpdateLocation> _requestedLocations =
        new(ReferenceEqualityComparer.Instance);

    public event Action<Client, Point2>? ChunkEntered;

    public UpdateOrder UpdateOrder => UpdateOrder.Default - 1;

    public void Update(float dt)
    {
        if (CommonLib.WorkType != WorkType.Server || !Time.PeriodicEvent(0.25, 0))
        {
            return;
        }

        var allocatedBytes = GC.GetAllocatedBytesForCurrentThread();
        var started = Stopwatch.GetTimestamp();
        var clients = CommonLib.Net.Clients.Values.Where(client => client.IsConnected).ToArray();
        Entities.RetainClients(clients);
        foreach (var client in _requestedLocations.Keys.Where(client =>
                     !clients.Any(connected => ReferenceEquals(connected, client))).ToArray())
        {
            _requestedLocations.Remove(client);
        }

        foreach (var (client, requested) in _requestedLocations)
        {
            if (_subsystemPlayers.PlayersData.Find(player => ReferenceEquals(player.Client, client)) is { } player)
            {
                ApplyRequestedLocation(client, player, requested);
            }
        }

        var locatedClients = new List<Client>();
        foreach (var client in clients)
        {
            if (!ReferenceEquals(client, CommonLib.Net.Self) &&
                TryGetLocation(client, out var center, out var distance))
            {
                _spatial.SetLocation(client, center, distance);
                locatedClients.Add(client);
            }
        }

        _spatial.RetainClients(locatedClients);
        SynchronizeBlockEntities(clients);
        _chunks.RetainClients(clients);
        var relevant = new Dictionary<Client, HashSet<Point2>>(ReferenceEqualityComparer.Instance);
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

        foreach (var client in clients.Where(client => client != CommonLib.Net.Self &&
                                                       client.State == ClientState.Playing))
        {
            var current = relevant.TryGetValue(client, out var chunks) ? chunks : [];
            var changes = _chunks.Synchronize(client, current);
            foreach (var chunk in changes.Left)
            {
                NetworkSender.SendTo(client, new ChunkStateResetPackage { Chunk = chunk });
            }

            foreach (var chunk in changes.Entered)
            {
                NetworkSender.SendTo(client, new ChunkStateResetPackage { Chunk = chunk });
                ChunkEntered?.Invoke(client, chunk);
            }
        }

        Statistics = Statistics.AddSample(Stopwatch.GetElapsedTime(started).TotalMilliseconds,
            GC.GetAllocatedBytesForCurrentThread() - allocatedBytes);
    }

    public override void Load(ValuesDictionary valuesDictionary)
    {
        _subsystemPlayers = Project.FindSubsystem<SubsystemPlayers>(true)!;
        _subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true)!;
        _subsystemBlockEntities = Project.FindSubsystem<SubsystemBlockEntities>(true)!;
    }

    public bool SetRequestedLocation(Client client, PlayerData player, TerrainUpdater.UpdateLocation requested)
    {
        if (!ApplyRequestedLocation(client, player, requested))
        {
            return false;
        }

        _requestedLocations[client] = requested;
        return true;
    }

    private bool ApplyRequestedLocation(Client client, PlayerData player, TerrainUpdater.UpdateLocation requested)
    {
        if (!NetworkTerrainPolicy.TryClampClientUpdateLocation(requested,
                SettingsManager.Current.MaxClientVisibilityRange,
                player.ComponentPlayer?.ComponentBody.Position.XZ ?? player.SpawnPosition.XZ, out var approved))
        {
            return false;
        }

        var updater = _subsystemTerrain.TerrainUpdater;
        // Reconcile the original camera request after movement arrives on another channel.
        // LastChunksUpdateCenter remains owned by the server updater.
        updater.SetUpdateLocation(player.PlayerIndex, approved.Center,
            approved.VisibilityDistance, approved.ContentDistance);
        _spatial.SetLocation(client, approved.Center, approved.ContentDistance);
        var distribution = updater.ServerChunkDistribution;
        if (distribution != null &&
            (!distribution.TryGetClientLocation(client, out var center, out var distance) ||
             center != approved.Center || distance != approved.ContentDistance))
        {
            distribution.UpdateClientLocation(client, approved.Center, approved.ContentDistance);
        }

        return true;
    }

    private void SynchronizeBlockEntities(IEnumerable<Client> clients)
    {
        var blocks = _subsystemBlockEntities.BlockEntities.Values
            .ToDictionary(block => block.Entity.EntityId);
        var relevant = new Dictionary<Client, HashSet<int>>(ReferenceEqualityComparer.Instance);
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
                                                       !ReferenceEquals(client, CommonLib.Net.Self)))
        {
            var current = relevant.TryGetValue(client, out var ids) ? ids : [];
            var changes = Entities.Synchronize(client, EntityInterestGroup.BlockEntities, current,
                blocks.ContainsKey, 1);
            if (changes.Entered.Count > 0)
            {
                NetworkSender.SendTo(client, new EntityPackage(changes.Entered.Select(id => blocks[id].Entity).ToList()));
            }

            foreach (var id in changes.Left)
            {
                NetworkSender.SendTo(client, new EntityPackage(id));
            }
        }
    }

    public bool IsPositionRelevant(Client client, Vector2 position)
    {
        if (!TryGetLocation(client, out var center, out var contentDistance))
        {
            return false;
        }

        return Vector2.DistanceSquared(center, position) <= MathUtils.Sqr(contentDistance);
    }

    public IEnumerable<Client> GetObservers(Vector2 position)
    {
        return _spatial.GetObservers(position).Where(client => client.IsConnected &&
            !ReferenceEquals(client, CommonLib.Net.Self));
    }

    internal Dictionary<Client, HashSet<int>> GetPointCandidates(IEnumerable<(int Id, Vector2 Position)> points)
    {
        return _spatial.GetPointCandidates(points);
    }

    public IEnumerable<Client> GetObserversWithin(Vector2 position, float radius)
    {
        return _spatial.GetObserversWithin(position, radius).Where(client => client.IsConnected &&
            !ReferenceEquals(client, CommonLib.Net.Self));
    }

    public IEnumerable<Client> GetChunkObservers(Point2 chunk)
    {
        return _spatial.GetChunkObservers(chunk).Where(client => client.IsConnected &&
            !ReferenceEquals(client, CommonLib.Net.Self));
    }

    private bool IsChunkRelevant(Client client, Point2 chunk)
    {
        if (!TryGetLocation(client, out var center, out var contentDistance))
        {
            return false;
        }

        return NetworkTerrainPolicy.IsChunkRelevant(chunk, center, contentDistance);
    }

    public bool ShouldIncludeInInitialSnapshot(Client client, Entity entity)
    {
        if (entity.FindComponent<ComponentBlockEntity>() is { } block)
        {
            // Bootstrap can precede the first shared indexing tick; authorize its single recipient directly.
            return IsChunkRelevant(client, new Point2(block.Coordinates.X >> 4, block.Coordinates.Z >> 4));
        }

        var body = entity.FindComponent<ComponentBody>();
        return body is null || IsBodyRelevant(client, body);
    }

    internal void EnsureBlockEntityObserved(Client client, ComponentBlockEntity block)
    {
        if (Entities.GetObservers(EntityInterestGroup.BlockEntities, block.Entity.EntityId)
            .Any(observer => ReferenceEquals(observer, client)))
        {
            return;
        }

        NetworkSender.SendTo(client, new EntityPackage(block.Entity));
        Entities.AddObserved(client, EntityInterestGroup.BlockEntities, block.Entity.EntityId);
    }

    internal void EnsureBodyGroupObserved(Client client, ComponentBody body)
    {
        var missing = GetBodyGroup(body)
            .Where(member => !Entities.GetObservers(EntityInterestGroup.Creatures, member.Entity.EntityId)
                .Any(observer => ReferenceEquals(observer, client)))
            .Select(member => member.Entity)
            .ToList();
        if (missing.Count == 0)
        {
            return;
        }

        NetworkSender.SendTo(client, new EntityPackage(missing));
        foreach (var entity in missing)
        {
            Entities.AddObserved(client, EntityInterestGroup.Creatures, entity.EntityId);
        }
    }

    public bool IsBodyRelevant(Client client, ComponentBody body)
    {
        return GetBodyGroup(body).Any(member =>
            ReferenceEquals(member.Player?.PlayerData.Client, client) ||
            IsPositionRelevant(client, member.Position.XZ));
    }

    internal IEnumerable<Client> GetBodyCandidates(ComponentBody body)
    {
        var candidates = new HashSet<Client>(ReferenceEqualityComparer.Instance);
        foreach (var member in GetBodyGroup(body))
        {
            candidates.UnionWith(_spatial.GetObservers(member.Position.XZ));
            if (member.Player?.PlayerData.Client is { } owner)
            {
                candidates.Add(owner);
            }
        }

        return candidates;
    }

    internal static IEnumerable<ComponentBody> GetBodyGroup(ComponentBody body)
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

    public void SeedInitialSnapshot(Client client, IEnumerable<Entity> entities)
    {
        var creatureIds = entities
            .Select(entity => entity.FindComponent<ComponentBody>())
            .Where(body => body is not null)
            .Select(body => body!.Entity.EntityId);
        Entities.Seed(client, EntityInterestGroup.Creatures, creatureIds);
        Entities.Seed(client, EntityInterestGroup.BlockEntities, entities
            .Where(entity => entity.FindComponent<ComponentBlockEntity>() is not null)
            .Select(entity => entity.EntityId));
        Entities.Seed(client, EntityInterestGroup.Pickables, []);
        Entities.Seed(client, EntityInterestGroup.Projectiles, []);
        Entities.Seed(client, EntityInterestGroup.MovingBlocks, []);
        Entities.Seed(client, EntityInterestGroup.Inventories, []);
    }

    private bool TryGetLocation(Client client, out Vector2 center, out float contentDistance)
    {
        if (_subsystemTerrain.TerrainUpdater.ServerChunkDistribution is { } distribution &&
            distribution.TryGetClientLocation(client, out center, out contentDistance))
        {
            return true;
        }

        var playerData = _subsystemPlayers.PlayersData.Find(player => ReferenceEquals(player.Client, client));
        if (playerData is null)
        {
            center = default;
            contentDistance = 0f;
            return false;
        }

        if (_subsystemTerrain.TerrainUpdater.UpdateLocations.TryGetValue(
                playerData.PlayerIndex,
                out var updateLocation))
        {
            center = updateLocation.Center;
            contentDistance = updateLocation.ContentDistance;
            return true;
        }

        if (playerData.ComponentPlayer is not { } player)
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
