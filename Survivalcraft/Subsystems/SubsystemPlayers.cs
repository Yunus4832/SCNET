using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Players;

namespace Game.Subsystems;

/// <summary>Persistent player identity, permissions, restrictions and character ownership.</summary>
public sealed class SubsystemPlayers : Subsystem
{
    public WorldPlayerStore Store { get; } = new();

    public override void Load(ValuesDictionary valuesDictionary)
    {
        Store.Load(Project.ExtensionData.Get("game", "players"));
    }

    public override void Save(ValuesDictionary valuesDictionary)
    {
        if (Project.SendToClientMode) { return; }

        var data = Project.ExtensionData.Get("game", "players");
        data.Clear();
        data.ApplyOverrides(Store.Save());
    }
}
