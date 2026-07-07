using System.Xml.Linq;

namespace Game.Dialogs;

public class ViewGameLogDialog : Dialog
{
    private enum FilterType
    {
        /// <summary>
        /// 全部
        /// </summary>
        All,

        /// <summary>
        /// 警告
        /// </summary>
        Warning,

        /// <summary>
        /// 错误
        /// </summary>
        Error
    }

    private const string _typeName = "ViewGameLogDialog";

    private readonly ButtonWidget _closeButton;

    private readonly ButtonWidget _copyButton;

    private FilterType _filter = FilterType.All;

    private readonly ButtonWidget _filterButton;

    private readonly ListPanelWidget _listPanel;

    public ViewGameLogDialog()
    {
        var node = ContentManager.Get<XElement>("Dialogs/ViewGameLogDialog");
        LoadContents(this, node);
        _listPanel = Children.Find<ListPanelWidget>("ViewGameLogDialog.ListPanel")!;
        _copyButton = Children.Find<ButtonWidget>("ViewGameLogDialog.CopyButton")!;
        _filterButton = Children.Find<ButtonWidget>("ViewGameLogDialog.FilterButton")!;
        _closeButton = Children.Find<ButtonWidget>("ViewGameLogDialog.CloseButton")!;
        _listPanel.ItemClicked += delegate(object item)
        {
            if (_listPanel.SelectedItem == item)
            {
                DialogsManager.ShowDialog(
                    ParentWidget,
                    new MessageDialog(
                        "Log Item",
                        item.ToString() ?? string.Empty,
                        "OK"
                    )
                );
            }
        };
        PopulateList();
    }

    public override void Update()
    {
        if (_copyButton.IsClicked)
        {
            ClipboardManager.ClipboardString = GameLogSink.GetRecentLog(131072);
        }

        if (_filterButton.IsClicked)
        {
            _filter = _filter switch
            {
                FilterType.All => FilterType.Warning,
                FilterType.Warning => FilterType.Error,
                FilterType.Error => FilterType.All,
                _ => throw new ArgumentOutOfRangeException()
            };

            PopulateList();
        }

        if (Input.Cancel || _closeButton.IsClicked)
        {
            DialogsManager.HideDialog(this);
        }

        _filterButton.Text = _filter switch
        {
            FilterType.All => LanguageManager.Get(_typeName, "All"),
            FilterType.Warning => LanguageManager.Get(_typeName, "Warning"),
            FilterType.Error => LanguageManager.Get(_typeName, "Error"),
            _ => _filterButton.Text
        };
    }

    public void PopulateList()
    {
        _listPanel.ItemWidgetFactory = delegate(object item)
        {
            var text = item.ToString() ?? string.Empty;
            var color = Color.Gray;
            if (text.Contains("ERROR:"))
            {
                color = Color.Red;
            }
            else if (text.Contains("WARNING:"))
            {
                color = Color.DarkYellow;
            }
            else if (text.Contains("INFO:"))
            {
                color = Color.LightGray;
            }

            return new LabelWidget
            {
                Text = text,
                FontScale = 0.7f,
                HorizontalAlignment = WidgetAlignment.Near,
                VerticalAlignment = WidgetAlignment.Center,
                Color = color
            };
        };
        var recentLogLines = GameLogSink.GetRecentLogLines(131072);
        _listPanel.ClearItems();
        if (recentLogLines.Count > 1000)
        {
            recentLogLines.RemoveRange(0, recentLogLines.Count - 1000);
        }

        foreach (var item in recentLogLines)
        {
            switch (_filter)
            {
                case FilterType.All:
                case FilterType.Warning when GetLogLevel(item) == LogType.Warning:
                case FilterType.Error when GetLogLevel(item) == LogType.Error:
                    _listPanel.AddItem(item);
                    continue;
                default:
                    continue;
            }
        }

        _listPanel.ScrollPosition = _listPanel.Items.Count * _listPanel.ItemSize;
    }

    private LogType GetLogLevel(string logItem)
    {
        if (logItem.Contains("ERROR:", StringComparison.OrdinalIgnoreCase))
        {
            return LogType.Error;
        }

        if (logItem.Contains("WARNING:", StringComparison.OrdinalIgnoreCase))
        {
            return LogType.Warning;
        }

        if (logItem.Contains("INFO:", StringComparison.OrdinalIgnoreCase))
        {
            return LogType.Information;
        }

        if (logItem.Contains("VERBOSE:", StringComparison.OrdinalIgnoreCase))
        {
            return LogType.Verbose;
        }

        if (logItem.Contains("DEBUG:", StringComparison.OrdinalIgnoreCase))
        {
            return LogType.Debug;
        }

        return LogType.Information;
    }
}
