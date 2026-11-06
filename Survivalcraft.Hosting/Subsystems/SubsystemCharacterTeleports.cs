using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Commands.BuiltIn;

namespace Game.Subsystems;

/// <summary>Host-owned command operations advanced at the world roster's completed-character phase.</summary>
public sealed class SubsystemCharacterTeleports : Subsystem
{
    private readonly Dictionary<CharacterData, PlayerTeleportRequest> _requests = new(
        ReferenceEqualityComparer.Instance
    );

    private SubsystemCharacters _characters = null!;

    private WorldRuntimeContext _runtime = null!;

    private CancellationTokenRegistration _lifetimeRegistration;

    private bool _stopped;

    private bool _disposed;

    public override void Load(ValuesDictionary valuesDictionary)
    {
        _runtime = WorldRuntimeContext.For(Project);
        if (!_runtime.IsAuthority)
        {
            throw new InvalidOperationException("Teleport operations require an authority world.");
        }

        _characters = Project.FindSubsystem<SubsystemCharacters>(true)!;
        _characters.CharacterUpdated += OnCharacterUpdated;
        _characters.CharacterRemoving += Cancel;
        _characters.Closing += Stop;
        _lifetimeRegistration = _runtime.Lifetime.Register(Stop);
        _runtime.OwnWorldResource(this);
    }

    public bool HasPending(CharacterData character) => _requests.ContainsKey(character);

    internal void Start(CommandContext context, CharacterData character, IGameCommand command, Vector3 position)
    {
        ObjectDisposedException.ThrowIf(_stopped || _disposed || _runtime.IsClosed, this);
        if (!ReferenceEquals(context.Project, Project) || !ReferenceEquals(character.Project, Project))
        {
            throw new InvalidOperationException("Teleport requests must belong to their owning world.");
        }

        Cancel(character);
        var request = new PlayerTeleportRequest(this, context, character, command, position);
        _requests.Add(character, request);
        character.Disposed += OnCharacterDisposed;
    }

    public void Cancel(CharacterData character)
    {
        if (_requests.TryGetValue(character, out var request)) { request.Cancel(); }
    }

    internal void Remove(CharacterData character, PlayerTeleportRequest expected)
    {
        if (_requests.TryGetValue(character, out var current) && ReferenceEquals(current, expected))
        {
            _requests.Remove(character);
            character.Disposed -= OnCharacterDisposed;
        }
    }

    private void OnCharacterUpdated(CharacterData character)
    {
        if (!_stopped && _requests.TryGetValue(character, out var request)) { request.Update(); }
    }

    private void OnCharacterDisposed(CharacterData character)
    {
        if (_requests.TryGetValue(character, out var request)) { request.Dispose(); }
    }

    private void Stop()
    {
        if (_stopped) { return; }

        _stopped = true;
        foreach (var request in _requests.Values.ToArray()) { request.Dispose(); }
    }

    public override void Dispose()
    {
        if (_disposed) { return; }

        _disposed = true;
        Stop();
        if (_characters is not null)
        {
            _characters.CharacterUpdated -= OnCharacterUpdated;
            _characters.CharacterRemoving -= Cancel;
            _characters.Closing -= Stop;
        }

        _lifetimeRegistration.Dispose();
    }
}
