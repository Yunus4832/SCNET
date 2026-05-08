using System.Xml.Linq;
using Engine.Serialization;
using EntitySystem.XmlUtilities;

namespace EntitySystem.TemplatesDatabase;

public static class XmlDatabaseSerializer
{
    public static Database LoadDatabase(XElement node)
    {
        var dictionary = new Dictionary<string, DatabaseObjectType>();
        var xElement = XmlUtils.FindChildElement(node, "DatabaseObjectTypes", true)!;
        foreach (var item in xElement.Elements())
        {
            var attributeValue = XmlUtils.GetAttributeValue<string>(item, "Name");
            var attributeValue2 = XmlUtils.GetAttributeValue<string>(item, "DefaultInstanceName");
            var attributeValue3 = XmlUtils.GetAttributeValue<string>(item, "IconName");
            var attributeValue4 = XmlUtils.GetAttributeValue<int>(item, "Order");
            var attributeValue5 = XmlUtils.GetAttributeValue<bool>(item, "SupportsValue");
            var attributeValue6 = XmlUtils.GetAttributeValue<bool>(item, "MustInherit");
            var attributeValue7 = XmlUtils.GetAttributeValue<int>(item, "NameLengthLimit");
            var attributeValue8 = XmlUtils.GetAttributeValue<bool>(item, "SaveStandalone");
            var value = new DatabaseObjectType(attributeValue, attributeValue2, attributeValue3, attributeValue4,
                attributeValue5, attributeValue6, attributeValue7, attributeValue8);
            dictionary.Add(attributeValue, value);
        }

        foreach (var item2 in xElement.Elements())
        {
            var attributeValue9 = XmlUtils.GetAttributeValue<string>(item2, "Name");
            var attributeValue10 = XmlUtils.GetAttributeValue<string>(item2, "AllowedNestingParents");
            var attributeValue11 = XmlUtils.GetAttributeValue<string>(item2, "AllowedInheritanceParents");
            var attributeValue12 = XmlUtils.GetAttributeValue<string>(item2, "NestedValueType");
            var list = new List<DatabaseObjectType>();
            var array = attributeValue10.Split([',', ' '], StringSplitOptions.RemoveEmptyEntries);
            foreach (var text in array)
            {
                if (!dictionary.TryGetValue(text, out var value2))
                {
                    throw new InvalidOperationException($"Database object type \"{text}\" not found.");
                }

                list.Add(value2);
            }

            var list2 = new List<DatabaseObjectType>();
            array = attributeValue11.Split([',', ' '], StringSplitOptions.RemoveEmptyEntries);
            foreach (var text2 in array)
            {
                if (!dictionary.TryGetValue(text2, out var value3))
                {
                    throw new InvalidOperationException($"Database object type \"{text2}\" not found.");
                }

                list2.Add(value3);
            }

            DatabaseObjectType? value4 = null;
            if (!string.IsNullOrEmpty(attributeValue12) && !dictionary.TryGetValue(attributeValue12, out value4))
            {
                throw new InvalidOperationException($"Database object type \"{attributeValue12}\" not found.");
            }

            dictionary[attributeValue9].InitializeRelations(list, list2, value4!);
        }

        foreach (var item3 in XmlUtils.FindChildElement(node, "Assemblies", true)!.Elements())
        {
            Assembly.Load(new AssemblyName(XmlUtils.GetAttributeValue<string>(item3, "Name")));
        }

        var node2 = XmlUtils.FindChildElement(node, "DatabaseObjects", true)!;
        var database =
            new Database(
                new DatabaseObject(guid: XmlUtils.GetAttributeValue<Guid>(node2, "RootGuid"),
                    databaseObjectType: dictionary["Root"], name: "Root", value: null), dictionary.Values);
        foreach (var item4 in LoadDatabaseObjectsList(node2, database))
        {
            item4.NestingParent = database.Root;
        }

        return database;
    }

    public static List<DatabaseObject> LoadDatabaseObjectsList(XElement node, Database database,
        bool generateNewGuids = false)
    {
        var dictionary = new Dictionary<DatabaseObject, Guid>();
        var dictionary2 = new Dictionary<DatabaseObject, Guid>();
        var dictionary3 = generateNewGuids ? new Dictionary<Guid, Guid>() : null;
        var list = InternalLoadDatabaseObjectsList(node, database, dictionary, dictionary2, dictionary3);
        var dictionary4 = new Dictionary<Guid, DatabaseObject>();
        foreach (var item in list)
        {
            dictionary4.Add(item.Guid, item);
            foreach (var explicitNestingChild in item.GetExplicitNestingChildren(null, false))
            {
                dictionary4.Add(explicitNestingChild.Guid, explicitNestingChild);
            }
        }

        foreach (var item2 in dictionary)
        {
            var key = item2.Value;
            if (dictionary3 != null && dictionary3.TryGetValue(key, out var value))
            {
                key = value;
            }

            if (!dictionary4.TryGetValue(key, out var value2))
            {
                throw new InvalidOperationException(
                    $"Required nesting parent {item2.Value} not found in database objects list.");
            }

            item2.Key.NestingParent = value2;
        }

        foreach (var item3 in dictionary2)
        {
            var guid = item3.Value;
            if (dictionary3 != null && dictionary3.TryGetValue(guid, out var value3))
            {
                guid = value3;
            }

            item3.Key.ExplicitInheritanceParent = dictionary4.TryGetValue(guid, out var value4)
                ? value4
                : database.FindDatabaseObject(guid, null, true);
        }

        return list.Where(x => x.NestingParent == null).ToList();
    }

