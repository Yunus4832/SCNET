namespace Game.Protocol;

public enum PackageType : byte
{
    // 连接与初始同步
    ServerInfo = 0,

    ConnectionRequest = 1,

    ConnectionReject = 2,

    Client = 3,

    Bootstrap = 4,

    ConnectionPhaseAck = 5,

    InitialWorldSnapshot = 6,

    // 玩家与实体目录
    Entity = 7,

    PlayerData = 8,

    PlayerList = 9,

    OnlinePlayerState = 10,

    // 区块与方块变更
    SubsystemTerrain = 11,

    ChunkStateReset = 12,

    BlockEdit = 13,

    MovingBlockSet = 14,

    // 世界状态
    SubsystemTime = 15,

    SubsystemSky = 16,

    SubsystemWeather = 17,

    SubsystemSeason = 18,

    SubsystemElectricity = 19,

    // 动态实体状态
    Pickable = 20,

    SubsystemBody = 21,

    Projectile = 22,

    Explosion = 23,

    // 玩家组件状态
    ComponentPlayer = 24,

    ComponentBehavior = 25,

    ComponentHealth = 26,

    ComponentVitalStat = 27,

    ComponentMount = 28,

    ComponentSickness = 29,

    ComponentFlu = 30,

    ComponentOnFire = 31,

    ComponentSleep = 32,

    // 库存与方块交互
    ComponentInventory = 33,

    ComponentClothing = 34,

    ComponentFurnace = 35,

    Furniture = 36,

    SignBlock = 37,

    Dispenser = 38,

    EditableBlock = 39,

    // 消息与命令
    Message = 40,

    GroupManage = 41,

    Command = 42,

    // 模组消息
    ModEnvelope = 43,
}
