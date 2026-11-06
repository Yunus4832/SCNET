using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Commands;

namespace Game.Subsystems;

public sealed class SubsystemClientModServices : Subsystem
{
    private CommandRegistry? _commands;

    private PlayerContextActionHooks? _contextActions;

    public CommandRegistry Commands =>
        _commands ?? throw new InvalidOperationException("Client commands have not been bound.");

    public PlayerContextActionHooks ContextActions => _contextActions ??
                                                      throw new InvalidOperationException(
                                                          "Client context actions have not been bound."
                                                      );

    public void Bind(CommandRegistry commands, PlayerContextActionHooks contextActions)
    {
        ArgumentNullException.ThrowIfNull(commands);
        ArgumentNullException.ThrowIfNull(contextActions);
        if (_commands is not null) { throw new InvalidOperationException("Client mod services are already bound."); }

        _commands = commands;
        _contextActions = contextActions;
    }

    public override void Load(ValuesDictionary valuesDictionary)
    {
        _ = Commands;
        _ = ContextActions;
    }
}
