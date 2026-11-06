namespace Game.Commands.BuiltIn;

/// <summary>A world-local teleport preparation owned by the target player's lifetime.</summary>
internal sealed class PlayerTeleportRequest : IDisposable
{
    private readonly SubsystemCharacterTeleports _owner;

    private readonly CommandContext _context;

    private readonly WorldRuntimeContext _runtime;

    private readonly IGameCommand _command;

    private readonly CharacterData _target;

    private readonly ComponentCharacter _character;

    private readonly WorldConnection? _requesterClient;

    private readonly TerrainUpdater _updater;

    private readonly Vector3 _position;

    private readonly double _deadline;

    private readonly int _locationIndex;

    private bool _disposed;

    public PlayerTeleportRequest(
        SubsystemCharacterTeleports owner,
        CommandContext context,
        CharacterData target,
        IGameCommand command,
        Vector3 position
    )
    {
        _owner = owner;
        _context = context;
        _runtime = WorldRuntimeContext.For(context.Project!);
        _command = command;
        _target = target;
        _character = target.ComponentCharacter!;
        _requesterClient = context.Principal.Character?.ControllerConnection;
        _updater = context.Project!.FindSubsystem<SubsystemTerrain>(true)!.TerrainUpdater;
        _position = position;
        _deadline = Time.RealTime + 30;
        // Player indices are nonnegative; -1 is the updater's bootstrap location.
        _locationIndex = -2 - target.CharacterIndex;
        _updater.SetUpdateLocation(_locationIndex, position.XZ, 0f, 16f);
    }

    public void Update()
    {
        if (_disposed)
        {
            return;
        }

        if (_runtime.IsClosed)
        {
            Dispose();
            return;
        }

        try
        {
            if (!ReferenceEquals(_target.ComponentCharacter, _character) ||
                TeleportCommandHandlers.ValidateTarget(_context, _target) is { })
            {
                Cancel();
                return;
            }

            var principal = _context.Principal;
            if (principal.Character is { } requester)
            {
                if (!_target.SubsystemCharacters.Characters.Contains(requester) ||
                    (_requesterClient != null &&
                     (!ReferenceEquals(requester.ControllerConnection, _requesterClient) ||
                      !WorldConnectionQuery.IsPlayerOnline(_context.Project!, _requesterClient))))
                {
                    Cancel();
                    return;
                }

                principal = CommandPrincipal.FromPlayer(requester);
            }

            var context = new CommandContext(_context.Channel, principal, _context.Project, _context.CorrelationId)
            {
                Registry = _context.Registry
            };
            if (principal.Subject is { Ban: not null } ||
                !context.Registry.TryGetDefinition(_command.GetType(), out var registered) ||
                registered == null ||
                !registered.Definition.CanExecuteHere(_context.Project) ||
                !registered.Definition.CanInvoke(principal, context.Project) ||
                !registered.Definition.IsAuthorized(context, _command))
            {
                Finish(
                    CommandResult.LocalizedFail("command.forbidden", "CommandTypedForbidden_Message", "你没有执行该命令的权限。")
                );
                return;
            }

            if (Time.RealTime >= _deadline)
            {
                Finish(CommandResult.LocalizedFail("teleport.timeout", "TeleportTimeout_Message", "落点地形加载超时，已取消传送。"));
                return;
            }

            if (TeleportCommandHandlers.TryComplete(context, _target, _position) is { } result)
            {
                Finish(result);
            }
        }
        catch (Exception exception)
        {
            Log.Error($"Teleport preparation failed, correlation={_context.CorrelationId}: {exception}");
            Finish(CommandResult.LocalizedFail("command.failed", "CommandFailed_Message", "命令执行失败，详细信息已写入服务器日志。"));
        }
    }

    public void Cancel() =>
        Finish(CommandResult.LocalizedFail("teleport.cancelled", "TeleportCancelled_Message", "已取消等待中的传送。"));

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _updater.RemoveUpdateLocation(_locationIndex);
        _owner.Remove(_target, this);
    }

    private void Finish(CommandResult result)
    {
        if (_disposed)
        {
            return;
        }

        Dispose();
        if (_context.Registry.TryGetDefinition(_command.GetType(), out var registered))
        {
            result = result with { PanelBehavior = registered!.Definition.PanelBehavior };
        }

        Log.Information(
            $"Teleport completed: code={result.Code}, success={result.Success}, correlation={_context.CorrelationId}: {result.Message}"
        );
        if (_requesterClient != null)
        {
            if (WorldConnectionQuery.IsPlayerOnline(_context.Project!, _requesterClient))
            {
                CommandResultPublisher.PublishRemote(
                    _context.Project!,
                    result,
                    _requesterClient,
                    _context.CorrelationId
                );
            }
        }
        else
        {
            CommandResultPublisher.Publish(
                _context.Project!,
                result,
                includeServer: _context.Principal.Character is null,
                correlationId: _context.CorrelationId
            );
        }
    }
}
