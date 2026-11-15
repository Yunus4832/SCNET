using EntitySystem.Core;

namespace Game.Commands.Http;

public sealed record CommandExecutionScope(
    CommandRegistry Registry,
    Project? ApplicationProject,
    AuthorityWorldExecutor? Authority
);
