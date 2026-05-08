using System.Xml.Linq;

namespace Game.Dialogs;

public class BulletinDialog : Dialog
{
    private readonly Action _action;

    private readonly Action<LabelWidget, LabelWidget> _action2;

    private readonly Action<LabelWidget, LabelWidget> _action3;

    private readonly LabelWidget _buttonLabel;

    private readonly LabelWidget _contentLabel;

    public readonly ButtonWidget EditButton;

    private readonly ButtonWidget _okButton;

    private readonly ScrollPanelWidget _scrollPanel;

    private readonly LabelWidget _timeLabel;

    private readonly LabelWidget _titleLabel;

    public readonly ButtonWidget UpdateButton;

    public BulletinDialog(
        string title,
        string content,
        string time,
        Action action,
        Action<LabelWidget, LabelWidget> action2,
        Action<LabelWidget, LabelWidget> action3
    )
    {
        var node = ContentManager.Get<XElement>("Dialogs/BulletinDialog");
        LoadContents(this, node);
        _okButton = Children.Find<ButtonWidget>("OkButton")!;
        EditButton = Children.Find<ButtonWidget>("EditButton")!;
        UpdateButton = Children.Find<ButtonWidget>("UpdateButton")!;
        _titleLabel = Children.Find<LabelWidget>("Title")!;
        _contentLabel = Children.Find<LabelWidget>("Content")!;
        _timeLabel = Children.Find<LabelWidget>("Time")!;
        _buttonLabel = Children.Find<LabelWidget>("ButtonLabel")!;
        _scrollPanel = Children.Find<ScrollPanelWidget>("ScrollPanel")!;
        _buttonLabel.Text = LanguageControl.Ok;
        _okButton.IsVisible = false;
        _titleLabel.Text = title;
        _contentLabel.Text = content;
        _timeLabel.Text = time;
        _action = action;
        _action2 = action2;
        _action3 = action3;
        EditButton.IsVisible = false;
        UpdateButton.IsVisible = false;
    }

    public override void Update()
    {
        var length = MathUtils.Max(_scrollPanel.ScrollAreaLength - _scrollPanel.ActualSize.Y, 0f);
        if (_scrollPanel.ScrollPosition >= length * 0.8f && _scrollPanel.ScrollAreaLength != 0)
        {
            _okButton.IsVisible = true;
        }

        if (_okButton.IsClicked)
        {
            _action.Invoke();
            DialogsManager.HideDialog(this);
        }

        if (EditButton.IsClicked)
        {
            _action2?.Invoke(_titleLabel, _contentLabel);
        }

        if (!UpdateButton.IsClicked)
        {
            return;
        }

        _action3?.Invoke(_titleLabel, _contentLabel);
        DialogsManager.HideDialog(this);
    }
}
