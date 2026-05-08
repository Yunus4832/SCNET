using System.Collections;
using System.Xml.Linq;

namespace Game.Dialogs;

public class ListSelectionDialog : Dialog
{
    private readonly CanvasWidget _contentWidget;

    private double? _dismissTime;

    private bool _isDismissed;

    private readonly ListPanelWidget _listWidget;

    private readonly Action<object> _selectionHandler;

    private readonly LabelWidget _titleLabelWidget;

    public ListSelectionDialog(
        string title,
        IEnumerable items,
        float itemSize,
        Func<object, Widget> itemWidgetFactory,
        Action<object> selectionHandler
    )
    {
        _selectionHandler = selectionHandler;
        var node = ContentManager.Get<XElement>("Dialogs/ListSelectionDialog");
        LoadContents(this, node);
        _titleLabelWidget = Children.Find<LabelWidget>("ListSelectionDialog.Title")!;
        _listWidget = Children.Find<ListPanelWidget>("ListSelectionDialog.List")!;
        _contentWidget = Children.Find<CanvasWidget>("ListSelectionDialog.Content")!;
        _titleLabelWidget.Text = title;
        _titleLabelWidget.IsVisible = !string.IsNullOrEmpty(title);
        _listWidget.ItemSize = itemSize;
        _listWidget.ItemWidgetFactory = itemWidgetFactory;
        foreach (var item in items)
        {
            _listWidget.AddItem(item);
        }

        var num = _listWidget.Items.Count;
        float num2;
        while (true)
        {
            if (num < 0)
            {
                return;
            }

            num2 = MathUtils.Min(num + 0.5f, _listWidget.Items.Count);
            if (num2 * itemSize <= _contentWidget.Size.Y)
            {
                break;
            }

            num--;
        }

        _contentWidget.Size = new Vector2(_contentWidget.Size.X, num2 * itemSize);
    }

    public ListSelectionDialog(
        string title,
        IEnumerable items,
        float itemSize,
        Func<object, string> itemToStringConverter,
        Action<object> selectionHandler
    ) : this(
        title,
        items,
        itemSize,
        item => new LabelWidget
        {
            Text = itemToStringConverter(item),
            HorizontalAlignment = WidgetAlignment.Center,
            VerticalAlignment = WidgetAlignment.Center
        },
        selectionHandler
    )
    {
    }

    public Vector2 ContentSize
    {
        get => _contentWidget.Size;
        set => _contentWidget.Size = value;
    }

    public override void Update()
    {
        if (Input.Back || Input.Cancel)
        {
            _dismissTime = 0.0;
        }
        else if (Input.Tap.HasValue && !_listWidget.HitTest(Input.Tap.Value))
        {
            _dismissTime = 0.0;
        }
        else if (!_dismissTime.HasValue && _listWidget.SelectedItem != null)
        {
            _dismissTime = Time.FrameStartTime + 0.05000000074505806;
        }

        if (_dismissTime.HasValue && Time.FrameStartTime >= _dismissTime.Value)
        {
            Dismiss(_listWidget.SelectedItem);
        }
    }

    public void Dismiss(object? result)
    {
        if (_isDismissed)
        {
            return;
        }

        _isDismissed = true;
        DialogsManager.HideDialog(this);
        if (result != null)
        {
            _selectionHandler(result);
        }
    }
}
