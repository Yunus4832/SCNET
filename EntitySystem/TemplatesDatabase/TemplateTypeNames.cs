using Engine.Core.Mathematics;
using Engine.Core.Reflection;

namespace EntitySystem.TemplatesDatabase;

/// <summary>Type names owned by the template and values-dictionary XML format.</summary>
public static class TemplateTypeNames
{
    private static readonly Dictionary<string, Type> _typesByName = new(StringComparer.Ordinal)
    {
        ["bool"] = typeof(bool),
        ["sbyte"] = typeof(sbyte),
        ["byte"] = typeof(byte),
        ["short"] = typeof(short),
        ["ushort"] = typeof(ushort),
        ["int"] = typeof(int),
        ["uint"] = typeof(uint),
        ["long"] = typeof(long),
        ["ulong"] = typeof(ulong),
        ["float"] = typeof(float),
        ["double"] = typeof(double),
        ["char"] = typeof(char),
        ["string"] = typeof(string),
        ["Vector2"] = typeof(Vector2),
        ["Vector3"] = typeof(Vector3),
        ["Vector4"] = typeof(Vector4),
        ["Quaternion"] = typeof(Quaternion),
        ["Matrix"] = typeof(Matrix),
        ["Color"] = typeof(Color),
        ["Point2"] = typeof(Point2),
        ["Point3"] = typeof(Point3),
        ["Rectangle"] = typeof(Rectangle),
        ["Box"] = typeof(Box),
        ["BoundingRectangle"] = typeof(BoundingRectangle),
        ["BoundingBox"] = typeof(BoundingBox),
        ["BoundingCircle"] = typeof(BoundingCircle),
        ["BoundingSphere"] = typeof(BoundingSphere),
        ["Plane"] = typeof(Plane),
        ["Ray2"] = typeof(Ray2),
        ["Ray3"] = typeof(Ray3),
    };

    private static readonly Dictionary<string, string> _namesByFullName =
        _typesByName.ToDictionary(pair => pair.Value.FullName!, pair => pair.Key, StringComparer.Ordinal);

    public static Type? Resolve(string name, bool throwIfNotFound = true)
    {
        return _typesByName.TryGetValue(name, out var type)
            ? type
            : RuntimeTypeResolver.FindType(name, throwIfNotFound);
    }

    public static string GetShortTypeName(string fullName)
    {
        return _namesByFullName.GetValueOrDefault(fullName, fullName);
    }
}
