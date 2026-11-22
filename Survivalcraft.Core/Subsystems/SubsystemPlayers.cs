using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Subsystems;

/// <summary>记录玩家的持久身份、权限、限制及角色归属。</summary>
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
