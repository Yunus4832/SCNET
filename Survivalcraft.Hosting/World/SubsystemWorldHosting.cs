using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Hosting;

public sealed class SubsystemWorldHosting : Subsystem
{
    private SubsystemGameInfo _gameInfo = null!;

    private bool? _persistedRunServer;

    private GameMode? _persistedGameMode;

    private bool _subscribed;

    public WorldHostingSettings Settings { get; } = new();

    public override void Load(ValuesDictionary valuesDictionary)
    {
        if (_subscribed) { throw new InvalidOperationException("World hosting is already loaded."); }

        if (!WorldRuntimeContext.For(Project).IsAuthority)
        {
            throw new InvalidOperationException("World hosting requires authority.");
        }

        _gameInfo = Project.FindSubsystem<SubsystemGameInfo>(true)!;
        Settings.Load(_gameInfo.ValuesDictionary);
        Project.AfterSubsystemsSaved += OnAfterSubsystemsSaved;
        _subscribed = true;
    }

    public void ApplyRunServerOverride(bool runServer)
    {
        _persistedRunServer ??= Settings.RunServer;
        Settings.RunServer = runServer;
    }

    public void ApplyGameModeOverride(GameMode gameMode)
    {
        _persistedGameMode ??= _gameInfo.WorldSettings.GameMode;
        _gameInfo.WorldSettings.GameMode = gameMode;
    }

    private void OnAfterSubsystemsSaved(ProjectData snapshot)
    {
        if (Project.SendToClientMode) { return; }

        var gameInfo =
            snapshot.ValuesDictionary.GetValue<ValuesDictionary>(_gameInfo.ValuesDictionary.DatabaseObject.Name);
        Settings.Save(gameInfo, _gameInfo.WorldSettings.Name);
        if (_persistedRunServer is { } runServer) { gameInfo.SetValue("RunServer", runServer); }

        if (_persistedGameMode is { } gameMode) { gameInfo.SetValue("GameMode", gameMode); }
    }

    public override void Dispose()
    {
        if (!_subscribed) { return; }

        _subscribed = false;
        Project.AfterSubsystemsSaved -= OnAfterSubsystemsSaved;
    }
}