    public static DatabaseObject LoadDatabaseObject(XElement node, Database database)
    {
        var dictionary = new Dictionary<DatabaseObject, Guid>();
        var result = InternalLoadDatabaseObject(node, database, null, dictionary, null);
        foreach (var item in dictionary)
        {
            item.Key.ExplicitInheritanceParent = database.FindDatabaseObject(item.Value, null, true);
        }

        return result;
    }

    public static void SaveDatabase(XElement node, Database database)
    {
        var parentNode = XmlUtils.AddElement(node, "DatabaseObjectTypes");
        foreach (var databaseObjectType in database.DatabaseObjectTypes)
        {
            var node2 = XmlUtils.AddElement(parentNode, "DatabaseObjectType");
            XmlUtils.SetAttributeValue(node2, "Name", databaseObjectType.Name);
            XmlUtils.SetAttributeValue(node2, "DefaultInstanceName", databaseObjectType.DefaultInstanceName);
            XmlUtils.SetAttributeValue(node2, "IconName",
                !string.IsNullOrEmpty(databaseObjectType.IconName) ? databaseObjectType.IconName : string.Empty);
            XmlUtils.SetAttributeValue(node2, "Order", databaseObjectType.Order);
            XmlUtils.SetAttributeValue(node2, "SupportsValue", databaseObjectType.SupportsValue);
            XmlUtils.SetAttributeValue(node2, "MustInherit", databaseObjectType.MustInherit);
            XmlUtils.SetAttributeValue(node2, "NameLengthLimit", databaseObjectType.NameLengthLimit);
            XmlUtils.SetAttributeValue(node2, "SaveStandalone", databaseObjectType.SaveStandalone);
            XmlUtils.SetAttributeValue(node2, "AllowedNestingParents",
                databaseObjectType.AllowedNestingParents.Aggregate(string.Empty,
                    (r, d) => r.Length != 0 ? r + "," + d.Name : d.Name));
            XmlUtils.SetAttributeValue(node2, "AllowedInheritanceParents",
                databaseObjectType.AllowedInheritanceParents.Aggregate(string.Empty,
                    (r, d) => r.Length != 0 ? r + "," + d.Name : d.Name));
            XmlUtils.SetAttributeValue(node2, "NestedValueType", databaseObjectType.NestedValueType.Name);
        }

        var list = new List<Type>();
        database.FindUsedValueTypes(list);
        var list2 = new List<Assembly>();
        foreach (var item in list)
        {
            if (!list2.Contains(item.GetTypeInfo().Assembly))
            {
                list2.Add(item.GetTypeInfo().Assembly);
            }
        }

        list2.Sort((a1, a2) => string.CompareOrdinal(a1.FullName, a2.FullName));
        var parentNode2 = XmlUtils.AddElement(node, "Assemblies");
        foreach (var item2 in list2)
        {
            XmlUtils.SetAttributeValue(XmlUtils.AddElement(parentNode2, "Assembly"), "Name",
                item2.GetName().Name ?? throw new InvalidOperationException("Cannot get AssemblyName"));
        }

        var node3 = XmlUtils.AddElement(node, "DatabaseObjects");
        XmlUtils.SetAttributeValue(node3, "RootGuid", database.Root.Guid);
        SaveDatabaseObjectsList(node3, database.Root.GetExplicitNestingChildren(null, true));
    }

    public static void SaveDatabaseObjectsList(XElement node, IEnumerable<DatabaseObject> databaseObjects)
    {
        var list = new List<DatabaseObject>();
        var databaseObjectArray = databaseObjects as DatabaseObject[] ?? databaseObjects.ToArray();
        foreach (var databaseObject in databaseObjectArray)
        {
            list.AddRange(from x in databaseObject.GetExplicitNestingChildren(null, false)
                where x.Type.SaveStandalone
                select x);
        }

        InternalSaveDatabaseObjectsList(node, list, true);
        InternalSaveDatabaseObjectsList(node, databaseObjectArray, false);
    }

