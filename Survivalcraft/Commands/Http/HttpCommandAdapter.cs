using System.Text.Json;
using System.Text.Json.Nodes;

namespace Game.Commands;

/// <summary>
/// Protocol contract reserved for a future HTTP frontend. Every command uses
/// one endpoint and is selected by its stable command identity.
/// </summary>
public static class HttpCommandProtocol
{
    public const int DefaultPort = 28889;

    public const string Endpoint = "/commands";

    public const string IdentityProperty = "identity";

    public const string ArgumentsProperty = "arguments";

    public static JsonObject CreateEnvelope(
        ResourceId identity,
        JsonObject? arguments = null)
    {
        return new JsonObject
        {
            [IdentityProperty] = identity.ToString(),
            [ArgumentsProperty] = arguments?.DeepClone() ?? new JsonObject()
        };
    }

    public static bool TryParseEnvelope(
        JsonObject envelope,
        out HttpCommandRequest? request,
        out string error)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        try
        {
            var identityValue = envelope[IdentityProperty]?.GetValue<string>();
            var separator = identityValue?.IndexOf(':') ?? -1;
            if (separator <= 0 ||
                separator == identityValue!.Length - 1 ||
                envelope[ArgumentsProperty] is not JsonObject arguments)
            {
                request = null;
                error =
                    "HTTP command envelope requires identity \"namespace:path\" and an arguments object.";
                return false;
            }

            var identity = new ResourceId(
                new ModId(identityValue[..separator]),
                identityValue[(separator + 1)..]);
            request = new HttpCommandRequest(identity, arguments);
            error = string.Empty;
            return true;
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or
            ArgumentException or
            FormatException)
        {
            request = null;
            error = "HTTP command envelope is invalid.";
            return false;
        }
    }
}

public sealed record HttpCommandRequest(
    ResourceId Identity,
    JsonObject Arguments);

/// <summary>One declared argument in the HTTP command envelope.</summary>
public sealed record HttpCommandArgumentDefinition(
    string Name,
    string ValueType,
    bool Required = true);

/// <summary>One HTTP command available to the current authenticated principal.</summary>
public sealed record HttpDiscoveredCommand(
    string Identity,
    string Description,
    IReadOnlyList<HttpCommandArgumentDefinition> Arguments);

public sealed class HttpCommandArguments(JsonObject values)
{
    private static readonly JsonSerializerOptions _jsonOptions =
        new(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        };

    public JsonObject Values { get; } =
        values ?? throw new ArgumentNullException(nameof(values));

    public T Get<T>(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (!Values.TryGetPropertyValue(name, out var node) || node is null)
        {
            throw new InvalidDataException(
                $"Required HTTP command argument \"{name}\" is missing.");
        }

        return node.Deserialize<T>(_jsonOptions)
               ?? throw new InvalidDataException(
                   $"HTTP command argument \"{name}\" cannot be converted to {typeof(T).Name}.");
    }
}

public sealed class HttpCommandBinding : ICommandAdapterBinding
{
    private readonly Func<HttpCommandArguments, IGameCommand> _createCommand;

    public Type CommandType { get; }

    public IReadOnlyList<HttpCommandArgumentDefinition> Arguments { get; }

    private HttpCommandBinding(
        Type commandType,
        Func<HttpCommandArguments, IGameCommand> createCommand,
        IReadOnlyList<HttpCommandArgumentDefinition> arguments)
    {
        CommandType = commandType;
        _createCommand = createCommand;
        Arguments = arguments;
    }

    public static HttpCommandBinding Create<TCommand>(
        Func<HttpCommandArguments, TCommand> createCommand,
        params HttpCommandArgumentDefinition[] arguments)
        where TCommand : IGameCommand
    {
        ArgumentNullException.ThrowIfNull(createCommand);
        return new HttpCommandBinding(
            typeof(TCommand),
            arguments => createCommand(arguments),
            arguments ?? []);
    }

    internal IGameCommand CreateCommand(HttpCommandArguments arguments)
    {
        return _createCommand(arguments);
    }
}

public sealed class HttpCommandAdapter(CommandRegistry registry)
    : ICommandFrontendAdapter<HttpCommandRequest>
{
    private readonly CommandRegistry _registry =
        registry ?? throw new ArgumentNullException(nameof(registry));

    public CommandResult Execute(
        HttpCommandRequest request,
        CommandContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);
        if (!_registry.Adapters.TryGet<HttpCommandBinding>(
                request.Identity,
                out var binding) ||
            binding is null)
        {
            return CommandResult.LocalizedFail(
                "command.http_not_exposed",
                "HttpNotExposed_Message",
                "命令 {0} 未向 HTTP 前端开放。",
                request.Identity.ToString());
        }

        try
        {
            var command = binding.CreateCommand(
                new HttpCommandArguments(request.Arguments));
            context.Registry = _registry;
            return new CommandDispatcher(_registry).Execute(command, context);
        }
        catch (Exception exception)
        {
            Log.Error(
                $"HTTP command {request.Identity} binding failed, " +
                $"principal={context.Principal.Name}, correlation={context.CorrelationId}: {exception}");
            return CommandResult.LocalizedFail(
                "command.http_invalid_arguments",
                "HttpInvalidArguments_Message",
                "HTTP 命令参数无效。");
        }
    }
}
