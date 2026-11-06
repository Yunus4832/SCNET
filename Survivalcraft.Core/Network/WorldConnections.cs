using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;

using EntitySystem.Core;

using Game.Protocol.Identity;

namespace Game.Network;

/// <summary>World-owned logical roster and authority link. Physical registration must commit through this registry.</summary>
public sealed class WorldConnections : IDisposable
{
    private static readonly ConditionalWeakTable<WorldRuntimeContext, WorldConnections> _bindings = new();

    private readonly Dictionary<int, WorldConnection> _members = [];

    private readonly CancellationTokenRegistration _closed;

    private bool _disposed;

    private long _lastGeneration;

    private readonly Dictionary<byte, (long Generation, Guid Epoch, WorldConnection Record, bool Removed)> _lifetimes =
        [];


    public WorldRuntimeContext World { get; }

    public IReadOnlyDictionary<int, WorldConnection> Members { get; }

    public int PlayerCount => _members.Values.Count(connection => connection.PlayerAddress is not null);

    public WorldConnection? Self { get; private set; }

    public WorldConnection? Authority { get; private set; }

    public WorldConnections(WorldRuntimeContext world)
    {
        ObjectDisposedException.ThrowIf(world.IsClosed, world);
        World = world;
        Members = new ReadOnlyDictionary<int, WorldConnection>(_members);
        _bindings.Add(world, this);
        try
        {
            world.OwnWorldResource(this);
            _closed = world.Lifetime.Register(Clear);
        }
        catch
        {
            _bindings.Remove(world);
            throw;
        }
    }

    public static WorldConnections For(WorldRuntimeContext world)
    {
        return _bindings.TryGetValue(world, out var connections)
            ? connections
            : throw new InvalidOperationException("World has no logical connection registry.");
    }

    public static WorldConnections For(Project project) => For(WorldRuntimeContext.For(project));

    public static bool TryGet(WorldRuntimeContext world, out WorldConnections? connections)
    {
        return _bindings.TryGetValue(world, out connections);
    }

    private void Validate(WorldConnection connection)
    {
        ObjectDisposedException.ThrowIf(World.IsClosed || _disposed, this);
        if (connection.World is not null && !ReferenceEquals(connection.World, World))
        {
            throw new InvalidOperationException("A connection cannot belong to two worlds.");
        }
    }

    public void BeginConnectionLifetime(WorldConnection connection)
    {
        Validate(connection);
        if (!World.IsAuthority)
        {
            throw new InvalidOperationException("Only authority allocates connection lifetimes.");
        }

        if (_members.TryGetValue(connection.ID, out var occupant) && !ReferenceEquals(occupant, connection))
        {
            throw new InvalidOperationException("Retire the previous occupant before allocating a new lifetime.");
        }

        if (connection.ConnectionGeneration != 0)
        {
            if (_lifetimes.TryGetValue(connection.ID, out var current) &&
                ReferenceEquals(current.Record, connection) &&
                !current.Removed) { return; }

            throw new InvalidOperationException("A retired connection record cannot begin another lifetime.");
        }

        connection.ConnectionGeneration = checked(++_lastGeneration);
        connection.ConnectionEpoch = Guid.NewGuid();
        connection.World = World;
        _lifetimes[connection.ID] = (connection.ConnectionGeneration, connection.ConnectionEpoch, connection, false);
    }

