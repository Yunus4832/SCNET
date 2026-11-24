using System.Xml.Linq;

using Engine.Core.Reflection;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Managers;

public static class DatabaseManager
{
    public static GameDatabase Create(XElement node, Func<string, bool, Type?> findType) =>
        new(XmlDatabaseSerializer.LoadDatabase(node, findType));

    public static Entity? CreateEntity(Project project, string entityTemplateName, bool throwIfNotFound)
    {
        var valuesDictionary = project.GameDatabase.FindEntityValuesDictionary(entityTemplateName, throwIfNotFound);
        return valuesDictionary == null ? null : project.CreateEntity(valuesDictionary);
    }

    public static Entity? CreateEntity(
        Project project,
        string entityTemplateName,
        ValuesDictionary overrides,
        bool throwIfNotFound
    )
    {
        var valuesDictionary = project.GameDatabase.FindEntityValuesDictionary(entityTemplateName, throwIfNotFound);
        if (valuesDictionary == null)
        {
            return null;
        }

        var entityValues = CopyValuesDictionary(valuesDictionary);
        entityValues.ApplyOverrides(overrides);
        return project.CreateEntity(entityValues);
    }

    private static ValuesDictionary CopyValuesDictionary(ValuesDictionary source)
    {
        var copy = new ValuesDictionary { DatabaseObject = source.DatabaseObject };
        foreach (var item in source)
        {
            copy.SetValue(item.Key, item.Value is ValuesDictionary nested ? CopyValuesDictionary(nested) : item.Value);
        }

        return copy;
    }
}
