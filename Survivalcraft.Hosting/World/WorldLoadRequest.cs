using EntitySystem.Core;

using Game.Startup;

namespace Game.World;

public sealed record WorldLoadRequest(
    WorldInfo World,
    SessionInfo StartupSession,
    NetNode AuthorityNode,
    GameModRuntime Mods,
    GameDatabase Database,
    AuthorityWorldProjectPolicies Policies,
    BlockTextureStore Textures,
    bool AllowsLocalPause
);
