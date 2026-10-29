namespace Game.Dialogs;

public class SelectGameModeDialog : ListSelectionDialog
{
    public SelectGameModeDialog(
        string title,
        bool allowAdventure,
        Action<GameMode> selectionHandler,
        GameMode? currentGameMode = null
    ) : base(
        title,
        GetAllowedGameModes(allowAdventure),
        120f,
        item => CreateTextItem(
            LanguageManager.Get("GameMode", ((GameMode)item).ToString()),
            StringsManager.GetString("GameMode", (GameMode)item, "Description"),
            true
        ),
        item => selectionHandler((GameMode)item),
        currentGameMode
    )
    {
        ContentSize = new Vector2(680f, 420f);
    }

    private static IEnumerable<GameMode> GetAllowedGameModes(bool allowAdventure)
    {
        if (!allowAdventure)
        {
            return
            [
                GameMode.Creative,
                GameMode.Survival,
                GameMode.Challenging,
                GameMode.Harmless,
                GameMode.Cruel
            ];
        }

        return
        [
            GameMode.Creative,
            GameMode.Survival,
            GameMode.Challenging,
            GameMode.Harmless,
            GameMode.Cruel,
            GameMode.Adventure
        ];
    }
}
