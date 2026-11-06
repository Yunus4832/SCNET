using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Subsystems;

public class SubsystemGameWidgets : Subsystem, IUpdateable
{
    public UpdateExecutionSide UpdateSide => UpdateExecutionSide.Presentation;

    private readonly List<GameWidget> _gameWidgets = [];


    public CharacterData? MainCharacterData;

    public GameWidget? LocalCharacterWidget;

    public GamesWidget GamesWidget { get; set; } = null!;

    public ReadOnlyList<GameWidget> GameWidgets => new(_gameWidgets);

    public SubsystemTerrain SubsystemTerrain { get; set; } = null!;


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
        SubsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true)!;
        GamesWidget = valuesDictionary.GetValue<GamesWidget>("GamesWidget");
    }

    public override void Dispose()
    {
        var array = GameWidgets.ToArray();
        foreach (var gameWidget in array)
        {
            RemoveGameWidget(gameWidget);
        }
    }

    internal void SelectLocalCharacter(CharacterData character)
    {
        MainCharacterData = character;
        LocalCharacterWidget = Project.FindSubsystem<SubsystemCharacterPresentation>(true)!.Get(character).GameWidget;
    }

    internal void AddGameWidget(CharacterPresentation presentation)
    {
        var characterData = presentation.CharacterData;
        var gameWidget = new GameWidget(presentation, characterData.CharacterIndex);
        _gameWidgets.Add(gameWidget);
        GamesWidget.Children.Add(gameWidget);
        presentation.Attach(gameWidget);
        if (characterData.IsLocalCharacter)
        {
            MainCharacterData = characterData;
            LocalCharacterWidget = gameWidget;
        }
    }

    internal void RemoveGameWidget(GameWidget gameWidget)
    {
        if (!_gameWidgets.Remove(gameWidget)) { return; }

        GamesWidget.Children.Remove(gameWidget);
        gameWidget.Dispose();
        if (ReferenceEquals(LocalCharacterWidget, gameWidget))
        {
            MainCharacterData = null;
            LocalCharacterWidget = null;
        }
    }
}
