using System.Diagnostics;

using Engine.Core.Mathematics;
using Engine.Network;

namespace ServerLoadTool;

public sealed class LoadClient : IDisposable
{
    // The load client creates the default male character (wire value 0).
    private const byte _defaultCharacterSex = 0;

    private readonly LoadOptions _options;

    private readonly NetworkHost _network;

    private readonly CancellationTokenSource _stop = new();

    private readonly Task<NetworkConnection> _connecting;

    private Guid _characterId;

    private readonly Guid _token = Guid.NewGuid();

    private readonly Dictionary<Point2, Chunk> _chunks = [];

    private readonly Stopwatch _clock = Stopwatch.StartNew();

    private NetworkConnection? _peer;

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

    public int Ping => _peer?.RoundTripMilliseconds ?? 0;

    public LoadClient(LoadOptions options, int index, string runName)
    {
        _options = options;
        Index = index;
        Name = $"Load{runName}{index:D3}";
        _network = new NetworkHost(options: new NetworkHostOptions { DisconnectTimeout = TimeSpan.FromSeconds(15) });
        _network.Received += (peer, message) =>
        {
            try
            {
                _peer ??= peer;
                ReceivedBytes += message.Payload.Length;
                foreach (var package in LoadWire.Decode(message.Payload))
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
        _network.Disconnected += (_, information) =>
        {
            if (!_stopping && Error == null)
            {
                Fail($"disconnected: {Describe(information)}");
            }
        };
        _connecting = ConnectAsync();
    }

    private async Task<NetworkConnection> ConnectAsync()
    {
        await using var preparation = await _network.PrepareAddressAsync(_options.Host, _options.Port, _stop.Token);
        var request = new ConnectionRequestPackage(
            _token,
            GameProtocol.CurrentVersion,
            GameProtocol.EmptyModDataHash
        );
        return await preparation.ConnectAsync(LoadWire.Encode(request), _stop.Token);
    }

    private static string Describe(NetworkDisconnectInfo information)
    {
        if (information.Reason == NetworkDisconnectReason.Rejected && !information.Data.IsEmpty)
        {
            try
            {
                return LoadWire.Decode(information.Data).OfType<ConnectionRejectPackage>().FirstOrDefault()?.Reason ??
                       information.Reason.ToString();
            }
            catch (Exception)
            {
                // A transport rejection without a game payload still represents a failed run.
            }
        }

        return information.Reason.ToString();
    }

    private void Fail(string error)
    {
        Error ??= error;
        State = "failed";
    }

    private void Send(IProtocolMessage package)
    {
        if (_peer == null || Error != null)
        {
            return;
        }

        var data = LoadWire.Encode(package, _id);
        var channel = package is CharacterStatePackage ? NetworkChannel.Snapshot :
            package is ComponentCharacterPackage ? NetworkChannel.ReliableEvent : NetworkChannel.Control;
        var delivery = package is CharacterStatePackage ? DeliveryMode.Sequenced : DeliveryMode.ReliableOrdered;
        var result = _peer.TrySend(data, (byte)channel, delivery);
        if (result != NetworkSendResult.Accepted)
        {
            Fail($"send: {result}");
            return;
        }

        SentBytes += data.Length;
    }

    private void Receive(IProtocolMessage package)
    {
        switch (package)
        {
            case ConnectionRejectPackage reject:
                Fail($"rejected: {reject.Reason}");
                break;
            case AdmissionChallengePackage challenge:
                if (challenge.Mode != ServerAdmissionMode.Open)
                {
                    Fail("This load client requires an open-admission server.");
                    break;
                }

                State = "admission";
                Send(new AdmissionProofPackage(challenge.Nonce));
                break;
            case AdmissionResultPackage admission:
                if (admission.Code != AdmissionResultCode.Accepted)
                {
                    Fail($"admission: {admission.Code}");
                }

                break;
            case BootstrapPackage bootstrap:
                World = LoadWorldInfo.Read(bootstrap.World);
                _epoch = bootstrap.Epoch;
                var ownClient = bootstrap.ClientList.List.Single(client => client.TokenId == _token);
                _id = ownClient.ID;
                _characterId = ownClient.CharacterId;
                State = "bootstrap";
                Send(new ConnectionPhaseAckPackage(_epoch, ConnectionPhase.BootstrapApplied));
                break;
            case InitialWorldSnapshotPackage snapshot:
                if (snapshot.Epoch != _epoch)
                {
                    throw new InvalidDataException("World snapshot epoch mismatch.");
                }

                ReadEntities(snapshot.CharacterEntities);
                _snapshot = true;
                Send(new ConnectionPhaseAckPackage(_epoch, ConnectionPhase.WorldSnapshotApplied));
                if (_characterId == Guid.Empty)
                {
                    Send(
                        new CharacterCreateRequestPackage
                        {
                            CreationRequestId = Guid.NewGuid(), CharacterName = Name, SkinName = "$Male1",
                            CharacterSex = _defaultCharacterSex
                        }
                    );
                }

                State = "joining";
                break;
            case CharacterCreateRejectedPackage rejected:
                Fail($"Character creation rejected: {rejected.CreationError}");
                break;
            case SingleEntitySnapshotPackage entity:
                ReadCharacterSnapshots(entity.Characters, entity.CharacterEntities);
                break;
            case EntitySnapshotPackage entities:
                ReadCharacterSnapshots(entities.Characters, entities.CharacterEntities);
                break;
            case CharacterPositionPackage position when position.ControllerSessionId == _id:
                Position = position.Position;
                _origin = Position;
                _movingSeconds = 0;
                break;
            case ComponentMountPackage mount when mount.FromId == _entityId:
                if (mount is DismountedPackage message2)
                {
                    _mounted = false;
                    _dismountReadyAt = _clock.Elapsed.TotalSeconds + 1;
                }
                else if (mount is MountedPackage message1)
                {
                    _mounted = true;
                }

                break;
            case TerrainFragmentPackage terrain:
                if (_chunks.TryGetValue(terrain.ChunkFragment.Allocation.Coords, out var chunk) &&
                    !chunk.Completed &&
                    chunk.Allocation == terrain.ChunkFragment.Allocation &&
                    chunk.Fragments.Add(terrain.ChunkFragment, out _))
                {
                    chunk.Completed = true;
                    CompletedChunks++;
                    ChunkMilliseconds.Add((_clock.Elapsed.TotalSeconds - chunk.Started) * 1000);
                }

                break;
            case OnlineCharacterStatePackage online:
                var ownState = online.Characters.FirstOrDefault(player => player.CharacterId == _characterId);
                if (ownState.CharacterId == _characterId)
                {
                    AuthoritativePosition = ownState.Position;
                }

                if (online.Characters.Any(player => player.CharacterId == _characterId && player.Health <= 0))
                {
                    Fail("player died; use an isolated Creative world for this synthetic workload");
                }

                break;
        }
    }

    private void ReadCharacterSnapshots(
        IReadOnlyList<CharacterSnapshot> characters,
        IReadOnlyList<CharacterEntitySnapshot> summaries
    )
    {
        foreach (var character in characters)
        {
            if (character.Client is { } session && session.ID == _id) { _characterId = session.CharacterId; }
        }

        ReadEntities(summaries);
    }

    private void ReadEntities(IReadOnlyList<CharacterEntitySnapshot> entities)
    {
        foreach (var entity in entities)
        {
            if (entity.CharacterId != _characterId || _hasEntity) { continue; }

            Position = entity.Position;
            _entityId = entity.EntityId;
            _mounted = entity.ParentBodyId != 0;
            SpawnedMounted = _mounted;
            _origin = Position;
            _hasEntity = true;
        }
    }

    public void Update(bool move)
    {
        if (_peer is null && _connecting.IsCompleted && Error is null)
        {
            try
            {
                _peer = _connecting.GetAwaiter().GetResult();
            }
            catch (NetworkConnectionException exception)
            {
                Fail($"connect: {Describe(exception.Information)}");
            }
            catch (Exception exception)
            {
                Fail($"connect: {exception.Message}");
            }
        }

        if (_network.Failure is { } failure)
        {
            Fail($"transport: {failure.Message}");
        }

        _network.DispatchEvents();
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
            Send(
                new CharacterUpdateLocationPackage
                {
                    UpdateLocation = new TerrainUpdateLocation
                    {
                        Center = Position.XZ,
                        ContentDistance = _options.Visibility,
                        VisibilityDistance = _options.Visibility,
                        LastChunksUpdateCenter = _locationCenter
                    }
                }
            );
            UpdateChunks(now);
        }

        RetryChunks(now);
        if (_mounted && now - _lastDismountRequest >= 1)
        {
            _lastDismountRequest = now;
            Send(new DismountRequestPackage { FromId = _entityId });
        }

        if (State != "playing" &&
            !_mounted &&
            now >= _dismountReadyAt &&
            _chunks.Count > 0 &&
            _chunks.Values.All(chunk => chunk.Completed))
        {
            State = "playing";
            PlayingSeconds = now;
            Send(new ClientStateChangedPackage { ClientId = _id!.Value, State = ClientState.Playing });
            Send(new CharacterReadyPackage());
            Send(new CharacterFlightPackage { IsCreativeFly = true });
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

            Send(
                new CharacterStatePackage
                {
                    PackageChangeFlag = ComponentCharacterPackage.ChangFlag.PositionChange |
                                        ComponentCharacterPackage.ChangFlag.VelocityChange |
                                        ComponentCharacterPackage.ChangFlag.RotationChange |
                                        ComponentCharacterPackage.ChangFlag.LookAnglesChange |
                                        ComponentCharacterPackage.ChangFlag.SneakChange,
                    Position = Position,
                    Velocity = (Position - previousPosition) / (float)dt,
                    Rotation = Quaternion.Identity
                }
            );
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
            Send(new TerrainChunksRequestPackage { ChunkRequests = requests.ToArray() });
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
                Send(
                    new TerrainChunksRequestPackage
                        { ChunkRequests = [new ChunkContentRequest(chunk.Allocation)] }
                );
            }
        }
    }

    public void Dispose()
    {
        _stopping = true;
        _peer?.Disconnect();
        _stop.Cancel();
        try
        {
            _network.DisposeAsync().AsTask().GetAwaiter().GetResult();
            try
            {
                _connecting.GetAwaiter().GetResult();
            }
            catch (Exception) when (_connecting.IsCanceled || _connecting.IsFaulted)
            {
                // Observe the canceled/failed attempt while disposing this load client.
            }
        }
        finally
        {
            _stop.Dispose();
        }
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
