using System.Xml.Linq;

namespace Game.Widgets;

public sealed class SignViewerWidget : CanvasWidget
{
    private readonly Action _close;
    private readonly ButtonWidget _closeButton;
    private readonly LabelWidget _emptyLabel;
    private readonly ButtonWidget _openLinkButton;
    private readonly Uri? _url;

    public SignViewerWidget(SignData? signData, Action close)
    {
        LoadContents(this, ContentManager.Get<XElement>("Widgets/SignViewerWidget"));
        _close = close;
        _closeButton = Children.Find<ButtonWidget>("SignViewer.Close")!;
        _openLinkButton = Children.Find<ButtonWidget>("SignViewer.OpenLink")!;
        _emptyLabel = Children.Find<LabelWidget>("SignViewer.Empty")!;

        var hasText = false;
        for (var i = 0; i < 4; i++)
        {
            var label = Children.Find<LabelWidget>($"SignViewer.Line{i + 1}")!;
            var text = signData?.Lines.ElementAtOrDefault(i) ?? string.Empty;
            label.Text = text;
            label.Color = signData?.Colors.ElementAtOrDefault(i) ?? Color.Black;
            hasText |= !string.IsNullOrEmpty(text);
        }

        _emptyLabel.IsVisible = !hasText;
        _url = TryGetWebUrl(signData?.Url);
        _openLinkButton.IsVisible = _url is not null;
    }

    public override void Update()
    {
        if (_openLinkButton.IsClicked && _url is not null)
        {
            WebBrowserManager.LaunchBrowser(_url.AbsoluteUri);
        }

        if (_closeButton.IsClicked)
        {
            _close();
        }
    }

    private static Uri? TryGetWebUrl(string? value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https"))
        {
            return null;
        }

        return uri;
    }
}
