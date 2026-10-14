namespace Game.Commands;

[Flags]
public enum CommandPrincipalKinds
{
    None = 0,
    LocalOperator = 1,
    Player = 2,
    System = 4
}
