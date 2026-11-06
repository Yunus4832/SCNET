using EntitySystem.Core;

namespace Game.Subsystems;

/// <summary>Owns one world message service for authority routing and client presentation subscribers.</summary>
public sealed class SubsystemMessages : Subsystem
{
    public GameMessageService Service => field ??= new GameMessageService(Project);
}
