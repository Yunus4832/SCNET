using System.Runtime.CompilerServices;

namespace Engine.Core.Text.Conversion;

public static class HumanReadableConverter
{
    private sealed record ConverterCatalog(IReadOnlyDictionary<Type, IHumanReadableConverter> Converters);

    private sealed record ConverterEntry(IHumanReadableConverter Converter, int AssemblyVersion);

    private static readonly Lock _gate = new();

    private static readonly ConditionalWeakTable<Assembly, ConverterCatalog> _catalogs = new();

    private static readonly ConditionalWeakTable<Type, ConverterEntry> _convertersByType = new();

    private static int _assemblyVersion;

    static HumanReadableConverter()
    {
        AppDomain.CurrentDomain.AssemblyLoad += (_, _) => Interlocked.Increment(ref _assemblyVersion);
    }

    public static string ConvertToString(object value)
    {
        var type = value.GetType();
        try
        {
            return GetConverter(type, true)!.ConvertToString(value);
        }
        catch (Exception innerException)
        {
            throw new InvalidOperationException(
                $"Cannot convert value of type \"{type.FullName}\" to string.",
                innerException
            );
        }
    }

    public static bool TryConvertFromString(Type type, string data, out object? result)
    {
        try
        {
            result = ConvertFromString(type, data);
            return true;
        }
        catch (Exception)
        {
            result = null;
            return false;
        }
    }

    public static bool TryConvertFromString<T>(string data, out T? result)
    {
        if (TryConvertFromString(typeof(T), data, out var result2))
        {
            result = (T?)result2;
            return true;
        }

        result = default;
        return false;
    }

    public static object ConvertFromString(Type type, string data)
    {
        try
        {
            var value = GetConverter(type, true)!.ConvertFromString(type, data);
            return type.IsInstanceOfType(value)
                ? value
                : throw new InvalidCastException(
                    $"Text converter returned a value incompatible with '{type.FullName}'."
                );
        }
        catch (Exception innerException)
        {
            throw new InvalidOperationException(
                $"Cannot convert string to value of type '{type.FullName}'.",
                innerException
            );
        }
    }

    public static T ConvertFromString<T>(string data)
    {
        return (T)ConvertFromString(typeof(T), data);
    }

    public static bool IsTypeSupported(Type type)
    {
        return GetConverter(type, false) != null;
    }

    public static string ValuesListToString<T>(char separator, params T[] values)
    {
        var array = new string[values.Length];
        for (var i = 0; i < values.Length; i++)
        {
            if (values[i] is null)
            {
                array[i] = string.Empty;
                continue;
            }

            array[i] = ConvertToString(values[i]!);
        }

        return string.Join(separator.ToString(), array);
    }

    public static T[] ValuesListFromString<T>(char separator, string data)
    {
        if (string.IsNullOrEmpty(data))
        {
            return [];
        }

        var array = data.Split(separator);
        var array2 = new T[array.Length];
        for (var i = 0; i < array.Length; i++)
        {
            array2[i] = ConvertFromString<T>(array[i]);
        }

        return array2;
    }

    private static IHumanReadableConverter? GetConverter(Type type, bool throwIfNotFound)
    {
        lock (_gate)
        {
            var version = Volatile.Read(ref _assemblyVersion);
            if (_convertersByType.TryGetValue(type, out var cached) && cached.AssemblyVersion == version)
            {
                return cached.Converter;
            }

            IHumanReadableConverter? converter = null;
            Type? fallbackType = null;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var catalog = assembly.IsDynamic
                    ? CreateCatalog(assembly)
                    : _catalogs.GetValue(assembly, CreateCatalog);
                if (catalog.Converters.TryGetValue(type, out var exact))
                {
                    converter = exact;
                    break;
                }

                foreach (var candidate in catalog.Converters)
                {
                    if (type.IsSubclassOf(candidate.Key) &&
                        (fallbackType is null || candidate.Key.IsSubclassOf(fallbackType)))
                    {
                        converter = candidate.Value;
                        fallbackType = candidate.Key;
                    }
                }
            }

            if (converter is not null)
            {
                // A converter from a collectible module must not be rooted through a permanent target type.
                if (!converter.GetType().IsCollectible)
                {
                    _convertersByType.Remove(type);
                    _convertersByType.Add(type, new ConverterEntry(converter, version));
                }

                return converter;
            }
        }

        return throwIfNotFound
            ? throw new InvalidOperationException(
                $"No text converter for type '{type.FullName}' in any loaded assembly."
            )
            : null;
    }

    private static ConverterCatalog CreateCatalog(Assembly assembly)
    {
        var converters = new Dictionary<Type, IHumanReadableConverter>();
        var contract = typeof(IHumanReadableConverter).Assembly;
        if (assembly != contract &&
            !assembly.IsDynamic &&
            !assembly.GetReferencedAssemblies().Any(reference => reference.Name == contract.GetName().Name))
        {
            return new ConverterCatalog(converters);
        }

        foreach (var type in assembly.DefinedTypes)
        {
            var attribute = type.GetCustomAttribute<HumanReadableConverterAttribute>();
            if (attribute is null || type.IsAbstract)
            {
                continue;
            }

            if (!typeof(IHumanReadableConverter).IsAssignableFrom(type))
            {
                throw new InvalidOperationException(
                    $"Text converter '{type.FullName}' must implement {nameof(IHumanReadableConverter)}."
                );
            }

            converters.TryAdd(attribute.Type, (IHumanReadableConverter)Activator.CreateInstance(type.AsType())!);
        }

        return new ConverterCatalog(converters);
    }
}
