using System.Reflection;

using EntitySystem.Core;

namespace Survivalcraft.Test.Subsystems;

internal static class WorldSubsystemTestSupport
{
    // Minimal command/lifecycle fixtures do not run the database-backed world loader.
    public static void Attach(Project project, Subsystem subsystem)
    {
        typeof(Subsystem).GetProperty(nameof(Subsystem.Project))!.SetValue(subsystem, project);
        typeof(Subsystem).GetField("_initialized", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(
            subsystem,
            true
        );
    }
}
