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
        _downloadButton.IsEnabled = !_busy && selected is not null;
        _uninstallButton.IsEnabled = false;
        _refreshButton.IsEnabled = !_busy;

        if (_refreshButton.IsClicked)
        {
            Refresh();
        }

        if (_downloadButton.IsClicked && selected is not null)
        {
            Install(selected);
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
                var cache = new ContentPackageCache(Storage.GetSystemPath(GamePaths.ContentPackageCache));
                var cached = await client.DownloadToCacheAsync(item, cache);
                Dispatcher.Dispatch(() =>
                {
                    _busy = false;
                    ContentPackageInstallDialogs.Show(cached, busy => _busy = busy,
                        () => DialogsManager.Alert(LanguageManager.Get(_typeName, "Installed")), ShowError);
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
