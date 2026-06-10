using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Network;
using Game.Network.Packages;

namespace Game.Subsystems;

public class SubsystemGameWidgets : Subsystem, IUpdateable
{
    public const int MaxMassageCount = 200;

    private readonly Queue<string> _playerMessages = new();

    private readonly List<GameWidget> _gameWidgets = [];

    private SubsystemPlayers _subsystemPlayers = null!;

    public PlayerData? MainPlayerData;

    public GameWidget? MainPlayerWidget;

    public GamesWidget GamesWidget { get; set; } = null!;

    public ReadOnlyList<GameWidget> GameWidgets => new(_gameWidgets);

    public SubsystemTerrain SubsystemTerrain { get; set; } = null!;

    public List<string> PlayerMessages => [.._playerMessages];

    public UpdateOrder UpdateOrder => UpdateOrder.Views;

    public void Update(float dt)
    {
        foreach (var gameWidget in GameWidgets)
        {
            gameWidget.ActiveCamera.Update(Time.FrameDuration);
        }
    }

    public event Action<string>? OnMessageRecieved;

    public void AddMessage(string msg, string playerName = "", byte type = 0, List<byte>? toClients = null)
    {
        toClients ??= [];
        var arr = Project.FindSubsystem<SubsystemGameInfo>(true)!.WorldSettings.KeywordBlocking
            .Split([';'], StringSplitOptions.None);
        foreach (var block in arr)
        {
            if (!string.IsNullOrEmpty(block))
            {
                msg = msg.Replace(block, "*");
            }
        }

        CommonLib.Net.QueuePackage(new MessagePackage(playerName, msg, type, toClients));
        //本地加入自己的
        if (toClients.Count != 0 && CommonLib.MainPlayer != null)
        {
            toClients.Add(CommonLib.MainPlayer.PlayerData.ClientId);
        }

        AddNetMessage(msg, playerName, type, toClients);
    }

    public void AddNetMessage(string msg, string playerName = "", byte type = 0, List<byte>? toClients = null,
        bool external = false)
    {
        toClients ??= [];
        var typeStr = "";
        switch (type)
        {
            case 1: typeStr = "<c=blue>[队]</c>"; break;
            case 2: typeStr = "<c=Violet>[私]</c>"; break;
        }

        var message =
            $"{typeStr}{(string.IsNullOrEmpty(playerName) ? "<c=red>[系统]</c>" : "[" + playerName + "]")}{msg}";
        if (toClients.Count > 0 && CommonLib.Net.Self != null && toClients.Contains(CommonLib.Net.Self.ID))
        {
            Insert(message, type, external);
        }
        else if (type == 0)
        {
            Insert(message, 0, external);
        }
    }

    private void Insert(string msg, byte type = 0, bool external = false)
    {
        //只保留全服消息
        if (type == 0)
        {
            while (_playerMessages.Count >= MaxMassageCount)
            {
                _playerMessages.Dequeue();
            }

            _playerMessages.Enqueue(msg);
        }

        if (!external)
        {
            Log.Information(msg);
        }

        OnMessageRecieved?.Invoke(msg);
    }

    public float CalculateSquaredDistanceFromNearestView(Vector3 p)
    {
        var num = float.MaxValue;
        foreach (var gameWidget in _gameWidgets)
        {
            var num2 = Vector3.DistanceSquared(p, gameWidget.ActiveCamera.ViewPosition);
            if (num2 < num)
            {
                num = num2;
            }
        }

        return num;
    }

    public float CalculateDistanceFromNearestView(Vector3 p)
    {
        return MathUtils.Sqrt(CalculateSquaredDistanceFromNearestView(p));
    }

    public override void Load(ValuesDictionary valuesDictionary)
    {
        _subsystemPlayers = Project.FindSubsystem<SubsystemPlayers>(true)!;
        SubsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true)!;
        if (RunMode.Value is RunModeType.Gui)
        {
            _subsystemPlayers.PlayerAdded += AddGameWidgetForPlayer;
            _subsystemPlayers.PlayerRemoved += delegate(PlayerData playerData)
            {
                RemoveGameWidget(playerData.GameWidget);
            };
        }

        if (CommonLib.MainPlayer != null)
        {
            MainPlayerData = CommonLib.MainPlayer.PlayerData;
        }

        GamesWidget = valuesDictionary.GetValue<GamesWidget>("GamesWidget");

        if (RunMode.Value is RunModeType.HeadlessServer)
        {
            return;
        }

        foreach (var playersDatum in _subsystemPlayers.PlayersData)
        {
            Log.Debug($"Widget load: {playersDatum.Name}");
            AddGameWidgetForPlayer(playersDatum);
        }
    }

    public override void Save(ValuesDictionary valuesDictionary)
    {
        /*
        if (CommonLib.WorkType == WorkType.Server)
        {
            var listDict = new ValuesDictionary();
            var msgs = PlayerMessages;
            for (int i = 0; i < msgs.Count; i++)
            {
                listDict.Add(i.ToString(), msgs[i]);
            }
            valuesDictionary.Add("Messages", listDict);
        }
        */
    }

    public override void Dispose()
    {
        var array = GameWidgets.ToArray();
        foreach (var gameWidget in array)
        {
            RemoveGameWidget(gameWidget);
            gameWidget.Dispose();
        }
    }

    private void AddGameWidgetForPlayer(PlayerData playerData)
    {
        var gameWidget = new GameWidget(playerData, playerData.PlayerIndex);
        _gameWidgets.Add(gameWidget);
        GamesWidget.Children.Add(gameWidget);
        playerData.GameWidget = gameWidget;
    }

    private void RemoveGameWidget(GameWidget gameWidget)
    {
        _gameWidgets.Remove(gameWidget);
        GamesWidget.Children.Remove(gameWidget);
    }
}
