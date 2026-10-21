namespace Game.Commands;

public sealed class CommandPrincipal
{
    private readonly HashSet<ResourceId> _delegablePermissions;

    private readonly HashSet<ResourceId> _permissions;

    public string Name { get; }

    public PlayerData? Player { get; }

    public CommandPrincipalKind Kind { get; }

    public IReadOnlySet<ResourceId> Permissions => _permissions;

    public IReadOnlySet<ResourceId> DelegablePermissions => _delegablePermissions;

    private CommandPrincipal(
        string name,
        CommandPrincipalKind kind,
        PlayerData? player = null,
        IEnumerable<ResourceId>? permissions = null,
        IEnumerable<ResourceId>? delegablePermissions = null
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
        Kind = kind;
        Player = player;
        _permissions = new HashSet<ResourceId>(permissions ?? []);
        _delegablePermissions = new HashSet<ResourceId>(delegablePermissions ?? []);
    }

    public bool Is(CommandPrincipalKind kind) => Kind == kind;

    public bool HasPermission(ResourceId permission)
    {
        return _permissions.Contains(permission);
    }

    public bool CanDelegate(ResourceId permission)
    {
        return _delegablePermissions.Contains(permission);
    }

    internal static CommandPrincipal CreatePlayer(
        string name,
        PlayerData? player = null,
        IEnumerable<ResourceId>? permissions = null,
        IEnumerable<ResourceId>? delegablePermissions = null
    )
    {
        var delegable = delegablePermissions?.ToArray() ?? [];
        return new CommandPrincipal(
            name,
            CommandPrincipalKind.Player,
            player,
            (permissions ?? []).Concat(delegable),
            delegable
        );
    }

    public static CommandPrincipal FromPlayer(PlayerData player)
    {
        ArgumentNullException.ThrowIfNull(player);
        return CreatePlayer(
            player.Name,
            player,
            player.CommandPermissions.Grants.Select(grant => grant.Permission),
            player.CommandPermissions.Grants
                .Where(grant => grant.CanDelegate)
                .Select(grant => grant.Permission)
        );
    }

    public static CommandPrincipal LocalOperator { get; } =
        new("Local operator", CommandPrincipalKind.LocalOperator);

    public static CommandPrincipal System { get; } =
        new("System", CommandPrincipalKind.System);
}
