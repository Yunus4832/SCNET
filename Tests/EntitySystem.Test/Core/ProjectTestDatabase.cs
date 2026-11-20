using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace EntitySystem.Test.Core;

internal static class ProjectTestDatabase
{
    public static (GameDatabase Database, DatabaseObject Template) Create()
    {
        var names = new[]
        {
            "Folder", "ProjectTemplate", "MemberSubsystemTemplate", "SubsystemTemplate", "EntityTemplate",
            "MemberComponentTemplate", "ComponentTemplate", "ParameterSet", "Parameter"
        };
        var types = names
            .Select(name => new DatabaseObjectType(name, name, name, 0, name == "Parameter", false, 64, false))
            .ToArray();
        foreach (var type in types)
        {
            type.InitializeRelations(types, null, types[^1]);
        }

        var root = new DatabaseObject(types[0], "Root");
        var database = new Database(root, types);
        var template = new DatabaseObject(types[1], "TestProject") { NestingParent = root };
        return (new GameDatabase(database), template);
    }
}