    public static void SaveDatabaseObject(XElement node, DatabaseObject databaseObject)
    {
        InternalSaveDatabaseObject(node, databaseObject, false);
    }

    private static List<DatabaseObject> InternalLoadDatabaseObjectsList(
        XElement node,
        Database database,
        Dictionary<DatabaseObject, Guid>? nestingParents,
        Dictionary<DatabaseObject, Guid>? inheritanceParents,
        Dictionary<Guid, Guid>? guidTranslation
    )
    {
        return node.Elements().Select(item2 =>
            InternalLoadDatabaseObject(item2, database, nestingParents, inheritanceParents, guidTranslation)).ToList();
    }

    private static DatabaseObject InternalLoadDatabaseObject(
        XElement node,
        Database database,
        Dictionary<DatabaseObject, Guid>? nestingParents,
        Dictionary<DatabaseObject, Guid>? inheritanceParents,
        Dictionary<Guid, Guid>? guidTranslation
    )
    {
        var guid = XmlUtils.GetAttributeValue(node, "Guid", Guid.Empty);
        var attributeValue = XmlUtils.GetAttributeValue(node, "Name", string.Empty);
        var attributeValue2 = XmlUtils.GetAttributeValue(node, "Description", string.Empty);
        var attributeValue3 = XmlUtils.GetAttributeValue(node, "NestingParent", Guid.Empty);
        var attributeValue4 = XmlUtils.GetAttributeValue(node, "InheritanceParent", Guid.Empty);
        var attributeValue5 = XmlUtils.GetAttributeValue(node, "Type", string.Empty);
        if (guid == Guid.Empty)
        {
            guid = Guid.NewGuid();
        }

        if (guidTranslation != null)
        {
            var guid2 = Guid.NewGuid();
            guidTranslation.Add(guid, guid2);
            guid = guid2;
        }

        var databaseObjectType = database.FindDatabaseObjectType(node.Name.ToString(), true)!;
        object? value = null;
        if (!string.IsNullOrEmpty(attributeValue5))
        {
            var type = TypeCache.FindType(attributeValue5, false, true)!;
            value = XmlUtils.GetAttributeValue(node, "Value", type);
        }

        var databaseObject = new DatabaseObject(databaseObjectType, guid, attributeValue, value)
        {
            Description = attributeValue2
        };
        if (nestingParents != null && attributeValue3 != Guid.Empty)
        {
            nestingParents.Add(databaseObject, attributeValue3);
        }

        if (inheritanceParents != null && attributeValue4 != Guid.Empty)
        {
            inheritanceParents.Add(databaseObject, attributeValue4);
        }

        foreach (var item in InternalLoadDatabaseObjectsList(node, database, nestingParents, inheritanceParents,
                     guidTranslation))
        {
            item.NestingParent = databaseObject;
        }

        return databaseObject;
    }

    public static void InternalSaveDatabaseObjectsList(
        XElement node,
        IEnumerable<DatabaseObject> databaseObjects,
        bool saveNestingParents
    )
    {
        var list = new List<DatabaseObject>(databaseObjects);
        list.Sort((o1, o2) =>
            o1.Type.Order != o2.Type.Order ? o1.Type.Order - o2.Type.Order : o1.Guid.CompareTo(o2.Guid));
        foreach (var item in list)
        {
            InternalSaveDatabaseObject(XmlUtils.AddElement(node, item.Type.Name), item, saveNestingParents);
        }
    }

    private static void InternalSaveDatabaseObject(XElement node, DatabaseObject databaseObject, bool saveNestingParent)
    {
        XmlUtils.SetAttributeValue(node, "Name", databaseObject.Name);
        if (!string.IsNullOrEmpty(databaseObject.Description))
        {
            XmlUtils.SetAttributeValue(node, "Description", databaseObject.Description);
        }

        XmlUtils.SetAttributeValue(node, "Guid", databaseObject.Guid);
        XmlUtils.SetAttributeValue(node, "Value", databaseObject.Value);
        XmlUtils.SetAttributeValue(node, "Type",
            TypeCache.GetShortTypeName(databaseObject.Value.GetType().FullName ??
                                       throw new InvalidOperationException("Cannot get type FullName")));

        if (databaseObject.ExplicitInheritanceParent != null)
        {
            XmlUtils.SetAttributeValue(node, "InheritanceParent", databaseObject.ExplicitInheritanceParent.Guid);
        }

        if (saveNestingParent && databaseObject.NestingParent != null)
        {
            XmlUtils.SetAttributeValue(node, "NestingParent", databaseObject.NestingParent.Guid);
        }

        InternalSaveDatabaseObjectsList(node, from x in databaseObject.GetExplicitNestingChildren(null, true)
            where !x.Type.SaveStandalone
            select x, false);
    }
}
