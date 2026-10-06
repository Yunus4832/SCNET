using System.Diagnostics;

using Engine.Core;

using EntitySystem.TemplatesDatabase;

using Game;
using Game.Managers;
using Game.Modding;
using Game.Network;
using Game.Network.Enums;
using Game.Network.Packages;
using Game.Network.Serialization;
using Game.Terrains;
using Game.Terrains.Distribution;

using LiteNetLib;

namespace ServerLoadTool;

public sealed class LoadClient : IDisposable
{
    private readonly LoadOptions _options;
    private readonly NetManager _network;
    private readonly Guid _identity = Guid.NewGuid();
    private readonly Guid _token = Guid.NewGuid();
    private readonly Dictionary<Point2, Chunk> _chunks = [];
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private NetPeer? _peer;
    private byte? _id;
    private Guid _epoch;
    private bool _hasEntity;
    private bool _snapshot;
    private double _lastMotion;
    private double _lastLocation;
    private Vector2? _locationCenter;
    private ulong _generation;
    private Vector3 _origin;
    private int _entityId;
    private bool _mounted;
    private double _lastDismountRequest;
    private double _dismountReadyAt;
    private double? _divergedAt;
    private double _movingSeconds;
    private bool _stopping;

    public int Index { get; }
    public string Name { get; }
    public string State { get; private set; } = "connecting";
    public string? Error { get; private set; }
    public Vector3 Position { get; private set; }
    public Vector3? AuthoritativePosition { get; private set; }
    public bool SpawnedMounted { get; private set; }
    public LoadWorldInfo? World { get; private set; }
    public double? PlayingSeconds { get; private set; }
    public long ReceivedBytes { get; private set; }
    public long SentBytes { get; private set; }
    public long ReceivedPackages { get; private set; }
    public long CompletedChunks { get; private set; }
    public long RetriedChunks { get; private set; }
    public List<double> ChunkMilliseconds { get; } = [];
    public int PendingChunks => _chunks.Values.Count(chunk => !chunk.Completed);
    public int Ping => _peer?.Ping ?? 0;

    public LoadClient(LoadOptions options, int index, string runName)
    {
        _options = options;
        Index = index;
        Name = $"Load{runName}{index:D3}";
        var listener = new EventBasedNetListener();
        _network = new NetManager(listener)
        {
            ChannelsCount = 7,
            UseSafeMtu = true,
            DisconnectTimeout = 15000,
            AutoRecycle = true,
            EnableStatistics = true
        };
        listener.NetworkReceiveEvent += (_, reader, _) =>
        {
            try
            {
                ReceivedBytes += reader.AvailableBytes;
                foreach (var package in LoadWire.Decode(reader))
                {
                    ReceivedPackages++;
                    Receive(package);
                }
            }
            catch (Exception exception)
            {
                Fail($"protocol: {exception.Message}");
            }
        };
        listener.PeerDisconnectedEvent += (_, info) =>
        {
            if (!_stopping && Error == null)
            {
                var reason = info.Reason.ToString();
                if (info.Reason == DisconnectReason.ConnectionRejected && info.AdditionalData?.AvailableBytes > 0)
                {
                    try
                    {
                        reason = LoadWire.Decode(info.AdditionalData).OfType<ConnectionRejectPackage>().FirstOrDefault()?.Reason ?? reason;
                    }
                    catch (Exception)
                    {
                        // Transport rejection without a game payload still remains a failed run.
                    }
                }

                Fail($"disconnected: {reason}");
            }
        };
        listener.NetworkErrorEvent += (_, error) => Fail($"socket: {error}");
        if (!_network.StartInManualMode(0))
        {
            throw new IOException("Cannot bind load-client UDP socket.");
        }

        var request = new ConnectionRequestPackage(_token, VersionsManager.ProtocolVersion, _identity, ModProfileManager.EmptyDataHash);
        try
        {
            _peer = _network.Connect(options.Host, options.Port, LoadWire.Encode(request));
        }
        catch
        {
            _network.Stop();
            throw;
        }
    }

    private void Fail(string error)
    {
        Error ??= error;
        State = "failed";
    }

    private void Send(IPackage package)
    {
        if (_peer == null || Error != null)
        {
            return;
        }

        var data = LoadWire.Encode(package, _id);
        var transport = PackageTransportPolicy.Get(package);
        _peer.Send(data, transport.ChannelNumber, transport.DeliveryMethod);
        SentBytes += data.Length;
    }

