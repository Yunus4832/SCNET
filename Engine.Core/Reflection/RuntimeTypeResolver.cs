namespace Engine.Core.Reflection;

/// <summary>Resolves runtime type names without owning persistence aliases or retaining collectible assemblies.</summary>
public static class RuntimeTypeResolver
{
    private static readonly Dictionary<string, Type> _typesByName = new(StringComparer.Ordinal);

    public static Type? FindType(string typeName, bool throwIfNotFound = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(typeName);
        lock (_typesByName)
        {
            if (_typesByName.TryGetValue(typeName, out var cached))
            {
                return cached;
            }

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(typeName, throwOnError: false);
                if (type is null)
                {
                    continue;
                }

                if (!type.IsCollectible)
                {
                    _typesByName.Add(typeName, type);
                }

                return type;
            }
        }

        return throwIfNotFound
            ? throw new InvalidOperationException($"Type '{typeName}' not found in any loaded assembly.")
            : null;
    }
}
