namespace Game.Network.Handling;

public static class ProtocolReceivePolicy
{
    public static bool IsPreWorldMessage(IProtocolMessage message) => message is
        ServerInfoPackage or AdmissionChallengePackage or AdmissionProofPackage or AdmissionResultPackage or
        ConnectionRequestPackage or ConnectionRejectPackage or ClientPackage or BootstrapPackage
        or ConnectionPhaseAckPackage;

    public static bool CanHandleFromClient(IProtocolMessage message, Client sender)
    {
        if (message is AdmissionProofPackage or ConnectionPhaseAckPackage) { return true; }

        if (message is TerrainChunksRequestPackage)
        {
            return sender.ConnectionPhase >= ConnectionPhase.BootstrapApplied &&
                   sender.State >= ClientState.ProjectLoaded;
        }

        if (message is ClientStateChangedPackage { State: ClientState.NotConnected }) { return true; }

        return sender.ConnectionPhase == ConnectionPhase.Live && sender.State >= MinimumState(message);
    }

    public static ClientState MinimumState(IProtocolMessage message) => message switch
    {
        TerrainSnapshotMessage => ClientState.ProjectLoaded,
        ModEnvelopePackage envelope => envelope.RequiredState,
        CommandPackage => ClientState.ProjectLoaded,
        ComponentInventoryPackage => ClientState.ProjectLoaded,
        FurniturePackage => ClientState.ProjectLoaded,
        EditableBlockPackage => ClientState.ProjectLoaded,
        PickablePackage or ProjectilePackage => ClientState.ProjectLoaded,
        CharacterDataPackage => ClientState.Connected,
        MessagePackage or TeamManagePackage => ClientState.ProjectLoaded,
        ComponentBehaviorPackage => ClientState.Playing,
        ComponentHealthPackage or ComponentOnFirePackage or MovingBlockPackage or ExplosionsPackage => ClientState
            .ProjectLoaded,
        ComponentVitalStatPackage or SubsystemElectricityPackage => ClientState.Playing,
        ComponentClothingPackage or ComponentFurnacePackage or SignBlockPackage or BlockEditPackage => ClientState
            .ProjectLoaded,
        DispenserPackage or DispenserSoundPackage or ComponentSicknessPackage or CharacterListPackage
            or OnlineCharacterStatePackage
            or ComponentMountPackage or ComponentSleepPackage or SubsystemWeatherPackage
            or ComponentFluPackage => ClientState.ProjectLoaded,
        SubsystemTimePackage or SubsystemSkyPackage or ChunkStateResetPackage or SubsystemBodyPackage => ClientState
            .Playing,
        SubsystemSeasonPackage or EntityPackage or ServerInfoPackage or SubsystemTerrainPackage
            or ComponentCharacterPackage => ClientState.ProjectLoaded,
        _ => ClientState.NotConnected
    };


    public static (T Package, PackageReceiveContext Context) DecodePackage<T>(
        PackageReceiveContext context,
        ReadOnlyMemory<byte> payload
    ) where T : class, IProtocolMessage
    {
        var message = DecodePackages(context, payload)[0];
        return ((T)message.Package, message.Context);
    }

    public static List<ReceivedPackage> DecodePackages(
        PackageReceiveContext context,
        ReadOnlyMemory<byte> payload
    )
    {
        if (payload.IsEmpty) { return []; }

        try
        {
            var messages = ProtocolPacketCodec.Decode(payload);
            return messages.Select(message => new ReceivedPackage(message, context)).ToList();
        }
        catch (Exception exception) when (exception is InvalidDataException or EndOfStreamException)
        {
            Log.Error(exception.ToString());
            return [];
        }
    }
}
