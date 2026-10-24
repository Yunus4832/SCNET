using Game.Network;
using Game.Players;

namespace Game.Commands;

public sealed class CommandPrincipal
{
    private readonly HashSet<ResourceId> _delegablePermissions;

    private readonly HashSet<ResourceId> _permissions;

    public string Name { get; }

    public CharacterData? Character { get; }

    public WorldPlayer? Subject { get; }

    public CommandPrincipalKind Kind { get; }

    public IReadOnlySet<ResourceId> Permissions => _permissions;

    public IReadOnlySet<ResourceId> DelegablePermissions => _delegablePermissions;

    private CommandPrincipal(
        string name,
        CommandPrincipalKind kind,
        CharacterData? player = null,
        IEnumerable<ResourceId>? permissions = null,
        IEnumerable<ResourceId>? delegablePermissions = null,
        WorldPlayer? subject = null
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
        Kind = kind;
        Character = player;
        Subject = subject;
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
        CharacterData? player = null,
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

    /// <summary>Client input uses public character metadata without server-owned permissions or subject records.</summary>
    internal static CommandPrincipal ForInput(CharacterData player)
    {
        ArgumentNullException.ThrowIfNull(player);
        return CommonLib.WorkType == WorkType.Client ? CreatePlayer(player.Name, player) : FromPlayer(player);
    }

    public static CommandPrincipal FromSession(Client session)
    {
        ArgumentNullException.ThrowIfNull(session);
        var subject = session.Subject ??
                      throw new InvalidOperationException("The session has no admitted world subject.");
        return FromSubject(subject, session.CharacterData);
    }

    internal static CommandPrincipal FromSubject(WorldPlayer subject, CharacterData? player = null)
    {
        ArgumentNullException.ThrowIfNull(subject);
        var name = subject.DisplayName.Length > 0 ? subject.DisplayName : player?.Name ?? subject.Address.Value;
        return new CommandPrincipal(
            name,
            CommandPrincipalKind.Player,
            player,
            subject.Permissions.Grants.Select(grant => grant.Permission),
            subject.Permissions.Grants.Where(grant => grant.CanDelegate).Select(grant => grant.Permission),
            subject
        );
    }

    public static CommandPrincipal FromPlayer(CharacterData player)
    {
        ArgumentNullException.ThrowIfNull(player);
        return FromSession(
            player.Client ?? throw new InvalidOperationException("The character has no controlling session.")
        );
    }

    public static CommandPrincipal LocalOperator { get; } =
        new("Local operator", CommandPrincipalKind.LocalOperator);

    public static CommandPrincipal System { get; } =
        new("System", CommandPrincipalKind.System);
}
