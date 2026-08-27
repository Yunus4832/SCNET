using System.Xml.Linq;

using Game.Content;

namespace Game.Screens;

public sealed class ContentServerScreen : Screen
{
    private const string _typeName = nameof(ContentServerScreen);

    private readonly ListPanelWidget _contentList;
    private readonly ButtonWidget _downloadButton;
    private readonly ButtonWidget _refreshButton;
    private readonly ButtonWidget _uninstallButton;
    private bool _busy;

    public ContentServerScreen()
    {
        var node = ContentManager.Get<XElement>("Screens/ContentServerScreen");
        LoadContents(this, node);
        _contentList = Children.Find<ListPanelWidget>("ContentList")!;
        _downloadButton = Children.Find<ButtonWidget>("Download")!;
        _uninstallButton = Children.Find<ButtonWidget>("Uninstall")!;
        _refreshButton = Children.Find<ButtonWidget>("Refresh")!;
        _contentList.ItemWidgetFactory = item =>
        {
            var content = (ContentCatalogItem)item;
            var widget = (ContainerWidget)LoadWidget(
                this, ContentManager.Get<XElement>("Widgets/ContentServerItem"), null);
            widget.Children.Find<LabelWidget>("ContentServerItem.Name")!.Text =
                $"{content.Name}  {content.Version}";
            widget.Children.Find<LabelWidget>("ContentServerItem.Details")!.Text =
                $"{content.Type} | {content.Identifier} | {DataSizeFormatter.Format(content.PackageSize)}";
            return widget;
        };
    }

    public override void Enter(object[] parameters)
    {
        Refresh();
    }

    public override void Update()
    {
        var selected = _contentList.SelectedItem as ContentCatalogItem;
        var installed = selected is not null && ContentInstallationManager.Load().Any(entry =>
            entry.ContentId == selected.ContentId && entry.VersionId == selected.VersionId);
        _downloadButton.IsEnabled = !_busy && selected is not null && !installed;
        _uninstallButton.IsEnabled = !_busy && selected is not null && installed;
        _refreshButton.IsEnabled = !_busy;

        if (_refreshButton.IsClicked)
        {
            Refresh();
        }
        if (_downloadButton.IsClicked && selected is not null)
        {
            Install(selected);
        }
        if (_uninstallButton.IsClicked && selected is not null)
        {
            try
            {
                ContentInstallationManager.Uninstall(selected);
                Refresh();
            }
            catch (Exception exception)
            {
                ShowError(exception);
            }
        }
        if (Input.Back || Input.Cancel || Children.Find<ButtonWidget>("TopBar.Back")!.IsClicked)
        {
            ScreensManager.SwitchScreen("Content");
        }
    }

    private void Refresh()
    {
        var serverUrl = SettingsManager.Current.ContentServerUrl;
        if (string.IsNullOrWhiteSpace(serverUrl))
        {
            _contentList.ClearItems();
            DialogsManager.Alert(LanguageManager.Get(_typeName, "ServerNotConfigured"));
            return;
        }

        _busy = true;
        Task.Run(async () =>
        {
            try
            {
                using var client = new ContentServerClient(serverUrl);
                var items = await client.ListAsync();
                Dispatcher.Dispatch(() =>
                {
                    _contentList.ClearItems();
                    foreach (var item in items)
                    {
                        _contentList.AddItem(item);
                    }
                    _busy = false;
                });
            }
            catch (Exception exception)
            {
                Dispatcher.Dispatch(() =>
                {
                    _busy = false;
                    ShowError(exception);
                });
            }
        });
    }

    private void Install(ContentCatalogItem item)
    {
        _busy = true;
        Task.Run(async () =>
        {
            try
            {
                using var client = new ContentServerClient(SettingsManager.Current.ContentServerUrl);
                var package = await client.DownloadAsync(item);
                ContentInstallationManager.Install(item, package);
                Dispatcher.Dispatch(() =>
                {
                    _busy = false;
                    DialogsManager.Alert(LanguageManager.Get(_typeName, "Installed"));
                });
            }
            catch (Exception exception)
            {
                Dispatcher.Dispatch(() =>
                {
                    _busy = false;
                    ShowError(exception);
                });
            }
        });
    }

    private static void ShowError(Exception exception)
    {
        DialogsManager.ShowDialog(null, new MessageDialog(
            LanguageManager.Get("Usual", "error"), exception.Message, LanguageManager.Get("Usual", "ok")));
    }
}
