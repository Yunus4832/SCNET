using EntitySystem.TemplatesDatabase;

namespace EntitySystem.Core;

public class GameDatabase(Database database)
{
    private readonly Dictionary<string, ValuesDictionary> _valueDictionaries = CreateEntityTemplates(database);

    public ICollection<ValuesDictionary> EntitiesValuesDictionaries => _valueDictionaries.Values;

    private static Dictionary<string, ValuesDictionary> CreateEntityTemplates(Database database)
    {
        var templates = new Dictionary<string, ValuesDictionary>(StringComparer.Ordinal);
        var type = database.FindDatabaseObjectType("EntityTemplate", true)!;
        foreach (var definition in database.Root.GetExplicitNestingChildren(type, false))
        {
            var values = new ValuesDictionary();
            values.PopulateFromDatabaseObject(definition);
            templates.Add(definition.Name, values);
        }

        return templates;
    }

    public ValuesDictionary? FindEntityValuesDictionary(string entityTemplateName, bool throwIfNotFound)
    {
        if (!_valueDictionaries.TryGetValue(entityTemplateName, out var value) && throwIfNotFound)
        {
            throw new InvalidOperationException($"EntityTemplate \"{entityTemplateName}\" not found.");
        }

        return value;
    }

    public ValuesDictionary? FindValuesDictionaryForComponent(ValuesDictionary entityVd, Type componentType)
    {
        foreach (var item in entityVd.Values.OfType<ValuesDictionary>())
        {
            if (item.DatabaseObject.Type != MemberComponentTemplateType)
            {
                continue;
            }

            var type = Database.FindType(item.GetValue<string>("Class"), true)!;
            if (componentType.IsAssignableFrom(type))
            {
                return item;
            }
        }

        return null;
    }

    public Database Database { get; private set; } = database;

    public DatabaseObjectType FolderType { get; private set; } = database.FindDatabaseObjectType("Folder", true)!;

    public DatabaseObjectType ProjectTemplateType { get; private set; } =
        database.FindDatabaseObjectType("ProjectTemplate", true)!;

    public DatabaseObjectType MemberSubsystemTemplateType { get; private set; } =
        database.FindDatabaseObjectType("MemberSubsystemTemplate", true)!;

    public DatabaseObjectType SubsystemTemplateType { get; private set; } =
        database.FindDatabaseObjectType("SubsystemTemplate", true)!;

    public DatabaseObjectType EntityTemplateType { get; private set; } =
        database.FindDatabaseObjectType("EntityTemplate", true)!;

    public DatabaseObjectType MemberComponentTemplateType { get; private set; } =
        database.FindDatabaseObjectType("MemberComponentTemplate", true)!;

    public DatabaseObjectType ComponentTemplateType { get; private set; } =
        database.FindDatabaseObjectType("ComponentTemplate", true)!;

    public DatabaseObjectType ParameterSetType { get; private set; } =
        database.FindDatabaseObjectType("ParameterSet", true)!;

    public DatabaseObjectType ParameterType { get; private set; } = database.FindDatabaseObjectType("Parameter", true)!;
}
