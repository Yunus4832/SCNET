using Game.Localization;
using Game.Startup;

namespace Game.Commands.BuiltIn;

public sealed record ProcessCommandServices(
    Action<SessionInfo?> Restart,
    Action Exit,
    Action<string> SwitchInstance,
    Func<LanguageCatalog> GetLanguage,
    Action<string> SetLanguage,
    Action SaveSettings
);