    public bool TryInstallController(
        CharacterControllerSnapshot snapshot,
        out WorldConnection? connection,
        WorldConnection? initialSelf = null
    )
    {
        connection = null;
        ObjectDisposedException.ThrowIf(World.IsClosed || _disposed, this);
        if (World.IsAuthority ||
            snapshot.ConnectionId == 0 ||
            snapshot.ConnectionEpoch == Guid.Empty ||
            snapshot.ConnectionGeneration <= 0) { return false; }

        if (_lifetimes.TryGetValue(snapshot.ConnectionId, out var previous))
        {
            if (snapshot.ConnectionGeneration < previous.Generation ||
                snapshot.ConnectionGeneration == previous.Generation && previous.Removed) { return false; }

            if (snapshot.ConnectionGeneration == previous.Generation)
            {
                var record = previous.Record;
                if (snapshot.ConnectionEpoch != previous.Epoch ||
                    record.PlayerAddress != snapshot.PlayerAddress ||
                    record.CharacterId != Guid.Empty && record.CharacterId != snapshot.CharacterId) { return false; }

                record.SetControlledCharacter(snapshot.CharacterId);
                connection = record;
                return true;
            }
        }

        var created = initialSelf is { ConnectionGeneration: 0 } &&
                      ReferenceEquals(Self, initialSelf) &&
                      initialSelf.ID == snapshot.ConnectionId
            ? initialSelf
            : new WorldConnection(snapshot.ConnectionId, snapshot.CharacterId, snapshot.PlayerAddress)
                { IsMetadataRecord = true };
        if (ReferenceEquals(created, initialSelf))
        {
            if (created.PlayerAddress is { } address && address != snapshot.PlayerAddress ||
                created.CharacterId != Guid.Empty && created.CharacterId != snapshot.CharacterId) { return false; }

            created.SetPlayerIdentity(snapshot.PlayerAddress);
            created.SetControlledCharacter(snapshot.CharacterId);
        }

        created.ConnectionEpoch = snapshot.ConnectionEpoch;
        created.ConnectionGeneration = snapshot.ConnectionGeneration;
        created.State = snapshot.State;
        created.World = World;
        _members[snapshot.ConnectionId] = created;
        _lifetimes[snapshot.ConnectionId] = (snapshot.ConnectionGeneration, snapshot.ConnectionEpoch, created, false);
        connection = created;
        return true;
    }

    public bool MatchesLifetime(byte id, Guid epoch, long generation, out WorldConnection? connection)
    {
        connection = _members.GetValueOrDefault(id);
        return connection is not null &&
               ReferenceEquals(connection.World, World) &&
               epoch != Guid.Empty &&
               generation > 0 &&
               connection.ConnectionEpoch == epoch &&
               connection.ConnectionGeneration == generation;
    }

    public bool RemoveLifetime(byte id, Guid epoch, long generation, out WorldConnection? connection)
    {
        if (!MatchesLifetime(id, epoch, generation, out connection)) { return false; }

        return Remove(connection!);
    }

    public void Register(WorldConnection connection)
    {
        Validate(connection);
        if (_members.TryGetValue(connection.ID, out var existing) && !ReferenceEquals(existing, connection))
        {
            throw new InvalidOperationException("Remove the previous connection before reusing its routing ID.");
        }

        if (World.IsAuthority) { BeginConnectionLifetime(connection); }

        _members[connection.ID] = connection;
        connection.World = World;
    }

    public void SetSelf(WorldConnection? connection)
    {
        if (connection is not null) { Validate(connection); }

        Self = connection;
        if (connection is not null) { connection.World = World; }
    }

    public void SetAuthority(WorldConnection? connection)
    {
        if (connection is not null) { Validate(connection); }

        Authority = connection;
        if (connection is not null) { connection.World = World; }
    }

    public bool Remove(WorldConnection connection)
    {
        if (!_members.TryGetValue(connection.ID, out var registered) || !ReferenceEquals(registered, connection))
        {
            return false;
        }

        _members.Remove(connection.ID);
        if (_lifetimes.TryGetValue(connection.ID, out var lifetime) && ReferenceEquals(lifetime.Record, connection))
        {
            _lifetimes[connection.ID] = (lifetime.Generation, lifetime.Epoch, lifetime.Record, true);
        }

        return true;
    }

    public bool Contains(WorldConnection connection)
    {
        return ReferenceEquals(Self, connection) ||
               ReferenceEquals(Authority, connection) ||
               _members.TryGetValue(connection.ID, out var member) && ReferenceEquals(member, connection);
    }

    public WorldConnection? FindCharacter(Guid characterId)
    {
        return characterId == Guid.Empty ? null : _members.Values.FirstOrDefault(c => c.CharacterId == characterId);
    }

    public WorldConnection? FindPlayer(PlayerAddress address)
    {
        return _members.Values.FirstOrDefault(c => c.PlayerAddress == address);
    }

    public void Clear()
    {
        foreach (var (id, lifetime) in _lifetimes.ToArray())
        {
            _lifetimes[id] = (lifetime.Generation, lifetime.Epoch, lifetime.Record, true);
        }

        _members.Clear();
        Self = null;
        Authority = null;
        // Connection identities remain owned by this world even after removal or close.
        // A replacement epoch must register a new record, even when it reuses a physical transport.
    }

    public void Dispose()
    {
        if (_disposed) { return; }

        _disposed = true;
        _closed.Dispose();
        Clear();
        _bindings.Remove(World);
    }
}
