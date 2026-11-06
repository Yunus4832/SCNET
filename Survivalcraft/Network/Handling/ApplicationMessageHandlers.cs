using EntitySystem.Core;

using Game.Content;
using Game.Network.Handling.Presentation;

namespace Game.Network.Handling;

public static class ApplicationMessageHandlers
{
    public static PackageHandlerCatalog CreatePreWorld(NodeRole role, bool presentation)
    {
        if (!Enum.IsDefined(role)) { throw new ArgumentOutOfRangeException(nameof(role)); }

        var builder = new PackageHandlerCatalogBuilder();
        HostRuleHandlers.RegisterPreWorld(builder);
        if (role != NodeRole.Authority && presentation)
        {
            builder.RegisterFamily(() => new BootstrapPackageHandler());
        }

        return builder.Build();
    }

    public static void InstallWorldHandlers(NetNode node, GameModRuntime mods)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(mods);
        var repositories = ContentRepositoryManager.Current;
        var skins = CharacterSkinsManager.CreateStore();

        ServerInfoResponsePackage CaptureServerInfo(Project project) =>
            ServerInfoPackageBuilder.Capture(project, mods.CreateServerRequiredProfile(), repositories.Snapshot());

        var catalog = node.Runtime.IsAuthority
            ? HostRuleHandlers.CreateAuthority(mods.Commands, mods.Network, mods.ModDataHash, skins, CaptureServerInfo)
            : CreateClient(mods, skins, CaptureServerInfo, node.Runtime.HasPresentation);
        WorldNetworkBinding.For(node.Runtime).InstallHandlers(catalog);
    }

    private static PackageHandlerCatalog CreateClient(
        GameModRuntime mods,
        CharacterSkinStore skins,
        Func<Project, ServerInfoResponsePackage> captureServerInfo,
        bool presentation
    )
    {
        var builder = new PackageHandlerCatalogBuilder();
        HostRuleHandlers.RegisterCommon(builder, captureServerInfo, skins);
        builder.RegisterFamily(() => new ModEnvelopePackageHandler(mods.Network));
        builder.RegisterFamily(() => new ClientComponentCharacterPackageHandler());
        builder.RegisterFamily(() => new ClientComponentHealthPackageHandler());
        builder.RegisterFamily(() => new ClientComponentSleepPackageHandler());
        builder.RegisterFamily(() => new ClientComponentBehaviorPackageHandler());
        builder.RegisterFamily(() => new ClientComponentVitalStatPackageHandler());
        builder.RegisterFamily(() => new ClientComponentOnFirePackageHandler());
        builder.RegisterFamily(() => new ClientChunkStateResetPackageHandler());
        builder.RegisterFamily(() => new ClientBlockEditPackageHandler());
        builder.RegisterFamily(() => new ClientTeamManagePackageHandler());
        builder.RegisterFamily(() => new ClientCommandPackageHandler(mods.Commands));
        builder.RegisterFamily(() =>
            {
                var handler = new InitialWorldSnapshotPackageHandler();
                if (presentation)
                {
                    handler.Applied += runtime => ScreensManager.FindScreen<GameLoadingScreen>("GameLoading", true)!
                        .WorldSnapshotApplied(runtime);
                }

                return handler;
            }
        );
        if (presentation)
        {
            builder.RegisterFamily(() => new BootstrapPackageHandler());
            builder.RegisterFamily(() => new ExplosionsPackageHandler());
            builder.RegisterFamily(() => new DispenserSoundPackageHandler());
            builder.RegisterFamily(() => new SoundGeneratorNotePackageHandler());
            builder.RegisterFamily(() =>
                new NewYearCelebrationAnnouncementPackageHandler()
            );
        }

        return builder.Build();
    }
}
