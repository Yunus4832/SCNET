using EntitySystem.TemplatesDatabase;

namespace EntitySystem.Core;

public class GameDatabase(Database database)
{
    public Database Database { get; private set; } = database;

    public DatabaseObjectType FolderType { get; private set; } = database.FindDatabaseObjectType("Folder", true)!;

    public DatabaseObjectType ProjectTemplateType { get; private set; } = database.FindDatabaseObjectType("ProjectTemplate", true)!;

    public DatabaseObjectType MemberSubsystemTemplateType { get; private set; } = database.FindDatabaseObjectType("MemberSubsystemTemplate", true)!;

    public DatabaseObjectType SubsystemTemplateType { get; private set; } = database.FindDatabaseObjectType("SubsystemTemplate", true)!;

    public DatabaseObjectType EntityTemplateType { get; private set; } = database.FindDatabaseObjectType("EntityTemplate", true)!;

    public DatabaseObjectType MemberComponentTemplateType { get; private set; } = database.FindDatabaseObjectType("MemberComponentTemplate", true)!;

    public DatabaseObjectType ComponentTemplateType { get; private set; } = database.FindDatabaseObjectType("ComponentTemplate", true)!;

    public DatabaseObjectType ParameterSetType { get; private set; } = database.FindDatabaseObjectType("ParameterSet", true)!;

    public DatabaseObjectType ParameterType { get; private set; } = database.FindDatabaseObjectType("Parameter", true)!;
}
