namespace Engine.Serialization;

public static class HumanReadableConverter
{
    private static readonly Dictionary<Type, IHumanReadableConverter> _humanReadableConvertersByType = new();

    private static readonly HashSet<Assembly> _scannedAssemblies = [];

    public static string ConvertToString(object value)
    {
        var type = value.GetType();
        try
        {
            return GetConverter(type, true)!.ConvertToString(value);
        }
        catch (Exception innerException)
        {
            throw new InvalidOperationException($"Cannot convert value of type \"{type.FullName}\" to string.",
                innerException);
        }
    }

    public static bool TryConvertFromString(Type type, string data, out object? result)
    {
        try
        {
            result = GetConverter(type, true)!.ConvertFromString(type, data);
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
            return GetConverter(type, true)!.ConvertFromString(type, data);
        }
        catch
        {
            return Guid.Empty;
            //throw new InvalidOperationException($"Cannot convert string \"{data}\" to value of type \"{type.FullName}\".", innerException);
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
        lock (_humanReadableConvertersByType)
        {
            if (!_humanReadableConvertersByType.TryGetValue(type, out var value))
            {
                ScanAssembliesForConverters();
                if (!_humanReadableConvertersByType.TryGetValue(type, out value))
                {
                    if (value == null)
                    {
                        foreach (var item in _humanReadableConvertersByType)
                        {
                            if (!type.GetTypeInfo().IsSubclassOf(item.Key))
                            {
                                continue;
                            }

                            value = item.Value;
                            break;
                        }
                    }

                    _humanReadableConvertersByType.Add(type, value!);
                }
            }

            if (value != null)
            {
                return value;
            }

            if (throwIfNotFound)
            {
                throw new InvalidOperationException(
                    $"IHumanReadableConverter for type \"{type.FullName}\" not found in any loaded assembly.");
            }

            return null;
        }
    }

    private static void ScanAssembliesForConverters()
    {
        foreach (var item in TypeCache.LoadedAssemblies.Where(a => !TypeCache.IsKnownSystemAssembly(a)))
        {
            if (_scannedAssemblies.Contains(item))
            {
                continue;
            }

            foreach (var definedType in item.DefinedTypes)
            {
                var customAttribute = definedType.GetCustomAttribute<HumanReadableConverterAttribute>();
                if (customAttribute == null || _humanReadableConvertersByType.ContainsKey(customAttribute.Type))
                {
                    continue;
                }

                var value = (IHumanReadableConverter)Activator.CreateInstance(definedType.AsType())!;
                _humanReadableConvertersByType.Add(customAttribute.Type, value);
            }

            _scannedAssemblies.Add(item);
        }
    }
}
