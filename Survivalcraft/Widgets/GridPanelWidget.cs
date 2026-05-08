namespace Game.Widgets;

public class GridPanelWidget : ContainerWidget
{
    private readonly Dictionary<Widget, Point2> _cells = new();

    private List<Column> _columns = [];

    private List<Row> _rows = [];

    public GridPanelWidget()
    {
        ColumnsCount = 1;
        RowsCount = 1;
    }

    public int ColumnsCount
    {
        get => _columns.Count;
        set
        {
            _columns = new List<Column>(_columns.GetRange(0, MathUtils.Min(_columns.Count, value)));
            while (_columns.Count < value)
            {
                _columns.Add(new Column());
            }
        }
    }

    public int RowsCount
    {
        get => _rows.Count;
        set
        {
            _rows = new List<Row>(_rows.GetRange(0, MathUtils.Min(_rows.Count, value)));
            while (_rows.Count < value)
            {
                _rows.Add(new Row());
            }
        }
    }

    public Point2 GetWidgetCell(Widget widget)
    {
        _cells.TryGetValue(widget, out var value);
        return value;
    }

    public void SetWidgetCell(Widget widget, Point2 cell)
    {
        _cells[widget] = cell;
    }

    public static void SetCell(Widget widget, Point2 cell)
    {
        (widget.ParentWidget as GridPanelWidget)?.SetWidgetCell(widget, cell);
    }

    public override void WidgetRemoved(Widget widget)
    {
        _cells.Remove(widget);
    }

    protected override void MeasureOverride(Vector2 parentAvailableSize)
    {
        foreach (var column2 in _columns)
        {
            column2.ActualWidth = 0f;
        }

        foreach (var row2 in _rows)
        {
            row2.ActualHeight = 0f;
        }

        foreach (var child in Children)
        {
            child.Measure(Vector2.Max(parentAvailableSize - 2f * child.Margin, Vector2.Zero));
            var widgetCell = GetWidgetCell(child);
            if (IsCellValid(widgetCell))
            {
                var column = _columns[widgetCell.X];
                column.ActualWidth = MathUtils.Max(column.ActualWidth, child.ParentDesiredSize.X + 2f * child.Margin.X);
                var row = _rows[widgetCell.Y];
                row.ActualHeight = MathUtils.Max(row.ActualHeight, child.ParentDesiredSize.Y + 2f * child.Margin.Y);
            }
        }

        var zero = Vector2.Zero;
        foreach (var column3 in _columns)
        {
            column3.Position = zero.X;
            zero.X += column3.ActualWidth;
        }

        foreach (var row3 in _rows)
        {
            row3.Position = zero.Y;
            zero.Y += row3.ActualHeight;
        }

        DesiredSize = zero;
    }

    public override void ArrangeOverride()
    {
        foreach (var child in Children)
        {
            var widgetCell = GetWidgetCell(child);
            if (IsCellValid(widgetCell))
            {
                var column = _columns[widgetCell.X];
                var row = _rows[widgetCell.Y];
                ArrangeChildWidgetInCell(new Vector2(column.Position, row.Position),
                    new Vector2(column.Position + column.ActualWidth, row.Position + row.ActualHeight), child);
            }
            else
            {
                ArrangeChildWidgetInCell(Vector2.Zero, ActualSize, child);
            }
        }
    }

    public bool IsCellValid(Point2 cell)
    {
        if (cell.X >= 0 && cell.X < _columns.Count && cell.Y >= 0)
        {
            return cell.Y < _rows.Count;
        }

        return false;
    }

    public class Column
    {
        public float ActualWidth;
        public float Position;
    }

    public class Row
    {
        public float ActualHeight;
        public float Position;
    }
}
