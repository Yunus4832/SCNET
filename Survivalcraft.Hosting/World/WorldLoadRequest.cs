using Game.Startup;

namespace Game.World;

public sealed record WorldLoadRequest(
    WorldInfo World,
    SessionInfo StartupSession,
    NetNode AuthorityNode,
    Func<WorldRuntimeContext, GameModRuntime> AcquireMods,
    AuthorityWorldProjectPolicies Policies,
    BlockTextureStore Textures,
    bool AllowsLocalPause
);
