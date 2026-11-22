using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Protocol.Identity;

namespace Game.Subsystems;

/// <summary>管理世界公共标记和按玩家寻址的私有标记，其生命周期独立于角色。</summary>
public sealed class SubsystemPositionMarks : Subsystem
{
    private readonly PositionMarkStore _store = new();

    public PositionMarks PublicMarks => _store.PublicMarks;

    public PositionMarks GetPrivateMarks(PlayerAddress player) => _store.GetPrivateMarks(player);

    public override void Load(ValuesDictionary valuesDictionary)
    {
        _store.Load(Project.ExtensionData.Get("game", "position-marks"));
    }

    public override void Save(ValuesDictionary valuesDictionary)
    {
        if (Project.SendToClientMode) { return; }

        var data = Project.ExtensionData.Get("game", "position-marks");
        data.Clear();
        data.ApplyOverrides(_store.Save());
    }
}
