using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.World;

namespace Survivalcraft.Core.Test.Fixtures;

internal sealed class CoreTestProject : Project
{
    public CoreTestProject(RuntimeSide side)
    {
        var names = new[]
        {
            "Folder", "ProjectTemplate", "MemberSubsystemTemplate", "SubsystemTemplate", "EntityTemplate",
            "MemberComponentTemplate", "ComponentTemplate", "ParameterSet", "Parameter"
        };
        var types = names.Select(name =>
            new DatabaseObjectType(name, name, name, 0, name == "Parameter", false, 64, false)
        ).ToArray();
        foreach (var type in types)
        {
            type.InitializeRelations(types, null, types[^1]);
        }

        var root = new DatabaseObject(types[0], "Root");
        GameDatabase = new GameDatabase(new Database(root, types));
        ProjectTemplate = new DatabaseObject(types[1], "TestWorld") { NestingParent = root };
        new WorldRuntimeContext(side, false).BindProject(this);
    }

    public void AddSubsystem(Subsystem subsystem)
    {
        InitializeSubsystem(subsystem);
        subsystems.Add(subsystem);
    }

    public void InitializeSubsystem(Subsystem subsystem)
    {
        var definition = new DatabaseObject(GameDatabase.MemberSubsystemTemplateType, subsystem.GetType().Name)
        {
            NestingParent = ProjectTemplate
        };
        var values = new ValuesDictionary();
        values.PopulateFromDatabaseObject(definition);
        subsystem.Initialize(this, values);
    }
}