    private void Receive(IPackage package)
    {
        switch (package)
        {
            case ConnectionRejectPackage reject:
                Fail($"rejected: {reject.Reason}");
                break;
            case BootstrapPackage bootstrap:
                World = LoadWorldInfo.Read(bootstrap.ProjectData);
                _epoch = bootstrap.Epoch;
                _id = bootstrap.ClientList.List.Single(client => client.TokenId == _token).ID;
                State = "bootstrap";
                Send(new ConnectionPhaseAckPackage(_epoch, ConnectionPhase.BootstrapApplied));
                break;
            case InitialWorldSnapshotPackage snapshot:
                if (snapshot.Epoch != _epoch)
                {
                    throw new InvalidDataException("World snapshot epoch mismatch.");
                }

                ReadEntities(snapshot.EntityData);
                _snapshot = true;
                Send(new ConnectionPhaseAckPackage(_epoch, ConnectionPhase.WorldSnapshotApplied));
                Send(new PlayerDataPackage
                {
                    Type = PlayerDataPackage.DataType.Create,
                    PlayerName = Name,
                    SkinName = "$Male1",
                    PlayerClass = PlayerClass.Male
                });
                State = "joining";
                break;
            case EntityPackage { Type: EntityPackage.EventType.LoadOne or EntityPackage.EventType.LoadList } entities:
                ReadEntities(entities.EntityData);
                break;
            case ComponentPlayerPackage { Type: ComponentPlayerPackage.PlayerAction.PositionSet } position when position.FromPlayerId == _id:
                Position = position.Position;
                _origin = Position;
                _movingSeconds = 0;
                break;
            case ComponentMountPackage mount when mount.FromId == _entityId:
                if (mount.Type == ComponentMountPackage.EventType.Dismount)
                {
                    _mounted = false;
                    _dismountReadyAt = _clock.Elapsed.TotalSeconds + 1;
                }
                else if (mount.Type == ComponentMountPackage.EventType.Mount)
                {
                    _mounted = true;
                }

                break;
            case SubsystemTerrainPackage { Type: SubsystemTerrainPackage.DataType.SyncTerrainChunkFragment } terrain:
                if (_chunks.TryGetValue(terrain.ChunkFragment.Allocation.Coords, out var chunk) &&
                    !chunk.Completed && chunk.Allocation == terrain.ChunkFragment.Allocation && chunk.Fragments.Add(terrain.ChunkFragment, out _))
                {
                    chunk.Completed = true;
                    CompletedChunks++;
                    ChunkMilliseconds.Add((_clock.Elapsed.TotalSeconds - chunk.Started) * 1000);
                }

                break;
            case OnlinePlayerStatePackage online:
                var ownState = online.Players.FirstOrDefault(player => player.PlayerGuid == _identity);
                if (ownState.PlayerGuid == _identity)
                {
                    AuthoritativePosition = ownState.Position;
                }

                if (online.Players.Any(player => player.PlayerGuid == _identity && player.Health <= 0))
                {
                    Fail("player died; use an isolated Creative world for this synthetic workload");
                }

                break;
        }
    }

    private void ReadEntities(byte[] data)
    {
        var entities = new ValuesDictionary();
        entities.ApplyOverridesUseMessagePack(data);
        foreach (var value in entities.Values.OfType<ValuesDictionary>())
        {
            var components = value.GetValue<ValuesDictionary>("Overrides");
            if (components.GetValue<ValuesDictionary?>("Player", null)?.GetValue("PlayerGuid", Guid.Empty) != _identity)
            {
                continue;
            }

            if (!_hasEntity)
            {
                Position = components.GetValue<ValuesDictionary>("Body").GetValue<Vector3>("Position");
                _entityId = value.GetValue<int>("Id");
                _mounted = components.GetValue<ValuesDictionary>("Body").GetValue("ParentBody", 0) != 0;
                SpawnedMounted = _mounted;
                _origin = Position;
                _hasEntity = true;
            }
        }
    }

