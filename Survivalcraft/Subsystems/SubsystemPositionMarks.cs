using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Protocol.Identity;

namespace Game.Subsystems;

/// <summary>World-owned public marks and player-addressed private marks, independent of role lifetimes.</summary>
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
