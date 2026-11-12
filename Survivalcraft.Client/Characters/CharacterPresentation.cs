using Game.Application;
using Game.Network.Transport;

namespace Game.Characters;

/// <summary>Local view preparation and interaction for an installed authoritative character.</summary>
public sealed class CharacterPresentation : IDisposable
{
    private readonly SubsystemCharacterPresentation _owner;

    public bool IsDisposed { get; private set; }

    private SpawnDialog? _spawnDialog;

    private ComponentCharacter? _observedEntity;

    private ComponentCharacter? _readyEntity;

    private float _progress;

    private double _preparationStarted;

    private double? _deathTime;

    private double _nextRespawnRequest;

    private bool _respawnNoticeShown;

    private bool _respawning;


    public CharacterData CharacterData { get; }

    public WidgetInputDevice InputDevice { get; set; }

    public GameWidget? Widget { get; private set; }

    public GameWidget GameWidget
    {
        get
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            return Widget ?? throw new InvalidOperationException("Character view is not attached.");
        }
    }

    public CharacterViewState State { get; private set; }

    public float PreparationProgress => _progress;

    public bool IsReadyForPlaying => State is CharacterViewState.Playing or CharacterViewState.Dead;

    public CharacterPresentation(SubsystemCharacterPresentation owner, CharacterData character)
    {
        _owner = owner;
        CharacterData = character;
        character.EntityAdded += OnEntityAdded;
        character.EntityRemoved += OnEntityRemoved;
    }

    internal void Attach(GameWidget widget)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        if (widget.IsViewClosed) { throw new InvalidOperationException("Closed widgets cannot be attached again."); }

        if (!ReferenceEquals(widget.Presentation, this))
        {
            throw new InvalidOperationException("Widget belongs to another character view.");
        }

        if (!ReferenceEquals(Widget, widget)) { Widget?.CloseView(); }

        Widget = widget;
    }

    public void Update()
    {
        if (IsDisposed || Widget is null) { return; }

        if (!CharacterData.IsLocalCharacter)
        {
            State = CharacterViewState.Inactive;
            return;
        }

        var component = CharacterData.ComponentCharacter;
        if (!ReferenceEquals(component, _observedEntity))
        {
            _observedEntity = component;
            _readyEntity = null;
            _deathTime = null;
            _respawnNoticeShown = false;
            _progress = 0;
            _preparationStarted = Time.FrameStartTime;
            if (component != null)
            {
                ActivateFirstPersonCamera();
                State = CharacterViewState.PreparingView;
                if (CharacterData.IsLocalCharacter)
                {
                    _owner.Terrain.SetUpdateLocation(
                        CharacterData.CharacterIndex,
                        component.ComponentBody.Position.XZ,
                        MathUtils.Min(Configuration.ClientConfiguration.Preferences.VisibilityRange, 64f),
                        64f
                    );
                }
            }
        }

        if (component is null)
        {
            State = CharacterViewState.WaitingForEntity;
            ShowSpawnDialog(
                Localization.ClientLanguage.Catalog.Get(CharacterData.TypeName, 15),
                _respawning
                    ? Localization.ClientLanguage.Catalog.Get(CharacterData.TypeName, 3)
                    : string.Empty
            );
            return;
        }

        if (!ReferenceEquals(component, _readyEntity))
        {
            var progress = _owner.TerrainPresentation.GetUpdateProgress(
                CharacterData.CharacterIndex,
                MathUtils.Min(Configuration.ClientConfiguration.Preferences.VisibilityRange, 32f),
                0f
            );
            _progress = MathUtils.Max(_progress, progress);
            ShowSpawnDialog(
                string.Format(
                    Localization.ClientLanguage.Catalog.Get(
                        CharacterData.TypeName,
                        _respawning ? 2 : 4
                    ),
                    CharacterData.Name,
                    MathUtils.Floor(CharacterData.Level)
                ),
                string.Empty
            );
            if ((_progress < 1f || Time.FrameStartTime - _preparationStarted < 2.0) &&
                Time.FrameStartTime - _preparationStarted < 15.0) { return; }

            _readyEntity = component;
            _respawning = false;
            HideSpawnDialog();
            var runtime = WorldRuntimeContext.For(CharacterData.Project);
            NetworkSender.SendToServer(
                runtime,
                ClientPackageBuilder.State(WorldNetworkBinding.For(runtime).RequireNode().Self!) with
                {
                    State = ClientState.Playing
                }
            );
        }

        State = component.ComponentHealth.Health <= 0f ? CharacterViewState.Dead : CharacterViewState.Playing;
        if (State == CharacterViewState.Dead)
        {
            if (!_deathTime.HasValue)
            {
                _deathTime = Time.RealTime;
                ActivateDeathCamera();
                ShowDeathMessage(component);
            }

            if (Time.RealTime - _deathTime.Value > 1.5 &&
                Time.RealTime >= _nextRespawnRequest &&
                !DialogsManager.HasDialogs(GameWidget.GuiWidget) &&
                GameWidget.Input.Any)
            {
                RequestRespawn();
            }
        }
        else if (_deathTime.HasValue)
        {
            _deathTime = null;
            ActivateFirstPersonCamera();
        }
    }

    private void RequestRespawn()
    {
        var settings = CharacterData.SubsystemGameInfo.WorldSettings;
        if (settings.GameMode == GameMode.Cruel || !settings.IsAdventureRespawnAllowed)
        {
            if (_respawnNoticeShown) { return; }

            _respawnNoticeShown = true;
            DialogsManager.ShowDialog(
                GameWidget.GuiWidget,
                new MessageDialog(
                    Localization.ClientLanguage.Catalog.Warning,
                    Localization.ClientLanguage.Catalog.Get(
                        CharacterData.TypeName,
                        settings.GameMode == GameMode.Cruel ? 16 : 17
                    ),
                    Localization.ClientLanguage.Catalog.Yes,
                    Localization.ClientLanguage.Catalog.No,
                    _ => WorldNetworkBinding.For(CharacterData.Project).RequireNode().StopImmediate()
                )
            );
            return;
        }

        _nextRespawnRequest = Time.RealTime + 1;
        NetworkSender.SendToServer(
            WorldRuntimeContext.For(CharacterData.Project),
            CharacterPackageBuilder.Capture(CharacterData, ComponentCharacterPackage.CharacterAction.Restart)
        );
    }

    private void ShowDeathMessage(ComponentCharacter component)
    {
        var cause = Localization.ClientLanguage.Resolver.Resolve(component.ComponentHealth.CauseOfDeath);
        if (string.IsNullOrEmpty(cause))
        {
            cause = Localization.ClientLanguage.Catalog.Get(CharacterData.TypeName, 12);
        }

        var message = string.Format(
            Localization.ClientLanguage.Catalog.Get(CharacterData.TypeName, 13),
            cause
        );
        var settings = CharacterData.SubsystemGameInfo.WorldSettings;
        var detail = settings.GameMode == GameMode.Cruel
            ? string.Format(
                Localization.ClientLanguage.Catalog.Get(CharacterData.TypeName, 7),
                message,
                Localization.ClientLanguage.Catalog.Get("GameMode", settings.GameMode.ToString())
            )
            : string.Format(
                Localization.ClientLanguage.Catalog.Get(
                    CharacterData.TypeName,
                    settings is { GameMode: GameMode.Adventure, IsAdventureRespawnAllowed: false } ? 8 : 9
                ),
                message
            );
        component.Entity.FindComponent<ComponentGui>(true)!.DisplayLargeMessage(
            Localization.ClientLanguage.Catalog.Get(CharacterData.TypeName, 6),
            detail,
            30f,
            1.5f
        );
    }

    public void ActivateDeathCamera() => GameWidget.ActiveCamera = GameWidget.FindCamera<DeathCamera>()!;

    public void ActivateFirstPersonCamera()
    {
        GameWidget.ActiveCamera = GameWidget.FindCamera<FppCamera>()!;
        GameWidget.Target = CharacterData.ComponentCharacter;
    }

    private void ShowSpawnDialog(string large, string small)
    {
        if (ClientApplication.World is { IsEnteringWorld: true } world &&
            ReferenceEquals(world.Project, CharacterData.Project))
        {
            HideSpawnDialog();
            return;
        }

        if (_spawnDialog is null)
        {
            _spawnDialog = new SpawnDialog();
            DialogsManager.ShowDialog(GameWidget.GuiWidget, _spawnDialog);
        }

        _spawnDialog.LargeMessage = large;
        _spawnDialog.SmallMessage = small;
        _spawnDialog.Progress = _progress;
        _spawnDialog.TimeOfYear = CharacterData.SubsystemGameInfo.WorldSettings.TimeOfYear;
    }

    public void HideSpawnDialog()
    {
        if (_spawnDialog is null) { return; }

        DialogsManager.HideDialog(_spawnDialog);
        _spawnDialog = null;
    }

    private void OnEntityAdded(ComponentCharacter component)
    {
        if (IsDisposed) { return; }

        _observedEntity = null;
        _readyEntity = null;
        if (Widget is not null)
        {
            GameWidget.Target = component;
            GameWidget.ActiveCamera = GameWidget.FindCamera<FppCamera>()!;
        }
    }

    private void OnEntityRemoved()
    {
        if (IsDisposed) { return; }

        _respawning = _deathTime.HasValue;
        _observedEntity = null;
        _readyEntity = null;
        State = CharacterViewState.WaitingForEntity;
    }

    public void Dispose()
    {
        if (IsDisposed) { return; }

        IsDisposed = true;
        State = CharacterViewState.Inactive;
        Widget?.CloseView();
        CharacterData.EntityAdded -= OnEntityAdded;
        CharacterData.EntityRemoved -= OnEntityRemoved;
        HideSpawnDialog();
    }
}
