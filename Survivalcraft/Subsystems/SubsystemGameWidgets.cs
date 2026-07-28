using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Messaging;
using Game.Network;
namespace Game.Subsystems;

public class SubsystemGameWidgets : Subsystem, IUpdateable
{
    private readonly List<GameWidget> _gameWidgets = [];

    private GameMessageService? _messages;

    private SubsystemPlayers _subsystemPlayers = null!;

    public PlayerData? MainPlayerData;

    public GameWidget? MainPlayerWidget;

    public GamesWidget GamesWidget { get; set; } = null!;

    public ReadOnlyList<GameWidget> GameWidgets => new(_gameWidgets);

    public SubsystemTerrain SubsystemTerrain { get; set; } = null!;

    public GameMessageService Messages => _messages ??= new GameMessageService(Project);

    public UpdateOrder UpdateOrder => UpdateOrder.Views;

    public void Update(float dt)
    {
        foreach (var gameWidget in GameWidgets)
        {
            gameWidget.ActiveCamera.Update(Time.FrameDuration);
        }
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
            _subsystemPlayers.PlayerRemoved += delegate (PlayerData playerData)
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
