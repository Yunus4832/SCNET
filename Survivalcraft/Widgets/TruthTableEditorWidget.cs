using System.Xml.Linq;

using Engine.Graphics;

namespace Game.Widgets;

public sealed class TruthTableEditorWidget : CanvasWidget
{
    private readonly ButtonWidget _cancelButton;
    private readonly CheckboxWidget[] _checkboxes = new CheckboxWidget[16];
    private readonly ButtonWidget _clearButton;
    private readonly Action<bool> _completed;
    private readonly Action _close;
    private readonly GridPanelWidget _grid;
    private readonly ButtonWidget _invertButton;
    private readonly ButtonWidget _saveButton;
    private readonly TruthTableData _sourceData;
    private TruthTableData _temporaryData;
    private bool _hasChanges;

    public TruthTableEditorWidget(TruthTableData data, Action<bool> completed, Action close)
    {
        LoadContents(this, ContentManager.Get<XElement>("Widgets/TruthTableEditorWidget"));
        _sourceData = data;
        _temporaryData = (TruthTableData)data.Copy();
        _completed = completed;
        _close = close;
        _grid = Children.Find<GridPanelWidget>("TruthTableEditor.Grid")!;
        _clearButton = Children.Find<ButtonWidget>("TruthTableEditor.Clear")!;
        _invertButton = Children.Find<ButtonWidget>("TruthTableEditor.Invert")!;
        _saveButton = Children.Find<ButtonWidget>("TruthTableEditor.Save")!;
        _cancelButton = Children.Find<ButtonWidget>("TruthTableEditor.Cancel")!;
        CreateCells();
        RefreshCells();
    }

    public override void Update()
    {
        _saveButton.IsEnabled = _hasChanges;
        for (var index = 0; index < _checkboxes.Length; index++)
        {
            if (_checkboxes[index].IsClicked)
            {
                _temporaryData.Data[index] = (byte)(_temporaryData.Data[index] == 0 ? 15 : 0);
                _hasChanges = true;
                RefreshCells();
            }
        }

        if (_clearButton.IsClicked)
        {
            _temporaryData = new TruthTableData();
            _hasChanges = true;
            RefreshCells();
        }

        if (_invertButton.IsClicked)
        {
            for (var index = 0; index < _temporaryData.Data.Length; index++)
            {
                _temporaryData.Data[index] = (byte)(_temporaryData.Data[index] == 0 ? 15 : 0);
            }

            _hasChanges = true;
            RefreshCells();
        }

        if (_saveButton.IsClicked)
        {
            _sourceData.Data = (byte[])_temporaryData.Data.Clone();
            _completed(true);
            _close();
        }

        if (_cancelButton.IsClicked)
        {
            _completed(false);
            _close();
        }
    }

    private void CreateCells()
    {
        for (var index = 0; index < _checkboxes.Length; index++)
        {
            var cell = new StackPanelWidget
            {
                Direction = LayoutDirection.Vertical,
                HorizontalAlignment = WidgetAlignment.Center,
                Margin = new Vector2(3f, 2f)
            };
            cell.Children.Add(new LabelWidget
            {
                Text = Convert.ToString(index, 2).PadLeft(4, '0'),
                Size = new Vector2(62f, 30f),
                FontScale = 0.7f,
                Color = Color.White,
                HorizontalAlignment = WidgetAlignment.Center,
                TextAnchor = TextAnchor.HorizontalCenter
            });
            var checkbox = new CheckboxWidget
            {
                CheckboxSize = new Vector2(32f),
                HorizontalAlignment = WidgetAlignment.Center,
                IsAutoCheckingEnabled = false
            };
            cell.Children.Add(checkbox);
            _checkboxes[index] = checkbox;
            _grid.Children.Add(cell);
            _grid.SetWidgetCell(cell, new Point2(index % 8, index / 8));
        }
    }

    private void RefreshCells()
    {
        for (var index = 0; index < _checkboxes.Length; index++)
        {
            _checkboxes[index].IsChecked = _temporaryData.Data[index] != 0;
        }
    }
}
