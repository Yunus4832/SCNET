using System.Reflection;

using Game.Players;
using Game.Subsystems;
using Game.Teams;

namespace Survivalcraft.Test.Subsystems;

internal static class TeamTestSupport
{
    // Minimal command/query fixtures have no database-backed subsystem lifecycle.
    // Production initialization and template bindings are covered separately.
    public static SubsystemTeams CreateSubsystem(WorldPlayerStore players)
    {
        var subsystem = new SubsystemTeams();
        typeof(SubsystemTeams).GetField("_store", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(subsystem, new TeamStore(players));
        return subsystem;
    }
}
