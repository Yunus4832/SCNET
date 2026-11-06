using Game.Startup;

namespace Game.Commands.BuiltIn;

public sealed record GetNetworkSendStatisticsCommand : IGameCommand;

public sealed record RestartApplicationCommand(SessionInfo? RestartSession = null) : IGameCommand;

public sealed record SwitchInstanceCommand(string InstanceId) : IGameCommand;

public sealed record CreateInstanceCommand(string InstanceId) : IGameCommand;

public sealed record DeleteInstanceCommand(string InstanceId) : IGameCommand;

public sealed record CloneInstanceCommand(
    string SourceInstanceId,
    string TargetInstanceId
) : IGameCommand;

public sealed record ExitApplicationCommand : IGameCommand;

public sealed record GetLanguageCommand : IGameCommand;

public sealed record SetLanguageCommand(string LanguageType) : IGameCommand;
