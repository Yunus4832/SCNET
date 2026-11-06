using EntitySystem.Core;

namespace Game.Commands;

public sealed class CommandPermissionDefinition(
    CommandDomain domain,
    PermissionGrantPolicy grantPolicy = PermissionGrantPolicy.Standard,
    LocalizedText? description = null,
    bool managesStandardPermissions = false,
    Func<CommandPrincipal, Project?, bool>? implicitGrant = null
)
{
    private readonly Func<CommandPrincipal, Project?, bool>? _implicitGrant =
        implicitGrant;

    public CommandDomain Domain { get; } = domain;

    public PermissionGrantPolicy GrantPolicy { get; } = grantPolicy;

    public LocalizedText Description { get; } = description ?? LocalizedText.Empty;

    public bool ManagesStandardPermissions { get; } =
        managesStandardPermissions;

    internal bool IsImplicitlyGranted(
        CommandPrincipal principal,
        Project? project
    )
    {
        return _implicitGrant?.Invoke(principal, project) == true;
    }
}

public sealed record RegisteredCommandPermission(
    ResourceId Id,
    CommandPermissionDefinition Definition
);

public static class BuiltInPermissionIds
{
    private static readonly ModId _game = new("game");

    public static ResourceId ManageStandard =>
        new(_game, "permissions.manage.standard");
}
