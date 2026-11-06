using EntitySystem.Core;

namespace Game.Network.Handling;

public static class HostRuleHandlers
{
    public static void RegisterPreWorld(PackageHandlerCatalogBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.RegisterFamily(() => new AdmissionChallengePackageHandler());
        builder.RegisterFamily(() => new AdmissionProofPackageHandler());
        builder.RegisterFamily(() => new AdmissionResultPackageHandler());
        builder.RegisterFamily(() => new ConnectionRejectPackageHandler());
        builder.RegisterFamily(() => new ClientPackageHandler());
        builder.RegisterFamily(() => new ConnectionPhaseAckPackageHandler());
    }

    public static PackageHandlerCatalog CreateAuthority(
        CommandRegistry commands,
        ModNetworkHooks network,
        string modDataHash,
        CharacterSkinStore skins,
        Func<Project, ServerInfoResponsePackage> captureServerInfo
    )
    {
        var builder = new PackageHandlerCatalogBuilder();
        RegisterCommon(builder, captureServerInfo, skins);
        builder.RegisterFamily(() => new ConnectionRequestPackageHandler(modDataHash));
        builder.RegisterFamily(() => new ModEnvelopePackageHandler(network));
        builder.RegisterFamily(() => new ComponentCharacterPackageHandler());
        builder.RegisterFamily(() => new ComponentHealthPackageHandler());
        builder.RegisterFamily(() => new ComponentSleepPackageHandler());
        builder.RegisterFamily(() => new ComponentBehaviorPackageHandler());
        builder.RegisterFamily(() => new ComponentVitalStatPackageHandler());
        builder.RegisterFamily(() => new ComponentOnFirePackageHandler());
        builder.RegisterFamily(() => new ChunkStateResetPackageHandler());
        builder.RegisterFamily(() => new BlockEditPackageHandler());
        builder.RegisterFamily(() => new TeamManagePackageHandler());
        builder.RegisterFamily(() => new CommandPackageHandler(commands));
        return builder.Build();
    }

    public static void RegisterCommon(
        PackageHandlerCatalogBuilder builder,
        Func<Project, ServerInfoResponsePackage> captureServerInfo,
        CharacterSkinStore skins
    )
    {
        ArgumentNullException.ThrowIfNull(builder);
        RegisterPreWorld(builder);
        builder.RegisterFamily(() => new ServerInfoPackageHandler(captureServerInfo));
        builder.RegisterFamily(() => new PickablePackageHandler());
        builder.RegisterFamily(() => new SubsystemBodyPackageHandler());
        builder.RegisterFamily(() => new SubsystemTerrainPackageHandler());
        builder.RegisterFamily(() => new SubsystemTimePackageHandler());
        builder.RegisterFamily(() => new SubsystemSkyPackageHandler());
        builder.RegisterFamily(() => new SubsystemWeatherPackageHandler());
        builder.RegisterFamily(() => new SubsystemElectricityPackageHandler());
        builder.RegisterFamily(() => new ComponentInventoryPackageHandler());
        builder.RegisterFamily(() => new ComponentClothingPackageHandler(skins));
        builder.RegisterFamily(() => new ComponentMountPackageHandler());
        builder.RegisterFamily(() => new ComponentSicknessPackageHandler());
        builder.RegisterFamily(() => new ComponentFluPackageHandler());
        builder.RegisterFamily(() => new ComponentFurnacePackageHandler());
        builder.RegisterFamily(() => new CharacterDataPackageHandler());
        builder.RegisterFamily(() => new MessagePackageHandler());
        builder.RegisterFamily(() => new EntityPackageHandler());
        builder.RegisterFamily(() => new ProjectilePackageHandler());
        builder.RegisterFamily(() => new FurniturePackageHandler());
        builder.RegisterFamily(() => new MovingBlockPackageHandler());
        builder.RegisterFamily(() => new SignBlockPackageHandler());
        builder.RegisterFamily(() => new DispenserPackageHandler());
        builder.RegisterFamily(() => new EditableBlockPackageHandler());
        builder.RegisterFamily(() => new SubsystemSeasonPackageHandler());
        builder.RegisterFamily(() => new CharacterListPackageHandler());
        builder.RegisterFamily(() => new OnlineCharacterStatePackageHandler());
        builder.RegisterLogical(() => new TerrainSnapshotMessageHandler());
    }
}
