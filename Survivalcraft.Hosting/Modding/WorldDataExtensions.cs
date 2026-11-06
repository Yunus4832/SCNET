using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Modding;

public static class WorldDataExtensions
{
    public static ValuesDictionary GetWorldData(this IModContext context, Project project, string key)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(project);
        return project.ExtensionData.Get(context.Manifest.ModId.Value, key);
    }
}
