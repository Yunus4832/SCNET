using System.Xml.Linq;

using Engine.Serialization;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Managers;

public static class DatabaseManager
{
    private static GameDatabase? _gameDatabase;

    private static readonly Dictionary<string, ValuesDictionary> _valueDictionaries = new();

    public static XElement? DatabaseNodeField;

    public static XElement? DatabaseNode
    {
        get => DatabaseNodeField;
        set => DatabaseNodeField = value;
    }

    public static GameDatabase GameDatabase =>
        _gameDatabase ?? throw new InvalidOperationException("Database not loaded.");

    public static ICollection<ValuesDictionary> EntitiesValuesDictionaries => _valueDictionaries.Values;

    public static void Initialize()
    {
        _valueDictionaries.Clear();
    }

    public static void LoadDataBaseFromXml(XElement node)
    {
        _gameDatabase = new GameDatabase(XmlDatabaseSerializer.LoadDatabase(node));
        foreach (var explicitNestingChild in GameDatabase.Database.Root.GetExplicitNestingChildren(
                     GameDatabase.EntityTemplateType, false))
        {
            var valuesDictionary = new ValuesDictionary();
            valuesDictionary.PopulateFromDatabaseObject(explicitNestingChild);
            _valueDictionaries.Add(explicitNestingChild.Name, valuesDictionary);
        }
    }

    public static ValuesDictionary? FindEntityValuesDictionary(string entityTemplateName, bool throwIfNotFound)
    {
        if (!_valueDictionaries.TryGetValue(entityTemplateName, out var value) && throwIfNotFound)
        {
            throw new InvalidOperationException($"EntityTemplate \"{entityTemplateName}\" not found.");
        }

        return value;
    }

    public static ValuesDictionary? FindValuesDictionaryForComponent(ValuesDictionary entityVd, Type componentType)
    {
        foreach (var item in entityVd.Values.OfType<ValuesDictionary>())
        {
            if (item.DatabaseObject.Type == GameDatabase.MemberComponentTemplateType)
            {
                var type = TypeCache.FindType(item.GetValue<string>("Class"), true, true)!;
                if (componentType.GetTypeInfo().IsAssignableFrom(type.GetTypeInfo()))
                {
                    return item;
                }
            }
        }

        return null;
    }

    public static Entity? CreateEntity(Project project, string entityTemplateName, bool throwIfNotFound)
    {
        var valuesDictionary = FindEntityValuesDictionary(entityTemplateName, throwIfNotFound);
        return valuesDictionary == null ? null : project.CreateEntity(valuesDictionary);
    }

    public static Entity? CreateEntity(Project project, string entityTemplateName, ValuesDictionary overrides,
        bool throwIfNotFound)
    {
        var valuesDictionary = FindEntityValuesDictionary(entityTemplateName, throwIfNotFound);
        if (valuesDictionary == null)
        {
            return null;
        }

        valuesDictionary.ApplyOverrides(overrides);
        return project.CreateEntity(valuesDictionary);
    }

    /// <summary>
    /// 检查Database数据是否被修改器修改
    /// </summary>
    public static void CheckDataBaseValid()
    {
        Task.Run(() =>
        {
            var startTime = Time.RealTime;
            if (DatabaseNodeField == null)
            {
                return;
            }

            var d = new GameDatabase(XmlDatabaseSerializer.LoadDatabase(DatabaseNodeField));
            var llBase = GetParameterTypeList(GameDatabase);
            var ll = GetParameterTypeList(d);
            var useTime = Time.RealTime - startTime;
            for (var i = 0; i < ll.Count; i++)
            {
                var a = ll[i];
                var b = llBase[i];
                if (a.Value.ToString() != b.Value.ToString())
                {
                    GameEntry.RamDataChangeException("Database", a.Name);
                }
            }
            Log.Debug($"本次检测Database数据耗时：{useTime:0.000}s");
        });
    }

    private static List<DatabaseObject> GetParameterTypeList(GameDatabase database)
    {
        var playerEntityGuid = new Guid("4be6c1c5-d65d-4537-8a8b-a391969e6dc2");
        var djBase = database.Database.FindDatabaseObject(playerEntityGuid, database.EntityTemplateType, true)!;
        var pp = djBase.GetExplicitNestingChildren(database.MemberComponentTemplateType, true);

        return pp.SelectMany(pa => pa.GetExplicitNestingChildren(database.ParameterType, true)).ToList();
    }
}
