using System.Globalization;

using Game.Localization;

namespace Game.Commands;

public sealed class TextCommandAdapter(CommandRegistry registry)
    : ICommandFrontendAdapter<string>
{
    private readonly CommandRegistry _registry = registry ?? throw new ArgumentNullException(nameof(registry));

    public IReadOnlyList<RegisteredTextCommand> Entries => _registry.Adapters
        .Get<TextCommand>()
        .Select(entry => new RegisteredTextCommand(entry.Id, entry.Binding))
        .OrderBy(entry => entry.Command.Name, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    public bool TryFind(
        string name,
        out RegisteredTextCommand? registered)
    {
        if (_registry.Adapters.TryFindText(name, out var entry) &&
            entry is not null)
        {
            registered = new RegisteredTextCommand(entry.Id, entry.Binding);
            return true;
        }

        registered = null;
        return false;
    }

    public bool SupportsSource(string input, CommandSource source)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var line = input.TrimStart();
        if (line.StartsWith('/'))
        {
            line = line[1..];
        }

        var commandName = CommandLineTokenizer.TokenizePartial(line).FirstOrDefault();
        return !string.IsNullOrWhiteSpace(commandName) &&
               TryFind(commandName, out var registered) &&
               registered?.Command.SupportsSource(source) == true;
    }

    public IReadOnlyList<CommandSuggestion> Suggest(
        string input,
        CommandPrincipal principal,
        CommandSource source = CommandSource.Player)
    {
        ArgumentNullException.ThrowIfNull(principal);
        var line = input.TrimStart();
        if (line.StartsWith('/'))
        {
            line = line[1..];
        }

        var tokens = CommandLineTokenizer.TokenizePartial(line);
        if (tokens.Count <= 1)
        {
            var prefix = tokens.Count == 0 ? string.Empty : tokens[0];
            return Entries
                .Where(entry => IsVisible(entry.Command, principal, source))
                .Where(entry => entry.Command.Name.StartsWith(
                    prefix,
                    StringComparison.OrdinalIgnoreCase))
                .Select(entry => new CommandSuggestion(
                    entry.Command.Name,
                    entry.Command.Description.Resolve(),
                    false))
                .ToArray();
        }

        if (!TryFind(tokens[0], out var registered) ||
            registered is null ||
            !registered.Command.SupportsSource(source))
        {
            return [];
        }

        var completed = tokens.Skip(1).Take(tokens.Count - 2).ToArray();
        var prefixToken = tokens[^1];
        var suggestions = new Dictionary<string, CommandSuggestion>(
            StringComparer.OrdinalIgnoreCase);
        var suggestionContext = new CommandSuggestionContext(
            _registry,
            principal,
            source,
            completed);
        foreach (var route in registered.Command.Routes)
        {
            if (!_registry.CanExecute(route.CommandType, principal, source) ||
                completed.Length >= route.Segments.Count ||
                !MatchesCompleted(route, completed))
            {
                continue;
            }

            var segment = route.Segments[completed.Length];
            switch (segment)
            {
                case CommandLiteral literal when
                    literal.Value.StartsWith(prefixToken, StringComparison.OrdinalIgnoreCase):
                    suggestions.TryAdd(
                        literal.Value,
                        new CommandSuggestion(
                            literal.Value,
                            route.Description.Resolve(),
                            false));
                    break;
                case CommandArgument argument:
                    var argumentSuggestions = GetArgumentSuggestions(
                        argument,
                        suggestionContext,
                        route.Description);
                    foreach (var suggestion in argumentSuggestions.Where(item =>
                                 item.Value.StartsWith(
                                     prefixToken,
                                     StringComparison.OrdinalIgnoreCase)))
                    {
                        suggestions.TryAdd(
                            suggestion.Value,
                            new CommandSuggestion(
                                suggestion.Value,
                                (suggestion.Description ??
                                 route.Description).Resolve(),
                                true));
                    }

                    if (argumentSuggestions.Count > 0)
                    {
                        break;
                    }

                    var placeholder = $"<{argument.Name}>";
                    suggestions.TryAdd(
                        placeholder,
                        new CommandSuggestion(
                            placeholder,
                            route.Description.Resolve(),
                            true));
                    break;
            }
        }

        return suggestions.Values
            .OrderBy(item => item.Value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public bool CanExecute(
        string input,
        CommandPrincipal principal,
        CommandSource source = CommandSource.Player)
    {
        ArgumentNullException.ThrowIfNull(principal);
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var line = input.Trim();
        if (line.StartsWith('/'))
        {
            line = line[1..];
        }

        if (!CommandLineTokenizer.TryTokenize(line, out var tokens, out _) ||
            tokens.Count == 0 ||
            !TryFind(tokens[0], out var registered) ||
            registered is null ||
            !registered.Command.SupportsSource(source))
        {
            return false;
        }

        var arguments = tokens.Skip(1).ToArray();
        return registered.Command.Routes.Any(route =>
            _registry.CanExecute(route.CommandType, principal, source) &&
            route.Segments.Count == arguments.Length &&
            MatchesCompleted(route, arguments));
    }

    public CommandResult Execute(string input, CommandContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (string.IsNullOrWhiteSpace(input))
        {
            return CommandResult.LocalizedFail(
                "command.empty",
                "CommandEmpty_Message",
                "请输入指令。");
        }

        var line = input.Trim();
        if (line.StartsWith('/'))
        {
            line = line[1..];
        }

        if (!CommandLineTokenizer.TryTokenize(line, out var tokens, out var tokenizeError))
        {
            return CommandResult.LocalizedFail(
                "command.syntax",
                "CommandQuoteUnclosed_Message",
                string.IsNullOrWhiteSpace(tokenizeError)
                    ? "指令中存在未闭合的引号。"
                    : tokenizeError);
        }

        if (tokens.Count == 0)
        {
            return CommandResult.LocalizedFail(
                "command.empty",
                "CommandEmpty_Message",
                "请输入指令。");
        }

        if (!TryFind(tokens[0], out var registered) || registered is null)
        {
            return CommandResult.LocalizedFail(
                "command.unknown",
                "CommandUnknown_Message",
                "未知指令：{0}。输入 /help 查看可用指令。",
                tokens[0]);
        }

        if (!registered.Command.SupportsSource(context.Source))
        {
            return CommandResult.LocalizedFail(
                "command.frontend_unavailable",
                "CommandFrontendUnavailable_Message",
                "该指令未向当前文本入口开放。");
        }

        var arguments = tokens.Skip(1).ToArray();
        var denied = false;
        foreach (var route in registered.Command.Routes)
        {
            if (!TryMatchRoute(route, arguments, out var parsed))
            {
                continue;
            }

            if (!_registry.CanExecute(route.CommandType, context.Principal, context.Source))
            {
                denied = true;
                continue;
            }

            try
            {
                context.Registry = _registry;
                var command = route.CreateCommand(new CommandArguments(parsed));
                return new CommandDispatcher(_registry).Execute(command, context);
            }
            catch (Exception exception)
            {
                Log.Error(
                    $"Command {registered.Id} failed, principal={context.Principal.Name}, " +
                    $"source={context.Source}, correlation={context.CorrelationId}: {exception}");
                return CommandResult.LocalizedFail(
                    "command.failed",
                    "CommandFailed_Message",
                    "指令执行失败，详细信息已写入服务器日志。");
            }
        }

        if (denied)
        {
            return CommandResult.LocalizedFail(
                "command.forbidden",
                "CommandForbidden_Message",
                "你没有执行该指令的权限。");
        }

        var visibleRoutes = registered.Command.Routes
            .Where(route =>
                _registry.CanExecute(route.CommandType, context.Principal, context.Source))
            .ToArray();
        if (visibleRoutes.Length == 0)
        {
            return CommandResult.LocalizedFail(
                "command.forbidden",
                "CommandForbidden_Message",
                "你没有执行该指令的权限。");
        }

        var usage = string.Join(
            "\n",
            visibleRoutes.Select(route =>
                $"- {FormatUsage(registered.Command.Name, route)}"));
        return CommandResult.LocalizedFail(
            "command.usage",
            "CommandUsage_Message",
            "指令参数不正确。\n用法：\n{0}",
            usage);
    }

    internal static bool TryMatchSegment(CommandSegment segment, string token, out object? value)
    {
        switch (segment)
        {
            case CommandLiteral literal:
                value = literal.Value;
                return string.Equals(literal.Value, token, StringComparison.OrdinalIgnoreCase);
            case CommandArgument { Choices: { Count: > 0 } choices } argument:
                var choice = choices.FirstOrDefault(
                    item => string.Equals(item, token, StringComparison.OrdinalIgnoreCase));
                if (choice is not null)
                {
                    return TryConvert(argument.Kind, choice, out value);
                }

                value = null;
                return false;

            case CommandArgument argument:
                return TryConvert(argument.Kind, token, out value);
            default:
                value = null;
                return false;
        }
    }

    private bool IsVisible(
        TextCommand command,
        CommandPrincipal principal,
        CommandSource source)
    {
        return command.SupportsSource(source) &&
               command.Routes.Any(route =>
                   _registry.CanExecute(route.CommandType, principal, source));
    }

    private static bool MatchesCompleted(
        CommandRoute route,
        IReadOnlyList<string> tokens)
    {
        return !tokens.Where((token, index) =>
            !TryMatchSegment(route.Segments[index], token, out _)).Any();
    }

    private static IReadOnlyList<CommandArgumentSuggestion> GetArgumentSuggestions(
        CommandArgument argument,
        CommandSuggestionContext context,
        LocalizedText description)
    {
        var suggestions = new Dictionary<string, CommandArgumentSuggestion>(
            StringComparer.OrdinalIgnoreCase);
        if (argument.Choices is { Count: > 0 })
        {
            foreach (var choice in argument.Choices)
            {
                suggestions.TryAdd(
                    choice,
                    new CommandArgumentSuggestion(choice, description));
            }
        }

        if (argument.SuggestionProvider is not null)
        {
            try
            {
                foreach (var suggestion in argument.SuggestionProvider(context))
                {
                    if (!string.IsNullOrWhiteSpace(suggestion.Value))
                    {
                        suggestions[suggestion.Value] = suggestion;
                    }
                }
            }
            catch (Exception exception)
            {
                Log.Error($"Command suggestion provider failed: {exception}");
            }
        }

        return suggestions.Values.ToArray();
    }

    private static bool TryMatchRoute(
        CommandRoute route,
        IReadOnlyList<string> tokens,
        out Dictionary<string, object> values)
    {
        values = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        if (route.Segments.Count != tokens.Count)
        {
            return false;
        }

        for (var index = 0; index < route.Segments.Count; index++)
        {
            var segment = route.Segments[index];
            if (!TryMatchSegment(segment, tokens[index], out var value))
            {
                return false;
            }

            if (segment is CommandArgument argument)
            {
                values.Add(argument.Name, value!);
            }
        }

        return true;
    }

    private static bool TryConvert(CommandArgumentKind kind, string token, out object? value)
    {
        switch (kind)
        {
            case CommandArgumentKind.String:
                value = token;
                return true;
            case CommandArgumentKind.Integer when
                int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer):
                value = integer;
                return true;
            case CommandArgumentKind.Number when
                double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var number):
                value = number;
                return true;
            case CommandArgumentKind.Boolean when bool.TryParse(token, out var boolean):
                value = boolean;
                return true;
            case CommandArgumentKind.Guid when Guid.TryParse(token, out var guid):
                value = guid;
                return true;
            default:
                value = null;
                return false;
        }
    }

    private static string FormatUsage(string name, CommandRoute route)
    {
        var suffix = string.Join(
            " ",
            route.Segments.Select(segment => segment switch
            {
                CommandLiteral literal => literal.Value,
                CommandArgument argument => $"<{argument.Name}>",
                _ => string.Empty
            }));
        return string.IsNullOrEmpty(suffix) ? $"/{name}" : $"/{name} {suffix}";
    }
}
