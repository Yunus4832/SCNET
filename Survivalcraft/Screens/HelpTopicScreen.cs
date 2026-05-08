using System.Xml.Linq;

namespace Game.Screens;

public class HelpTopicScreen : Screen
{
    private readonly ScrollPanelWidget _scrollPanel;

    private readonly LabelWidget _textLabel;

    private readonly LabelWidget _titleLabel;

    public HelpTopicScreen()
    {
        var node = ContentManager.Get<XElement>("Screens/HelpTopicScreen");
        LoadContents(this, node);
        _titleLabel = Children.Find<LabelWidget>("Title")!;
        _textLabel = Children.Find<LabelWidget>("Text")!;
        _scrollPanel = Children.Find<ScrollPanelWidget>("ScrollPanel")!;
    }

    public override void Enter(object[] parameters)
    {
        var helpTopic = (HelpTopic)parameters[0];
        _titleLabel.Text = helpTopic.Title;
        _textLabel.Text = helpTopic.Text;
        _scrollPanel.ScrollPosition = 0f;
    }

    public override void Update()
    {
        GameManager.UpdateProject();
        if (Input.Back || Input.Cancel || Children.Find<ButtonWidget>("TopBar.Back")!.IsClicked)
        {
            ScreensManager.SwitchScreen(ScreensManager.PreviousScreen);
        }
    }
}
