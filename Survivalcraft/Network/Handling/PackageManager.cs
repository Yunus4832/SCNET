using Game.Network.Transport;

namespace Game.Network.Handling;

public static class PackageManager
{
    private static readonly Dictionary<Type, Type> _registeredOwners = new();

    public static void Initialize()
    {
        _registeredOwners.Clear();
        PackageDispatcher.Clear();
        RegisterBuiltInPackages();
    }

    private static void RegisterBuiltInPackages()
    {
        RegisterPackage<ServerInfoPackage, ServerInfoPackageHandler>();
        RegisterPackage<AdmissionChallengePackage, AdmissionChallengePackageHandler>();
        RegisterPackage<AdmissionProofPackage, AdmissionProofPackageHandler>();
        RegisterPackage<AdmissionResultPackage, AdmissionResultPackageHandler>();
        RegisterPackage<ConnectionRequestPackage, ConnectionRequestPackageHandler>();
        RegisterPackage<ConnectionRejectPackage, ConnectionRejectPackageHandler>();
        RegisterPackage<ClientPackage, ClientPackageHandler>();
        RegisterPackage<PickablePackage, PickablePackageHandler>();
        RegisterPackage<SubsystemBodyPackage, SubsystemBodyPackageHandler>();
        RegisterPackage<SubsystemTerrainPackage, SubsystemTerrainPackageHandler>();
        RegisterPackage<SubsystemTimePackage, SubsystemTimePackageHandler>();
        RegisterPackage<SubsystemSkyPackage, SubsystemSkyPackageHandler>();
        RegisterPackage<SubsystemWeatherPackage, SubsystemWeatherPackageHandler>();
        RegisterPackage<SubsystemElectricityPackage, SubsystemElectricityPackageHandler>();
        RegisterPackage<ComponentInventoryPackage, ComponentInventoryPackageHandler>();
        RegisterPackage<ComponentVitalStatPackage, ComponentVitalStatPackageHandler>();
        RegisterPackage<ComponentClothingPackage, ComponentClothingPackageHandler>();
        RegisterPackage<ComponentBehaviorPackage, ComponentBehaviorPackageHandler>();
        RegisterPackage<ComponentHealthPackage, ComponentHealthPackageHandler>();
        RegisterPackage<ComponentMountPackage, ComponentMountPackageHandler>();
        RegisterPackage<ComponentSicknessPackage, ComponentSicknessPackageHandler>();
        RegisterPackage<ComponentFluPackage, ComponentFluPackageHandler>();
        RegisterPackage<ComponentOnFirePackage, ComponentOnFirePackageHandler>();
        RegisterPackage<ComponentSleepPackage, ComponentSleepPackageHandler>();
        RegisterPackage<ComponentFurnacePackage, ComponentFurnacePackageHandler>();
        RegisterPackage<CharacterDataPackage, CharacterDataPackageHandler>();
        RegisterPackage<MessagePackage, MessagePackageHandler>();
        RegisterPackage<EntityPackage, EntityPackageHandler>();
        RegisterPackage<ProjectilePackage, ProjectilePackageHandler>();
        RegisterPackage<FurniturePackage, FurniturePackageHandler>();
        RegisterPackage<ExplosionsPackage, ExplosionsPackageHandler>();
        RegisterPackage<MovingBlockPackage, MovingBlockPackageHandler>();
        RegisterPackage<BlockEditPackage, BlockEditPackageHandler>();
        RegisterPackage<SignBlockPackage, SignBlockPackageHandler>();
        RegisterPackage<DispenserPackage, DispenserPackageHandler>();
        RegisterPackage<EditableBlockPackage, EditableBlockPackageHandler>();
        RegisterPackage<TeamManagePackage, TeamManagePackageHandler>();
        RegisterPackage<SubsystemSeasonPackage, SubsystemSeasonPackageHandler>();
        RegisterPackage<CommandPackage, CommandPackageHandler>();
        RegisterPackage<CharacterListPackage, CharacterListPackageHandler>();
        RegisterPackage<OnlineCharacterStatePackage, OnlineCharacterStatePackageHandler>();
        RegisterPackage<BootstrapPackage, BootstrapPackageHandler>();
        RegisterPackage<ConnectionPhaseAckPackage, ConnectionPhaseAckPackageHandler>();
        RegisterPackage<InitialWorldSnapshotPackage, InitialWorldSnapshotPackageHandler>();
        RegisterPackage<ChunkStateResetPackage, ChunkStateResetPackageHandler>();
        RegisterPackage<ModEnvelopePackage, ModEnvelopePackageHandler>();
        RegisterPackage<ComponentCharacterPackage, ComponentCharacterPackageHandler>();
    }

