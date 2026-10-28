using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Teams;

namespace Game.Subsystems;

public sealed class SubsystemTeams : Subsystem
{
    private TeamStore? _store;

    public TeamStore Store => _store ?? throw new InvalidOperationException("Teams have not been initialized.");

    public override void Load(ValuesDictionary valuesDictionary)
    {
        var players = Project.FindSubsystem<SubsystemPlayers>(true)!;
        _store = new TeamStore(players.Store, GameManager.Role != WorldRole.RemoteClient);
        Store.Load(valuesDictionary.GetValue("Teams", new ValuesDictionary()));
    }

    public void CancelPendingOperations(Game.Protocol.Identity.PlayerAddress player)
    {
        _store?.CancelOperationsFor(player);
    }

    public override void Save(ValuesDictionary valuesDictionary)
    {
        valuesDictionary.SetValue("Teams", Store.Save());
    }
}
