using Game.Network;
using Game.Network.Enums;

namespace Game.Commands;

/// <summary>A world-local teleport preparation owned by the target player's lifetime.</summary>
internal sealed class PlayerTeleportRequest : IDisposable
{
    private readonly CommandContext _context;
    private readonly IGameCommand _command;
    private readonly PlayerData _target;
    private readonly ComponentPlayer _character;
    private readonly Client? _requesterClient;
    private readonly TerrainUpdater _updater;
    private readonly Vector3 _position;
    private readonly double _deadline;
    private readonly int _locationIndex;
    private bool _disposed;

    public PlayerTeleportRequest(CommandContext context, PlayerData target, IGameCommand command, Vector3 position)
    {
        _context = context;
        _command = command;
        _target = target;
        _character = target.ComponentPlayer!;
        _requesterClient = context.Principal.Player is { IsMainPlayer: false } requester ? requester.Client : null;
        _updater = context.Project!.FindSubsystem<SubsystemTerrain>(true)!.TerrainUpdater;
        _position = position;
        _deadline = Time.RealTime + 30;
        // Player indices are nonnegative; -1 is the updater's bootstrap location.
        _locationIndex = -2 - target.PlayerIndex;
        _updater.SetUpdateLocation(_locationIndex, position.XZ, 0f, command is TeleportSpawnCommand { World: true } ? 32f : 16f);
    }

    public void Update()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            if (!ReferenceEquals(_target.ComponentPlayer, _character) ||
                TeleportCommandHandlers.ValidateTarget(_context, _target) is { })
            {
                Cancel();
                return;
            }

            var principal = _context.Principal;
            if (principal.Player is { } requester)
            {
                if (!_target.SubsystemPlayers.PlayersData.Contains(requester) ||
                    (_requesterClient != null &&
                     (!ReferenceEquals(requester.Client, _requesterClient) || !_requesterClient.IsConnected)))
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
            if (!context.Registry.TryGetDefinition(_command.GetType(), out var registered) ||
                registered == null ||
                !registered.Definition.CanExecuteHere(RunMode.Value, CommonLib.WorkType) ||
                !registered.Definition.CanInvoke(principal, context.Project) ||
                !registered.Definition.IsAuthorized(context, _command))
            {
                Finish(CommandResult.LocalizedFail("command.forbidden", "CommandTypedForbidden_Message", "你没有执行该命令的权限。"));
                return;
            }

            if (Time.RealTime >= _deadline)
            {
                Finish(CommandResult.LocalizedFail("teleport.timeout", "TeleportTimeout_Message", "落点地形加载超时，已取消传送。"));
                return;
            }

            if (TeleportCommandHandlers.TryComplete(context, _target, _position, _command is TeleportSpawnCommand { World: true }) is { } result)
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
        if (ReferenceEquals(_target.PendingTeleport, this))
        {
            _target.PendingTeleport = null;
        }
    }

    private void Finish(CommandResult result)
    {
        if (_disposed)
        {
            return;
        }

        Dispose();
        Log.Information($"Teleport completed: code={result.Code}, success={result.Success}, correlation={_context.CorrelationId}: {result.Message}");
        if (CommonLib.WorkType == WorkType.Server && _requesterClient != null)
        {
            if (_requesterClient.IsConnected)
            {
                CommandResultPublisher.PublishRemote(_context.Project!, result, _requesterClient, _context.CorrelationId);
            }
        }
        else
        {
            CommandResultPublisher.Publish(_context.Project!, result,
                includeServer: _context.Principal.Player is null or { IsMainPlayer: true });
        }
    }
}
