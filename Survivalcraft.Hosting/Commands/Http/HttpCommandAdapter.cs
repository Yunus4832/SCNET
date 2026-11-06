using EntitySystem.Core;

using Game.Localization;

namespace Game.Commands.Http;

public sealed class HttpCommandAdapter(CommandRegistry registry)
    : ICommandFrontendAdapter<HttpCommandRequest>
{
    private readonly CommandRegistry _registry =
        registry ?? throw new ArgumentNullException(nameof(registry));

    public IReadOnlyList<HttpDiscoveredCommand> Discover(
        CommandPrincipal principal,
        Project? applicationProject,
        Project? authorityProject,
        LocalizedTextResolver resolver
    )
    {
        return _registry.Adapters.Get<HttpCommandBinding>()
            .Where(entry =>
                {
                    if (!_registry.TryGetDefinition(entry.Id, out var command) || command is null) { return false; }

                    var project = command.Definition.Domain == CommandDomain.Application
                        ? applicationProject
                        : authorityProject;
                    return command.Definition.CanInvoke(principal, project) &&
                           command.Definition.CanExecuteHere(project) &&
                           command.Definition.IsPotentiallyAuthorized(_registry.Permissions, principal, project);
                }
            )
            .Select(entry =>
                {
                    _registry.TryGetDefinition(entry.Id, out var command);
                    return new HttpDiscoveredCommand(
                        entry.Id.ToString(),
                        resolver.Resolve(command!.Definition.Description),
                        entry.Binding.Arguments
                    );
                }
            ).ToArray();
    }

    public CommandResult Execute(HttpCommandRequest request, CommandContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return TryCreate(request, out var command, out var error)
            ? new CommandDispatcher(_registry).Execute(command!, context)
            : error!;
    }

    public bool TryCreate(
        HttpCommandRequest request,
        out IGameCommand? command,
        out CommandResult? error
    )
    {
        ArgumentNullException.ThrowIfNull(request);
        command = null;
        error = null;
        if (!_registry.Adapters.TryGet<HttpCommandBinding>(
                request.Identity,
                out var binding
            ) ||
            binding is null)
        {
            error = CommandResult.LocalizedFail(
                "command.http_not_exposed",
                "HttpNotExposed_Message",
                "命令 {0} 未向 HTTP 前端开放。",
                request.Identity.ToString()
            );
            return false;
        }

        try
        {
            command = binding.CreateCommand(new HttpCommandArguments(request.Arguments));
            return true;
        }
        catch (Exception exception)
        {
            Log.Error(
                $"HTTP command {request.Identity} binding failed, " +
                $"{exception}"
            );
            error = CommandResult.LocalizedFail(
                "command.http_invalid_arguments",
                "HttpInvalidArguments_Message",
                "HTTP 命令参数无效。"
            );
            return false;
        }
    }
}
