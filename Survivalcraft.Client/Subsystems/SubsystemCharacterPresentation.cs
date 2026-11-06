using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Application;

namespace Game.Subsystems;

/// <summary>WorldConnection-owned character views and startup selection flow.</summary>
public sealed class SubsystemCharacterPresentation : Subsystem, IUpdateable
{
    private readonly Dictionary<CharacterData, CharacterPresentation> _presentations = new();

    private SubsystemCharacters _characters = null!;

    private SubsystemGameWidgets _views = null!;

    private bool _startupCharacterCreateRequested;

    private bool _startupCharacterCreateFailed;

    private WidgetInputDevice? _pendingInputDevice;

    private bool _disposed;

    public SubsystemTerrain Terrain { get; private set; } = null!;

    public SubsystemTerrainPresentation TerrainPresentation { get; private set; } = null!;

    public UpdateExecutionSide UpdateSide => UpdateExecutionSide.Presentation;

    public UpdateOrder UpdateOrder => UpdateOrder.SubsystemCharacters + 1;

    public CharacterPresentation Get(CharacterData character)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!ReferenceEquals(character.Project, Project))
        {
            throw new InvalidOperationException("Character belongs to another world.");
        }

        if (!_presentations.TryGetValue(character, out var presentation))
        {
            presentation = new CharacterPresentation(this, character);
            _presentations.Add(character, presentation);
        }

        return presentation;
    }

    public override void Load(ValuesDictionary valuesDictionary)
    {
        if (!WorldRuntimeContext.For(Project).HasPresentation)
        {
            throw new InvalidOperationException("Character views require presentation capability.");
        }

        _characters = Project.FindSubsystem<SubsystemCharacters>(true)!;
        _views = Project.FindSubsystem<SubsystemGameWidgets>(true)!;
        Terrain = Project.FindSubsystem<SubsystemTerrain>(true)!;
        TerrainPresentation = Project.FindSubsystem<SubsystemTerrainPresentation>(true)!;
        _characters.CharacterAdded += OnCharacterAdded;
        _characters.CharacterRemoved += OnCharacterRemoved;
        _characters.CharacterCreationRejected += OnCreationRejected;
        _characters.OwnCharacterCreationRequested += OnCreationRequested;
        _characters.Closing += Dispose;
        foreach (var character in _characters.Characters) { OnCharacterAdded(character); }
    }

    private void OnCharacterAdded(CharacterData character)
    {
        var presentation = Get(character);
        if (presentation.Widget is null) { _views.AddGameWidget(presentation); }
    }

    private void OnCharacterRemoved(CharacterData character)
    {
        if (!_presentations.Remove(character, out var presentation)) { return; }

        presentation.Dispose();
        if (presentation.Widget is { } widget) { _views.RemoveGameWidget(widget); }
    }

    private void OnCreationRequested(CharacterData profile) => _pendingInputDevice = Get(profile).InputDevice;

    private void OnCreationRejected(CharacterCreationError error)
    {
        _startupCharacterCreateFailed = true;
        _startupCharacterCreateRequested = false;
        _pendingInputDevice = null;
    }

    public void Update(float dt)
    {
        if (_disposed) { return; }

        var runtime = WorldRuntimeContext.For(Project);
        if (WorldConnections.For(runtime).Authority?.ConnectionPhase !=
            ConnectionPhase.Live)
        {
            return;
        }

        if (WorldNetworkBinding.For(runtime).RequireNode().Self?.PlayerAddress is not null &&
            _characters.Characters.All(character => !character.IsLocalCharacter))
        {
            if (!TryPrepareStartupCharacter())
            {
                ScreensManager.SwitchScreen("Character", CharacterScreen.Mode.Initial, Project);
            }
        }
        else if (_startupCharacterCreateRequested &&
                 _characters.Characters.Any(character => character.IsLocalCharacter))
        {
            _startupCharacterCreateRequested = false;
            ScreensManager.SwitchScreen("Game");
        }

        foreach (var character in _characters.Characters)
        {
            var presentation = Get(character);
            if (character.IsLocalCharacter && _pendingInputDevice.HasValue)
            {
                presentation.InputDevice = _pendingInputDevice.Value;
                _pendingInputDevice = null;
            }

            presentation.Update();
        }
    }

    private bool TryPrepareStartupCharacter()
    {
        var requestedName = ClientApplication.World?.RequestedCharacterName;
        if (_startupCharacterCreateFailed || string.IsNullOrWhiteSpace(requestedName)) { return false; }

        if (_startupCharacterCreateRequested) { return true; }

        var profile = new CharacterData(Project) { Name = CharacterData.SanitizeName(requestedName.Trim()) };
        if (!_characters.IsCharacterNameAvailable(profile.Name))
        {
            Log.Warning($"Ignoring --player because '{requestedName}' is not a valid player name.");
            return false;
        }

        _startupCharacterCreateRequested = true;
        _characters.RequestOwnCharacterCreation(profile);
        return true;
    }

    public override void Dispose()
    {
        if (_disposed) { return; }

        _disposed = true;
        if (_characters is not null)
        {
            _characters.CharacterAdded -= OnCharacterAdded;
            _characters.CharacterRemoved -= OnCharacterRemoved;
            _characters.CharacterCreationRejected -= OnCreationRejected;
            _characters.OwnCharacterCreationRequested -= OnCreationRequested;
            _characters.Closing -= Dispose;
        }

        foreach (var presentation in _presentations.Values)
        {
            presentation.Dispose();
            if (presentation.Widget is { } widget) { _views.RemoveGameWidget(widget); }
        }

        _presentations.Clear();
    }
}
