using System.Xml.Linq;

namespace Game.Dialogs;

public class ViewGameLogDialog : Dialog
{
    private enum LogAction
    {
        Copy,
        Filter,
        Close
    }

    private enum FilterType
    {
        /// <summary>
        ///     全部
        /// </summary>
        All,

        /// <summary>
        ///     警告
        /// </summary>
        Warning,

        /// <summary>
        ///     错误
        /// </summary>
        Error
    }

    private const string _typeName = nameof(ViewGameLogDialog);

    private readonly ActionPanelWidget _actionPanel;

    private FilterType _filter = FilterType.All;

    private readonly ListPanelWidget _listPanel;

    public ViewGameLogDialog()
    {
        var node = ContentManager.Get<XElement>("Dialogs/ViewGameLogDialog");
        LoadContents(this, node);
        _listPanel = Children.Find<ListPanelWidget>("ViewGameLogDialog.ListPanel")!;
        _actionPanel = Children.Find<ActionPanelWidget>("ViewGameLogDialog.Actions")!;
        _actionPanel.ItemTextProvider = GetActionText;
        _actionPanel.ItemClicked += ExecuteAction;
        _actionPanel.SetPrimaryItems(
        [
            LogAction.Filter,
            LogAction.Copy,
            LogAction.Close
        ],
        [
            1f,
            1f,
            1f,
            0f
        ]);
        _listPanel.ItemClicked += delegate (object item)
        {
            if (_listPanel.SelectedItem == item)
            {
                var details = item.ToString() ?? string.Empty;
                DialogsManager.ShowDialog(
                    ParentWidget,
                    new MessageDialog(
                        "Log Item",
                        details,
                        LanguageManager.Ok,
                        string.Empty,
                        new Vector2(760f, -1f),
                        MessageDialog.CancelBehavior.InvokeButton1,
                        _ => { }
                    )
                );
            }
        };
        PopulateList();
    }

    public override void Update()
    {
        if (Input.Cancel)
        {
            DialogsManager.HideDialog(this);
        }
    }

    private string GetActionText(object item)
    {
        return (LogAction)item switch
        {
            LogAction.Copy => LanguageManager.GetContentWidgets(_typeName, "Copy"),
            LogAction.Filter => _filter switch
            {
                FilterType.All => LanguageManager.Get(_typeName, "All"),
                FilterType.Warning => LanguageManager.Get(_typeName, "Warning"),
                FilterType.Error => LanguageManager.Get(_typeName, "Error"),
                _ => throw new ArgumentOutOfRangeException()
            },
            LogAction.Close => LanguageManager.GetContentWidgets(_typeName, "Close"),
            _ => throw new ArgumentOutOfRangeException(nameof(item))
        };
    }

    private void ExecuteAction(object item)
    {
        switch ((LogAction)item)
        {
            case LogAction.Copy:
                ClipboardManager.ClipboardString = GameLogSink.GetRecentLog(131072);
                break;
            case LogAction.Filter:
                _filter = _filter switch
                {
                    FilterType.All => FilterType.Warning,
                    FilterType.Warning => FilterType.Error,
                    FilterType.Error => FilterType.All,
                    _ => throw new ArgumentOutOfRangeException()
                };
                PopulateList();
                break;
            case LogAction.Close:
                DialogsManager.HideDialog(this);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(item));
        }
    }

    public void PopulateList()
    {
        _listPanel.ItemWidgetFactory = delegate (object item)
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
