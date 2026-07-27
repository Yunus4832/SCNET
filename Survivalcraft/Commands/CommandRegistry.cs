using Game.Network;

namespace Game.Commands;

public sealed class CommandRegistry
{
    private readonly Dictionary<ResourceId, Entry> _entries = [];
    private readonly Dictionary<string, Entry> _lookup = new(StringComparer.OrdinalIgnoreCase);

    public bool IsFrozen { get; private set; }

    public IReadOnlyList<RegisteredCommand> Entries => _entries
        .Select(pair => new RegisteredCommand(pair.Key, pair.Value.Command))
        .OrderBy(entry => entry.Command.Name, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    internal IDisposable Register(ModId owner, ResourceId id, GameCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (IsFrozen)
        {
            throw new InvalidOperationException("Command registry is frozen.");
        }

        if (owner != id.Namespace)
        {
            throw new InvalidOperationException($"Mod {owner} cannot register command {id} in another namespace.");
        }

        if (_entries.ContainsKey(id))
        {
            throw new InvalidOperationException($"Command resource {id} is already registered.");
        }

        var names = new[] { command.Name }.Concat(command.Aliases).ToArray();
        if (names.Any(string.IsNullOrWhiteSpace))
        {
            throw new InvalidOperationException($"Command {id} contains an empty name or alias.");
        }

        var duplicate = names
            .GroupBy(name => name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidOperationException($"Command {id} declares duplicate name \"{duplicate.Key}\".");
        }

        foreach (var name in names)
        {
            if (_lookup.TryGetValue(name, out var existing))
            {
                throw new InvalidOperationException(
                    $"Command name or alias \"{name}\" conflicts with {existing.Id}.");
            }
        }

        var entry = new Entry(owner, id, command, names);
        _entries.Add(id, entry);
        foreach (var name in names)
        {
            _lookup.Add(name, entry);
        }

        return new Registration(this, owner, id);
    }

    public bool TryFind(string name, out RegisteredCommand? registered)
    {
        if (_lookup.TryGetValue(name, out var entry))
        {
            registered = new RegisteredCommand(entry.Id, entry.Command);
            return true;
        }

        registered = null;
        return false;
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
                .Where(entry => entry.Command.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .Select(entry => new CommandSuggestion(entry.Command.Name, entry.Command.Description, false))
                .ToArray();
        }

        if (!TryFind(tokens[0], out var registered) || registered is null)
        {
            return [];
        }

        var completed = tokens.Skip(1).Take(tokens.Count - 2).ToArray();
        var prefixToken = tokens[^1];
        var suggestions = new Dictionary<string, CommandSuggestion>(StringComparer.OrdinalIgnoreCase);
        var suggestionContext = new CommandSuggestionContext(this, principal, completed);
        foreach (var route in registered.Command.Routes)
        {
            if (!IsAvailable(registered.Command) ||
                !route.IsSourceAllowed(source) ||
                !principal.HasPermission(route.RequiredPermission) ||
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
                        new CommandSuggestion(literal.Value, route.Description, false));
                    break;
                case CommandArgument argument:
                    var argumentSuggestions = GetArgumentSuggestions(argument, suggestionContext, route.Description);
                    foreach (var suggestion in argumentSuggestions.Where(item =>
                                 item.Value.StartsWith(prefixToken, StringComparison.OrdinalIgnoreCase)))
                    {
                        suggestions.TryAdd(
                            suggestion.Value,
                            new CommandSuggestion(
                                suggestion.Value,
                                string.IsNullOrWhiteSpace(suggestion.Description)
                                    ? route.Description
                                    : suggestion.Description,
                                true));
                    }

                    if (argumentSuggestions.Count > 0)
                    {
                        break;
                    }

                    var placeholder = $"<{argument.Name}>";
                    suggestions.TryAdd(
                        placeholder,
                        new CommandSuggestion(placeholder, route.Description, true));
                    break;
            }
        }

        return suggestions.Values.OrderBy(item => item.Value, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public IReadOnlyList<string> GetPermissionNodes()
    {
        return GetGrantablePermissionNodes()
            .Select(node => node.Permission)
            .OrderBy(node => node, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public bool CanGrantPermission(
        string permission,
        CommandPrincipal principal,
        CommandSource source)
    {
        ArgumentNullException.ThrowIfNull(principal);
        var normalized = CommandPermissionSet.Normalize(permission);
        var node = GetGrantablePermissionNodes().FirstOrDefault(candidate =>
            string.Equals(candidate.Permission, normalized, StringComparison.OrdinalIgnoreCase));
        if (node is null)
        {
            return false;
        }

        if (source is CommandSource.ServerConsole)
        {
            return true;
        }

        if (node.Policy is not CommandGrantPolicy.Standard)
        {
            return false;
        }

        if (string.Equals(
                normalized,
                CommandPermissionSet.ManageStandardPermission,
                StringComparison.OrdinalIgnoreCase))
        {
            return principal.CanDelegate(CommandPermissionSet.ManageStandardPermission);
        }

        return principal.CanDelegate(normalized) ||
               principal.HasPermission(CommandPermissionSet.ManageStandardPermission);
    }

    private IReadOnlyList<GrantablePermissionNode> GetGrantablePermissionNodes()
    {
        var routes = Entries
            .SelectMany(entry => entry.Command.Routes)
            .Where(route =>
                !string.IsNullOrWhiteSpace(route.RequiredPermission) &&
                !string.Equals(
                    route.RequiredPermission,
                    CommandPermissionSet.GrantPermission,
                    StringComparison.OrdinalIgnoreCase))
            .Select(route => new GrantablePermissionNode(
                CommandPermissionSet.Normalize(route.RequiredPermission),
                route.GrantPolicy))
            .ToArray();
        var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            CommandPermissionSet.ManageStandardPermission,
            "*"
        };
        foreach (var route in routes.Where(route => route.Policy is not CommandGrantPolicy.NonGrantable))
        {
            candidates.Add(route.Permission);
            if (route.Permission == "*" || route.Permission.EndsWith(".*", StringComparison.Ordinal))
            {
                continue;
            }

            var segments = route.Permission.Split('.');
            for (var count = 1; count < segments.Length; count++)
            {
                candidates.Add(string.Join(".", segments.Take(count)) + ".*");
            }
        }

        return candidates
            .Select(candidate =>
            {
                if (string.Equals(
                        candidate,
                        CommandPermissionSet.ManageStandardPermission,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return new GrantablePermissionNode(candidate, CommandGrantPolicy.Standard);
                }

                var covered = routes
                    .Where(route => CommandPermissionSet.Implies(candidate, route.Permission))
                    .ToArray();
                if (covered.Length == 0 ||
                    covered.Any(route => route.Policy is CommandGrantPolicy.NonGrantable))
                {
                    return null;
                }

                var policy = covered.Any(route => route.Policy is CommandGrantPolicy.Protected)
                    ? CommandGrantPolicy.Protected
                    : CommandGrantPolicy.Standard;
                return new GrantablePermissionNode(candidate, policy);
            })
            .Where(node => node is not null)
            .Select(node => node!)
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
            registered is null)
        {
            return false;
        }

        var arguments = tokens.Skip(1).ToArray();
        return IsAvailable(registered.Command) &&
               registered.Command.Routes.Any(route =>
                   route.IsSourceAllowed(source) &&
                   principal.HasPermission(route.RequiredPermission) &&
                   route.Segments.Count == arguments.Length &&
                   MatchesCompleted(route, arguments));
    }

    internal void Freeze()
    {
        IsFrozen = true;
    }

    internal void RemoveOwner(ModId owner)
    {
        foreach (var id in _entries
                     .Where(pair => pair.Value.Owner == owner)
                     .Select(pair => pair.Key)
                     .ToArray())
        {
            Remove(owner, id);
        }
    }

    internal static bool IsAvailable(GameCommand command)
    {
        return command.IsAvailable(RunMode.Value, CommonLib.WorkType);
    }

    private static bool IsVisible(
        GameCommand command,
        CommandPrincipal principal,
        CommandSource source)
    {
        return IsAvailable(command) &&
               command.Routes.Any(route =>
                   route.IsSourceAllowed(source) &&
                   principal.HasPermission(route.RequiredPermission));
    }

    private static bool MatchesCompleted(CommandRoute route, IReadOnlyList<string> tokens)
    {
        return !tokens.Where((t, index) => !CommandDispatcher.TryMatchSegment(route.Segments[index], t, out _)).Any();
    }

    private static IReadOnlyList<CommandArgumentSuggestion> GetArgumentSuggestions(
        CommandArgument argument,
        CommandSuggestionContext context,
        string description)
    {
        var suggestions = new Dictionary<string, CommandArgumentSuggestion>(StringComparer.OrdinalIgnoreCase);
        if (argument.Choices is { Count: > 0 })
        {
            foreach (var choice in argument.Choices)
            {
                suggestions.TryAdd(choice, new CommandArgumentSuggestion(choice, description));
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
                        suggestions.TryAdd(suggestion.Value, suggestion);
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

    private void Remove(ModId owner, ResourceId id)
    {
        if (!_entries.TryGetValue(id, out var entry) || entry.Owner != owner)
        {
            return;
        }

        _entries.Remove(id);
        foreach (var name in entry.Names)
        {
            _lookup.Remove(name);
        }
    }

    private sealed record Entry(ModId Owner, ResourceId Id, GameCommand Command, IReadOnlyList<string> Names);

    private sealed record GrantablePermissionNode(string Permission, CommandGrantPolicy Policy);

    private sealed class Registration(CommandRegistry registry, ModId owner, ResourceId id) : IDisposable
    {
        private CommandRegistry? _registry = registry;

        public void Dispose()
        {
            Interlocked.Exchange(ref _registry, null)?.Remove(owner, id);
        }
    }
}
