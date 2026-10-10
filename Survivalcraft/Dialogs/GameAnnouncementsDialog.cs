using System.Xml.Linq;

using Engine.Graphics;

namespace Game.Dialogs;

public sealed class GameAnnouncementsDialog : Dialog
{
    private readonly LabelWidget _bodyLabel;
    private readonly ButtonWidget _closeButton;
    private readonly LabelWidget _dateLabel;
    private readonly ListPanelWidget _listPanel;
    private readonly LabelWidget _titleLabel;

    private GameAnnouncement? _selectedAnnouncement;

    public GameAnnouncementsDialog(string title, IReadOnlyList<GameAnnouncement> announcements)
    {
        LoadContents(this, ContentManager.Get<XElement>("Dialogs/GameAnnouncementsDialog"));
        Children.Find<LabelWidget>("GameAnnouncementsDialog.Header")!.Text = title;
        _listPanel = Children.Find<ListPanelWidget>("GameAnnouncementsDialog.List")!;
        _titleLabel = Children.Find<LabelWidget>("GameAnnouncementsDialog.Title")!;
        _dateLabel = Children.Find<LabelWidget>("GameAnnouncementsDialog.Date")!;
        _bodyLabel = Children.Find<LabelWidget>("GameAnnouncementsDialog.Body")!;
        _closeButton = Children.Find<ButtonWidget>("GameAnnouncementsDialog.Close")!;
        _closeButton.Text = LanguageManager.Get("Usual", "ok");
        _listPanel.ItemSize = 48f;
        _listPanel.ItemWidgetFactory = item => new LabelWidget
        {
            Text = ((GameAnnouncement)item).Title,
            Margin = new Vector2(12f, 0f),
            FontScale = 0.8f,
            VerticalAlignment = WidgetAlignment.Center,
            TextAnchor = TextAnchor.VerticalCenter
        };
        foreach (var announcement in announcements)
        {
            _listPanel.AddItem(announcement);
        }

        if (announcements.Count > 0)
        {
            _listPanel.SelectedItem = announcements[0];
            Select(announcements[0]);
        }
    }

    public override void Update()
    {
        if (_listPanel.SelectedItem is GameAnnouncement announcement &&
            !ReferenceEquals(announcement, _selectedAnnouncement))
        {
            Select(announcement);
        }

        if (Input.Cancel || _closeButton.IsClicked)
        {
            DialogsManager.HideDialog(this);
        }
    }

    private void Select(GameAnnouncement announcement)
    {
        _selectedAnnouncement = announcement;
        _titleLabel.Text = announcement.Title;
        _dateLabel.Text = announcement.PublishedAt?.ToLocalTime().ToString("d") ?? string.Empty;
        _bodyLabel.Text = announcement.Body;
    }
}
