using Dialog = Game.Dialogs.Dialog;

namespace Game.Managers;

public static class DialogsManager
{
    private static readonly Dictionary<Dialog, AnimationData> _animationDataDict = new();

    private static readonly List<Dialog> _dialogs = [];

    private static readonly List<Dialog> _loadingDialogs = [];

    private static readonly List<Dialog> _toRemove = [];

    public static ReadOnlyList<Dialog> ReadOnlyDialogs => new(_dialogs);

    public static bool HasDialogs(Widget? parentWidget)
    {
        parentWidget ??= ScreensManager.CurrentScreen ?? ScreensManager.RootWidget;
        return _dialogs.Any(dialog => dialog.ParentWidget == parentWidget);
    }

    public static void ShowDialog(ContainerWidget? parentWidget, Dialog dialog)
    {
        Dispatcher.Dispatch(delegate
        {
            if (_dialogs.Contains(dialog))
            {
                return;
            }

            parentWidget ??= ScreensManager.CurrentScreen ?? ScreensManager.RootWidget;
            dialog.WidgetsHierarchyInput = null;
            _dialogs.Add(dialog);
            var animationData = new AnimationData
            {
                Direction = 1
            };
            _animationDataDict[dialog] = animationData;
            parentWidget?.Children.Add(animationData.CoverRectangle);
            dialog.ParentWidget?.Children.Remove(dialog);
            parentWidget?.Children.Add(dialog);
            UpdateDialog(dialog, animationData);
            dialog.Input.Clear();
        });
    }

    public static void HideDialog(Dialog dialog)
    {
        Dispatcher.Dispatch(delegate
        {
            if (!_dialogs.Contains(dialog))
            {
                return;
            }

            dialog.ParentWidget?.Input.Clear();
            dialog.WidgetsHierarchyInput = new WidgetInput(WidgetInputDevice.None);
            _dialogs.Remove(dialog);
            _animationDataDict[dialog].Direction = -1;
        });
    }

    public static void HideLoadingDialogs()
    {
        var array = _loadingDialogs.ToArray();
        foreach (var item in array)
        {
            HideDialog(item);
        }

        _loadingDialogs.Clear();
    }

    public static void HideAllDialogs()
    {
        var array = _dialogs.ToArray();
        foreach (var item in array)
        {
            HideDialog(item);
        }
    }

    public static void Alert(string title, string msg, ContainerWidget? parentWidget = null)
    {
        ShowDialog(parentWidget, new MessageDialog(title, msg, LanguageControl.Ok));
    }

    public static void Alert(string msg, ContainerWidget? parentWidget = null)
    {
        Alert("提示", msg, parentWidget);
    }

    public static void Confirm(string msg, Action<MessageDialogButton> clickEvent, ContainerWidget? parentWidget = null)
    {
        var dialog = new MessageDialog(
            LanguageControl.Warning,
            msg,
            LanguageControl.Yes,
            LanguageControl.No,
            new Vector2(-1f),
            (button, self) =>
            {
                HideDialog(self);
                clickEvent(button);
            }
        );
        ShowDialog(parentWidget, dialog);
    }

    public static void Prompt(
        string title,
        string defaultValue,
        Action<string> ok,
        int maxTextLength = 64,
        ContainerWidget? parentWidget = null
    )
    {
        ShowDialog(parentWidget, new TextBoxDialog(title, defaultValue, maxTextLength, ok));
    }

    private static void Loading(string title, string msg)
    {
        var dialog = new BusyDialog(title, msg);
        ShowDialog(null, dialog);
        _loadingDialogs.Add(dialog);
    }

    public static void Loading(string msg)
    {
        Loading(string.Empty, msg);
    }

    public static void Update()
    {
        foreach (var (key, value) in _animationDataDict)
        {
            switch (value.Direction)
            {
                case > 0:
                    value.Factor = MathUtils.Min(value.Factor + 6f * Time.FrameDuration, 1f);
                    break;
                case < 0:
                {
                    value.Factor = MathUtils.Max(value.Factor - 6f * Time.FrameDuration, 0f);
                    if (value.Factor <= 0f)
                    {
                        _toRemove.Add(key);
                    }

                    break;
                }
            }

            UpdateDialog(key, value);
        }

        foreach (var item in _toRemove)
        {
            var animationData = _animationDataDict[item];
            _animationDataDict.Remove(item);
            item.ParentWidget?.Children.Remove(item);
            animationData.CoverRectangle.ParentWidget?.Children.Remove(animationData.CoverRectangle);
        }

        _toRemove.Clear();
    }

    private static void UpdateDialog(Dialog dialog, AnimationData animationData)
    {
        if (animationData.Factor < 1f)
        {
            var factor = animationData.Factor;
            var num = 0.75f + 0.25f * MathUtils.Pow(animationData.Factor, 0.25f);
            dialog.RenderTransform =
                Matrix.CreateTranslation((0f - dialog.ActualSize.X) / 2f, (0f - dialog.ActualSize.Y) / 2f, 0f) *
                Matrix.CreateScale(num, num, 1f) *
                Matrix.CreateTranslation(dialog.ActualSize.X / 2f, dialog.ActualSize.Y / 2f, 0f);
            dialog.ColorTransform = Color.White * factor;
            animationData.CoverRectangle.ColorTransform = Color.White * factor;
        }
        else
        {
            dialog.RenderTransform = Matrix.Identity;
            dialog.ColorTransform = Color.White;
            animationData.CoverRectangle.ColorTransform = Color.White;
        }
    }

    public class AnimationData
    {
        public readonly RectangleWidget CoverRectangle = new()
        {
            OutlineColor = Color.Transparent,
            FillColor = new Color(0, 0, 0, 192),
            IsHitTestVisible = true
        };

        public int Direction;
        public float Factor;
    }
}
