using EntitySystem.TemplatesDatabase;

namespace Game.Commands;

public sealed record CommandPermissionGrant(string Permission, bool CanDelegate);

public sealed class CommandPermissionSet
{
    public const string GrantPermission = "permissions.grant";

    public const string ManageStandardPermission = "permissions.manage.standard";

    private readonly List<CommandPermissionGrant> _grants = [];

    public IReadOnlyList<CommandPermissionGrant> Grants => _grants;

    public bool HasPermission(string permission)
    {
        var normalized = Normalize(permission);
        return _grants.Any(grant => Implies(grant.Permission, normalized));
    }

    public bool CanDelegate(string permission)
    {
        var normalized = Normalize(permission);
        return _grants.Any(grant =>
            grant.CanDelegate &&
            Implies(grant.Permission, normalized));
    }

    public bool Grant(string permission, bool canDelegate)
    {
        var normalized = Normalize(permission);
        var index = _grants.FindIndex(grant =>
            string.Equals(grant.Permission, normalized, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
        {
            _grants.Add(new CommandPermissionGrant(normalized, canDelegate));
            Sort();
            return true;
        }

        if (!canDelegate || _grants[index].CanDelegate)
        {
            return false;
        }

        _grants[index] = new CommandPermissionGrant(normalized, true);
        return true;
    }

    public bool Revoke(string permission)
    {
        var normalized = Normalize(permission);
        return _grants.RemoveAll(grant =>
            string.Equals(grant.Permission, normalized, StringComparison.OrdinalIgnoreCase)) > 0;
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
                { "Permission", grant.Permission },
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
            var permission = item.GetValue("Permission", string.Empty);
            if (string.IsNullOrWhiteSpace(permission))
            {
                continue;
            }

            Grant(permission, item.GetValue("CanDelegate", false));
        }
    }

    public static bool Implies(string grantedPermission, string requestedPermission)
    {
        var granted = Normalize(grantedPermission);
        var requested = Normalize(requestedPermission);
        if (granted == "*" ||
            string.Equals(granted, requested, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return granted.EndsWith(".*", StringComparison.Ordinal) &&
               requested.StartsWith(granted[..^1], StringComparison.OrdinalIgnoreCase);
    }

    public static string Normalize(string permission)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permission);
        var normalized = permission.Trim();
        var wildcardIndex = normalized.IndexOf('*');
        var validWildcard = wildcardIndex < 0 ||
                            normalized == "*" ||
                            wildcardIndex == normalized.Length - 1 &&
                            normalized.LastIndexOf('*') == wildcardIndex &&
                            normalized.EndsWith(".*", StringComparison.Ordinal);
        if (normalized.Any(char.IsWhiteSpace) ||
            normalized.StartsWith('.') ||
            normalized.EndsWith('.') ||
            normalized.Contains("..", StringComparison.Ordinal) ||
            !validWildcard)
        {
            throw new ArgumentException($"Invalid command permission \"{permission}\".", nameof(permission));
        }

        return normalized;
    }

    private void Sort()
    {
        _grants.Sort((left, right) =>
            StringComparer.OrdinalIgnoreCase.Compare(left.Permission, right.Permission));
    }
}
