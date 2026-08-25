using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Localization;

namespace Game.Commands;

public sealed record CommandPermissionGrant(
    ResourceId Permission,
    bool CanDelegate);

public sealed class CommandPermissionSet
{
    private readonly List<CommandPermissionGrant> _grants = [];

    public IReadOnlyList<CommandPermissionGrant> Grants => _grants;

    public bool HasPermission(ResourceId permission)
    {
        return _grants.Any(grant => grant.Permission == permission);
    }

    public bool CanDelegate(ResourceId permission)
    {
        return _grants.Any(grant =>
            grant.Permission == permission &&
            grant.CanDelegate);
    }

    public bool Grant(ResourceId permission, bool canDelegate)
    {
        var index = _grants.FindIndex(grant => grant.Permission == permission);
        if (index < 0)
        {
            _grants.Add(new CommandPermissionGrant(permission, canDelegate));
            Sort();
            return true;
        }

        if (!canDelegate || _grants[index].CanDelegate)
        {
            return false;
        }

        _grants[index] = new CommandPermissionGrant(permission, true);
        return true;
    }

    public bool Revoke(ResourceId permission)
    {
        return _grants.RemoveAll(grant => grant.Permission == permission) > 0;
    }

    public void Replace(IEnumerable<CommandPermissionGrant> grants)
    {
        ArgumentNullException.ThrowIfNull(grants);
        _grants.Clear();
        foreach (var grant in grants)
        {
            Grant(grant.Permission, grant.CanDelegate);
        }
    }

    public ValuesDictionary Save()
    {
        var values = new ValuesDictionary();
        for (var index = 0; index < _grants.Count; index++)
        {
            var grant = _grants[index];
            values.SetValue(index.ToString(), new ValuesDictionary
            {
                { "Namespace", grant.Permission.Namespace.ToString() },
                { "Path", grant.Permission.Path },
                { "CanDelegate", grant.CanDelegate }
            });
        }

        return values;
    }

    public void Load(ValuesDictionary values)
    {
        ArgumentNullException.ThrowIfNull(values);
        _grants.Clear();
        foreach (var item in values.Values.OfType<ValuesDictionary>())
        {
            var permissionNamespace = item.GetValue("Namespace", string.Empty);
            var path = item.GetValue("Path", string.Empty);
            if (string.IsNullOrWhiteSpace(permissionNamespace) ||
                string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            Grant(
                new ResourceId(new ModId(permissionNamespace), path),
                item.GetValue("CanDelegate", false));
        }
    }

    private void Sort()
    {
        _grants.Sort((left, right) =>
            StringComparer.Ordinal.Compare(
                left.Permission.ToString(),
                right.Permission.ToString()));
    }
}

public sealed class CommandPermissionDefinition(
    CommandDomain domain,
    PermissionGrantPolicy grantPolicy = PermissionGrantPolicy.Standard,
    LocalizedText? description = null,
    bool managesStandardPermissions = false,
    Func<CommandPrincipal, Project?, bool>? implicitGrant = null)
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
        Project? project)
    {
        return _implicitGrant?.Invoke(principal, project) == true;
    }
}

public sealed record RegisteredCommandPermission(
    ResourceId Id,
    CommandPermissionDefinition Definition);

public static class BuiltInPermissionIds
{
    private static readonly ModId _game = new("game");

    public static ResourceId ManageStandard =>
        new(_game, "permissions.manage.standard");
}
