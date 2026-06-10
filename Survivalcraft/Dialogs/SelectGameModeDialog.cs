using System.Xml.Linq;

namespace Game.Dialogs;

public class SelectGameModeDialog : ListSelectionDialog
{
    public SelectGameModeDialog(
        string title,
        bool allowAdventure,
        Action<GameMode> selectionHandler
    ) : base(
        title,
        GetAllowedGameModes(allowAdventure),
        140f,
        delegate(object item)
        {
            var gameMode = (GameMode)item;
            var node = ContentManager.Get<XElement>("Widgets/SelectGameModeItem");
            var obj = (ContainerWidget)LoadWidget(null, node, null);
            obj.Children.Find<LabelWidget>("SelectGameModeItem.Name")!.Text =
                LanguageManager.Get("GameMode", gameMode.ToString());
            obj.Children.Find<LabelWidget>("SelectGameModeItem.Description")!.Text =
                StringsManager.GetString("GameMode." + gameMode + ".Description");
            return obj;
        },
        delegate(object item) { selectionHandler((GameMode)item); }
    )
    {
        ContentSize = new Vector2(750f, 420f);
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
