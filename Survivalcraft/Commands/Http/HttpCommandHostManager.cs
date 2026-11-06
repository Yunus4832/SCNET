using Game.Application;

namespace Game.Commands.Http;

public static class HttpCommandHostManager
{
    private static HttpCommandHost? _host;

    private static readonly HttpCommandExecutionQueue _queue = new(
        () => CurrentModRuntime.Value is { } runtime
            ? new CommandExecutionScope(
                runtime.Commands,
                GameManager.ClientProject,
                GameManager.Runtime?.Authority?.Project
            )
            : null,
        () => Localization.ClientLanguage.Resolver
    );

    private static readonly HttpPlayerCommandAdapter _player = new(
        () => GameManager.LocalCharacter?.CharacterData,
        () => CurrentModRuntime.Value?.Commands,
        () => Localization.ClientLanguage.Resolver,
        CommandGateway.ExecuteOrSend
    );


    public static void Update()
    {
        _player.Update();
        _queue.Update();
    }


    public static void Start(SessionInfo session, CommandPrincipal principal)
    {
        ClientApplication.PlayerCommands = _player;
        if (_host is not null)
        {
            return;
        }

        if (!(session.HttpCommandEnabled ?? Configuration.HostingConfiguration.Preferences.HttpCommandEnabled))
        {
            return;
        }

        var port = session.HttpCommandPort ?? Configuration.HostingConfiguration.Preferences.HttpCommandPort;
        var accessToken = string.IsNullOrWhiteSpace(session.HttpCommandAccessToken)
            ? Configuration.HostingConfiguration.Preferences.HttpCommandAccessToken
            : session.HttpCommandAccessToken;
        if (port is <= 0 or > 65535)
        {
            Log.Error(
                $"HTTP command host is disabled because configured port {port} is invalid. " +
                "Set HttpCommandPort in Settings.xml or provide --http-command-port with a value from 1 to 65535."
            );
            return;
        }

        try
        {
            _host = HttpCommandHost.Start(
                port,
                accessToken,
                principal,
                _queue,
                _player,
                () => Localization.ClientLanguage.Resolver
            );
        }
        catch (Exception exception)
        {
            Log.Error(
                $"HTTP command host could not listen on 127.0.0.1:{port}; " +
                $"the game will continue without HTTP command access. {exception.Message}"
            );
        }
    }

    public static void Stop()
    {
        Interlocked.Exchange(ref _host, null)?.Dispose();
        _queue.FailPending();
        _player.FailPending();
    }
}
