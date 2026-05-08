using Engine.Core;

namespace Engine.Serialization;

public static class TypeCache
{
    private static readonly Dictionary<string, Type?> _typesByName;

    private static readonly Dictionary<string, string> _shortToLong;

    private static readonly Dictionary<string, string> _longToShort;

    private static List<Assembly> _loadedAssemblies;

    private static bool _rescanAssemblies;

    static TypeCache()
    {
        _typesByName = new Dictionary<string, Type?>();
        _shortToLong = new Dictionary<string, string>();
        _longToShort = new Dictionary<string, string>();
        _loadedAssemblies = [];
        _rescanAssemblies = true;
        AddShortTypeName("bool", typeof(bool).FullName!);
        AddShortTypeName("sbyte", typeof(sbyte).FullName!);
        AddShortTypeName("byte", typeof(byte).FullName!);
        AddShortTypeName("short", typeof(short).FullName!);
        AddShortTypeName("ushort", typeof(ushort).FullName!);
        AddShortTypeName("int", typeof(int).FullName!);
        AddShortTypeName("uint", typeof(uint).FullName!);
        AddShortTypeName("long", typeof(long).FullName!);
        AddShortTypeName("ulong", typeof(ulong).FullName!);
        AddShortTypeName("float", typeof(float).FullName!);
        AddShortTypeName("double", typeof(double).FullName!);
        AddShortTypeName("char", typeof(char).FullName!);
        AddShortTypeName("string", typeof(string).FullName!);
        AddShortTypeName("Vector2", typeof(Vector2).FullName!);
        AddShortTypeName("Vector3", typeof(Vector3).FullName!);
        AddShortTypeName("Vector4", typeof(Vector4).FullName!);
        AddShortTypeName("Quaternion", typeof(Quaternion).FullName!);
        AddShortTypeName("Matrix", typeof(Matrix).FullName!);
        AddShortTypeName("Color", typeof(Color).FullName!);
        AddShortTypeName("Point2", typeof(Point2).FullName!);
        AddShortTypeName("Point3", typeof(Point3).FullName!);
        AddShortTypeName("Rectangle", typeof(Rectangle).FullName!);
        AddShortTypeName("Box", typeof(Box).FullName!);
        AddShortTypeName("BoundingRectangle", typeof(BoundingRectangle).FullName!);
        AddShortTypeName("BoundingBox", typeof(BoundingBox).FullName!);
        AddShortTypeName("BoundingCircle", typeof(BoundingCircle).FullName!);
        AddShortTypeName("BoundingSphere", typeof(BoundingSphere).FullName!);
        AddShortTypeName("Plane", typeof(Plane).FullName!);
        AddShortTypeName("Ray2", typeof(Ray2).FullName!);
        AddShortTypeName("Ray3", typeof(Ray3).FullName!);
        AppDomain.CurrentDomain.AssemblyLoad += delegate
        {
            lock (_typesByName)
            {
                _rescanAssemblies = true;
            }
        };
    }

    public static ReadOnlyList<Assembly> LoadedAssemblies
    {
        get
        {
            lock (_typesByName)
            {
                if (_rescanAssemblies)
                {
                    _loadedAssemblies = new List<Assembly>(AppDomain.CurrentDomain.GetAssemblies());
                    _rescanAssemblies = false;
                }

                return new ReadOnlyList<Assembly>(_loadedAssemblies);
            }
        }
    }

    public static bool IsKnownSystemAssembly(Assembly assembly)
    {
        var text = assembly.FullName?.ToLower();
        if (text is null)
        {
            return false;
        }

        if (text.Contains("b77a5c561934e089"))
        {
            return true;
        }

        if (text.Contains("31bf3856ad364e35"))
        {
            return true;
        }

        if (text.Contains("b03f5f7f11d50a3a"))
        {
            return true;
        }

        if (text.Contains("89845dcd8080cc91"))
        {
            return true;
        }

        if (text.Contains("opentk"))
        {
            return true;
        }

        if (text.Contains("sharpdx"))
        {
            return true;
        }

        return false;
    }

    public static Type? FindType(string typeName, bool skipSystemAssemblies, bool throwIfNotFound)
    {
        lock (_typesByName)
        {
            if (_typesByName.TryGetValue(typeName, out var value))
            {
                return value;
            }

            var longTypeName = GetLongTypeName(typeName);
            foreach (var loadedAssembly in LoadedAssemblies)
            {
                if (skipSystemAssemblies && IsKnownSystemAssembly(loadedAssembly))
                {
                    continue;
                }

                value = loadedAssembly.GetType(longTypeName);
                if (value is not null)
                {
                    break;
                }
            }

            if (value is null)
            {
                if (throwIfNotFound)
                {
                    throw new InvalidOperationException(
                        $"Type \"{longTypeName}\" not found in any loaded assembly.");
                }

                return null;
            }

            _typesByName.Add(typeName, value);

            return value;
        }
    }

    public static string GetShortTypeName(string longTypeName)
    {
        return _longToShort.TryGetValue(longTypeName, out var value) ? value : longTypeName;
    }

    public static string GetLongTypeName(string shortTypeName)
    {
        return _shortToLong.TryGetValue(shortTypeName, out var value) ? value : shortTypeName;
    }

    private static void AddShortTypeName(string shortTypeName, string longTypeName)
    {
        _shortToLong.Add(shortTypeName, longTypeName);
        _longToShort.Add(longTypeName, shortTypeName);
    }
}
