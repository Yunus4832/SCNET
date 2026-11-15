using EntitySystem.Core;

namespace Game.World.Presentation;

public readonly record struct ClientWorldSaveRequest(
    Project ClientProject,
    bool ShowErrorDialog
);
