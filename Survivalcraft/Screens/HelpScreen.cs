using System.Text.Json.Nodes;
using System.Xml.Linq;

namespace Game.Screens;

public class HelpScreen : Screen
{
    private readonly ButtonWidget _bestiaryButton;

    private Screen? _previousScreen;

    private readonly ButtonWidget _recipaediaButton;

    private readonly Dictionary<string, HelpTopic> _topics = new();

    private readonly ListPanelWidget _topicsList;

    public HelpScreen()
    {
        var node = ContentManager.Get<XElement>("Screens/HelpScreen");
        LoadContents(this, node);
        _topicsList = Children.Find<ListPanelWidget>("TopicsList")!;
        _recipaediaButton = Children.Find<ButtonWidget>("RecipaediaButton")!;
        _bestiaryButton = Children.Find<ButtonWidget>("BestiaryButton")!;
        _topicsList.ItemWidgetFactory = delegate(object item)
        {
            var helpTopic3 = (HelpTopic)item;
            var node2 = ContentManager.Get<XElement>("Widgets/HelpTopicItem");
            var obj = (ContainerWidget)LoadWidget(this, node2, null);
            obj.Children.Find<LabelWidget>("HelpTopicItem.Title")!.Text = helpTopic3.Title;
            return obj;
        };
        _topicsList.ItemClicked += delegate(object item)
        {
            if (item is HelpTopic helpTopic2)
            {
                ShowTopic(helpTopic2);
            }
        };
        if (LanguageManager.KeyWords["Help"] is not JsonObject kvs)
        {
            return;
        }

        foreach (var (_, item) in kvs)
        {
            if (item is not JsonObject item3)
            {
                continue;
            }

            if (item3.TryGetPropertyValue("DisabledPlatforms", out var disabledPlatformsNode))
            {
                var disabledPlatforms = disabledPlatformsNode?.ToString() ?? string.Empty;
                if (disabledPlatforms.Split([","], StringSplitOptions.None)
                        .FirstOrDefault(s => string.Equals(s.Trim(), PlatformManager.Platform.ToString(),
                            StringComparison.CurrentCultureIgnoreCase)) == null)
                {
                    continue;
                }
            }

            var title = item3.ContainsKey("Title") ? item3["Title"]?.ToString() ?? string.Empty : string.Empty;
            var name = item3.ContainsKey("Name") ? item3["Name"]?.ToString() ?? string.Empty : string.Empty;
            var value = item3.ContainsKey("value") ? item3["value"]?.ToString() ?? string.Empty : string.Empty;
            var text = string.Empty;
            var array = value.Split(["\n"], StringSplitOptions.None);
            text = array.Aggregate(text, (current, text2) => current + text2.Trim() + " ");
            text = text.Replace("\r", "");
            text = text.Replace("â€™", "'");
            text = text.Replace("\\n", "\n");
            var helpTopic = new HelpTopic
            {
                Name = name,
                Title = title,
                Text = text
            };
            if (!string.IsNullOrEmpty(helpTopic.Name))
            {
                _topics.Add(helpTopic.Name, helpTopic);
            }

            _topicsList.AddItem(helpTopic);
        }
    }

    public override void Enter(object[] parameters)
    {
        if (ScreensManager.PreviousScreen != ScreensManager.FindScreen<Screen>("HelpTopic") &&
            ScreensManager.PreviousScreen != ScreensManager.FindScreen<Screen>("Recipaedia") &&
            ScreensManager.PreviousScreen != ScreensManager.FindScreen<Screen>("Bestiary"))
        {
            _previousScreen = ScreensManager.PreviousScreen;
        }
    }

    public override void Leave()
    {
        _topicsList.SelectedItem = null;
    }

    public override void Update()
    {
        GameManager.UpdateProject();
        if (_recipaediaButton.IsClicked)
        {
            ScreensManager.SwitchScreen("Recipaedia");
        }

        if (_bestiaryButton.IsClicked)
        {
            ScreensManager.SwitchScreen("Bestiary");
        }

        if (Input.Back || Input.Cancel || Children.Find<ButtonWidget>("TopBar.Back")!.IsClicked)
        {
            ScreensManager.SwitchScreen(_previousScreen);
        }
    }

    public HelpTopic GetTopic(string name)
    {
        return _topics[name];
    }

    private void ShowTopic(HelpTopic helpTopic)
    {
        if (helpTopic.Name == "Keyboard")
        {
            DialogsManager.ShowDialog(null, new KeyboardHelpDialog());
        }
        else if (helpTopic.Name == "Gamepad")
        {
            DialogsManager.ShowDialog(null, new GamepadHelpDialog());
        }
        else
        {
            ScreensManager.SwitchScreen("HelpTopic", helpTopic);
        }
    }
}