    public void Update(int elapsedMilliseconds, bool move)
    {
        _network.ManualReceive();
        _network.ManualUpdate(elapsedMilliseconds);
        if (Error != null)
        {
            return;
        }

        var now = _clock.Elapsed.TotalSeconds;
        if (State != "playing" && now > _options.JoinTimeoutSeconds)
        {
            Fail("join timeout");
            return;
        }

        if (!_hasEntity || !_snapshot)
        {
            return;
        }

        if (now - _lastLocation >= 1 && LoadInterest.ShouldMove(_locationCenter, Position.XZ))
        {
            _lastLocation = now;
            _locationCenter = Position.XZ;
            Send(new PlayerDataPackage(new TerrainUpdater.UpdateLocation
            {
                Center = Position.XZ,
                ContentDistance = _options.Visibility,
                VisibilityDistance = _options.Visibility,
                LastChunksUpdateCenter = _locationCenter
            }));
            UpdateChunks(now);
        }

        RetryChunks(now);
        if (_mounted && now - _lastDismountRequest >= 1)
        {
            _lastDismountRequest = now;
            Send(new ComponentMountPackage { Type = ComponentMountPackage.EventType.DismountRequest, FromId = _entityId });
        }

        if (State != "playing" && !_mounted && now >= _dismountReadyAt && _chunks.Count > 0 && _chunks.Values.All(chunk => chunk.Completed))
        {
            State = "playing";
            PlayingSeconds = now;
            Send(new ClientPackage(_id!.Value, ClientState.Playing));
            Send(new ComponentPlayerPackage { Type = ComponentPlayerPackage.PlayerAction.IntoPlaying });
            Send(new ComponentPlayerPackage { Type = ComponentPlayerPackage.PlayerAction.CreativeFlyChange, IsCreativeFly = true });
        }

        if (State == "playing" && AuthoritativePosition is { } authoritative)
        {
            if (Vector3.DistanceSquared(Position, authoritative) > 16 * 16)
            {
                _divergedAt ??= now;
                if (now - _divergedAt.Value > 5)
                {
                    Fail("authoritative movement diverged; the server is not accepting this workload");
                }
            }
            else
            {
                _divergedAt = null;
            }
        }

        if (State == "playing" && now - _lastMotion >= 0.1)
        {
            var dt = now - _lastMotion;
            _lastMotion = now;
            var previousPosition = Position;
            if (move && _options.Workload != "idle")
            {
                _movingSeconds += Math.Min(dt, 0.1);
                Position = LoadMotion.Position(_origin, _movingSeconds, Index, _options);
            }

            Send(new ComponentPlayerPackage
            {
                Type = ComponentPlayerPackage.PlayerAction.BodyUpdate,
                PackageChangeFlag = ComponentPlayerPackage.ChangFlag.PositionChange | ComponentPlayerPackage.ChangFlag.VelocityChange |
                    ComponentPlayerPackage.ChangFlag.RotationChange | ComponentPlayerPackage.ChangFlag.LookAnglesChange | ComponentPlayerPackage.ChangFlag.SneakChange,
                Position = Position,
                Velocity = (Position - previousPosition) / (float)dt,
                Rotation = Quaternion.Identity
            });
        }
    }

    private void UpdateChunks(double now)
    {
        var desired = LoadInterest.Chunks(_locationCenter!.Value, _options.Visibility);

        foreach (var coords in _chunks.Keys.Except(desired).ToArray())
        {
            _chunks.Remove(coords);
        }

        var requests = new List<ChunkContentRequest>();
        foreach (var coords in desired)
        {
            if (!_chunks.ContainsKey(coords))
            {
                var chunk = new Chunk(new ChunkAllocationId(coords, ++_generation), now);
                _chunks.Add(coords, chunk);
                requests.Add(new ChunkContentRequest(chunk.Allocation));
            }
        }

        if (requests.Count > 0)
        {
            Send(new SubsystemTerrainPackage(requests));
        }
    }

    private void RetryChunks(double now)
    {
        foreach (var chunk in _chunks.Values.Where(chunk => !chunk.Completed && now - chunk.LastRequest >= 3))
        {
            chunk.LastRequest = now;
            RetriedChunks++;
            if (chunk.Fragments.TryCreateMissingFragmentRequest(chunk.Allocation, out var missing))
            {
                Send(SubsystemTerrainPackage.CreateFragmentRequest([missing]));
            }
            else
            {
                Send(new SubsystemTerrainPackage([new ChunkContentRequest(chunk.Allocation)]));
            }
        }
    }

    public void Dispose()
    {
        _stopping = true;
        _peer?.Disconnect();
        _network.ManualUpdate(1);
        _network.Stop();
    }

    private sealed class Chunk(ChunkAllocationId allocation, double now)
    {
        public ChunkAllocationId Allocation { get; } = allocation;
        public double Started { get; } = now;
        public double LastRequest { get; set; } = now;
        public bool Completed { get; set; }
        public TerrainChunkFragmentReassembler Fragments { get; } = new();
    }
}
