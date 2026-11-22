namespace Engine.Core.Reflection;

/// <summary>解析运行时类型名，不管理持久化别名，也不持有可卸载程序集的引用。</summary>
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