    private static void RegisterPackage<TPackage, THandler>()
        where TPackage : IProtocolMessage
        where THandler : IPackageHandler<TPackage>, new()
    {
        RegisterPackage(new THandler());
    }

    public static void RegisterPackage<TPackage>(IPackageHandler<TPackage> handler)
        where TPackage : IProtocolMessage
    {
        var types = ProtocolMessageCodec.MessageTypes.Where(typeof(TPackage).IsAssignableFrom).ToArray();
        if (types.Length == 0 || handler.PackageType != typeof(TPackage))
        {
            throw new ArgumentException("The handler must match a known protocol message or family.");
        }

        foreach (var type in types)
        {
            if (!_registeredOwners.TryAdd(type, typeof(TPackage)))
            {
                throw new InvalidOperationException($"Duplicate message handler: {type.Name}.");
            }

            PackageDispatcher.Register(type, handler);
        }
    }

    public static void UnregisterPackage(Type messageType)
    {
        var owner = _registeredOwners.GetValueOrDefault(messageType, messageType);
        foreach (var type in _registeredOwners.Where(pair => pair.Value == owner).Select(pair => pair.Key).ToArray())
        {
            _registeredOwners.Remove(type);
        }

        PackageDispatcher.Unregister(messageType);
    }

    public static ClientState MinimumState(IProtocolMessage message) => message switch
    {
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
        DispenserPackage or ComponentSicknessPackage or CharacterListPackage or OnlineCharacterStatePackage
            or ComponentMountPackage or ComponentSleepPackage or SubsystemWeatherPackage
            or ComponentFluPackage => ClientState.ProjectLoaded,
        SubsystemTimePackage or SubsystemSkyPackage or ChunkStateResetPackage or SubsystemBodyPackage => ClientState
            .Playing,
        SubsystemSeasonPackage or EntityPackage or ServerInfoPackage or SubsystemTerrainPackage
            or ComponentCharacterPackage => ClientState.ProjectLoaded,
        _ => ClientState.NotConnected
    };


    public static (T Package, PackageReceiveContext Context) DecodePackage<T>(
        NetNode? netNode,
        ReadOnlyMemory<byte> payload,
        Client? sender = null
    ) where T : class, IProtocolMessage
    {
        var message = DecodePackages(netNode, payload, sender)[0];
        return ((T)message.Package, message.Context);
    }

    public static List<ReceivedPackage> DecodePackages(
        NetNode? netNode,
        ReadOnlyMemory<byte> payload,
        Client? sender = null
    )
    {
        var context = new PackageReceiveContext(netNode, netNode?.IsServer ?? false, sender);
        if (payload.IsEmpty) { return []; }

        try
        {
            var messages = ProtocolPacketCodec.Decode(payload);
            if (messages.Any(message => !_registeredOwners.ContainsKey(message.GetType())))
            {
                throw new InvalidDataException("A protocol message has no registered handler.");
            }

            return messages.Select(message => new ReceivedPackage(message, context)).ToList();
        }
        catch (Exception exception) when (exception is InvalidDataException or EndOfStreamException)
        {
            Log.Error(exception.ToString());
            return [];
        }
    }
}
