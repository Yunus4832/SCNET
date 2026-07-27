using System.Globalization;

namespace Game.Commands;

public sealed class CommandDispatcher(CommandRegistry registry)
{
    private readonly CommandRegistry _registry = registry ?? throw new ArgumentNullException(nameof(registry));

    public CommandResult Execute(string input, CommandContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (string.IsNullOrWhiteSpace(input))
        {
            return CommandResult.Fail("command.empty", "请输入指令。");
        }

        var line = input.Trim();
        if (line.StartsWith('/'))
        {
            line = line[1..];
        }

        if (!CommandLineTokenizer.TryTokenize(line, out var tokens, out var tokenizeError))
        {
            return CommandResult.Fail("command.syntax", tokenizeError);
        }

        if (tokens.Count == 0)
        {
            return CommandResult.Fail("command.empty", "请输入指令。");
        }

        if (!_registry.TryFind(tokens[0], out var registered) || registered is null)
        {
            return CommandResult.Fail("command.unknown", $"未知指令：{tokens[0]}。输入 /help 查看可用指令。");
        }

        var arguments = tokens.Skip(1).ToArray();
        var denied = false;
        foreach (var route in registered.Command.Routes)
        {
            if (!TryMatchRoute(route, arguments, out var parsed))
            {
                continue;
            }

            if (!route.IsSourceAllowed(context.Source) ||
                !context.Principal.HasPermission(route.RequiredPermission))
            {
                denied = true;
                continue;
            }

            try
            {
                context.Registry = _registry;
                return route.Execute(context, new CommandArguments(parsed));
            }
            catch (Exception exception)
            {
                Log.Error(
                    $"Command {registered.Id} failed, principal={context.Principal.Name}, " +
                    $"source={context.Source}, correlation={context.CorrelationId}: {exception}");
                return CommandResult.Fail("command.failed", "指令执行失败，详细信息已写入服务器日志。");
            }
        }

        if (denied)
        {
            return CommandResult.Fail("command.forbidden", "你没有执行该指令的权限。");
        }

        var visibleRoutes = registered.Command.Routes
            .Where(route =>
                route.IsSourceAllowed(context.Source) &&
                context.Principal.HasPermission(route.RequiredPermission))
            .ToArray();
        if (visibleRoutes.Length == 0)
        {
            return CommandResult.Fail("command.forbidden", "你没有执行该指令的权限。");
        }

        var usage = string.Join(
            "；",
            visibleRoutes.Select(route => FormatUsage(registered.Command.Name, route)));
        return CommandResult.Fail("command.usage", $"指令参数不正确。用法：{usage}");
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
