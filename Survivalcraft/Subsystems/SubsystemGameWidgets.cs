using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Messaging;
using Game.Network;

namespace Game.Subsystems;

public class SubsystemGameWidgets : Subsystem, IUpdateable
{
    private readonly List<GameWidget> _gameWidgets = [];

    private GameMessageService? _messages;

    private SubsystemCharacters _subsystemCharacters = null!;

    public CharacterData? MainCharacterData;

    public GameWidget? LocalCharacterWidget;

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
        _subsystemCharacters = Project.FindSubsystem<SubsystemCharacters>(true)!;
        SubsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true)!;
        if (RunMode.Value is RunModeType.Gui)
        {
            _subsystemCharacters.CharacterAdded += AddGameWidgetForPlayer;
            _subsystemCharacters.CharacterRemoved += delegate(CharacterData characterData)
            {
                RemoveGameWidget(characterData.GameWidget);
            };
        }

        if (CommonLib.LocalCharacter != null)
        {
            MainCharacterData = CommonLib.LocalCharacter.CharacterData;
        }

        GamesWidget = valuesDictionary.GetValue<GamesWidget>("GamesWidget");

        if (RunMode.Value is RunModeType.HeadlessServer)
        {
            return;
        }

        foreach (var playersDatum in _subsystemCharacters.Characters)
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

    private void AddGameWidgetForPlayer(CharacterData characterData)
    {
        var gameWidget = new GameWidget(characterData, characterData.CharacterIndex);
        _gameWidgets.Add(gameWidget);
        GamesWidget.Children.Add(gameWidget);
        characterData.GameWidget = gameWidget;
        if (characterData.IsLocalCharacter)
        {
            MainCharacterData = characterData;
            LocalCharacterWidget = gameWidget;
        }
    }

    private void RemoveGameWidget(GameWidget gameWidget)
    {
        _gameWidgets.Remove(gameWidget);
        GamesWidget.Children.Remove(gameWidget);
        if (ReferenceEquals(LocalCharacterWidget, gameWidget))
        {
            MainCharacterData = null;
            LocalCharacterWidget = null;
        }
    }
}
