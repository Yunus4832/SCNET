using System.Reflection;
using System.Runtime.CompilerServices;

using Engine.Core;
using Engine.Graphics;

using Game.Dialogs;
using Game.Widgets;

namespace Survivalcraft.Test.Dialogs;

public sealed class CancellableBusyDialogTest
{
    [Theory]
    [InlineData(0L, 100L, "0%")]
    [InlineData(25L, 100L, "25%")]
    [InlineData(200L, 100L, "100%")]
    [InlineData(-1L, 100L, "0%")]
    [InlineData(25L, null, "")]
    [InlineData(25L, 0L, "")]
    public void DisplaysBoundedProgressAndClearsUnknownTotals(long completed, long? total, string expected)
    {
        var dialog = CreateDialog(new TestButton());
        dialog.SetProgress(50, 100);

        dialog.SetProgress(completed, total);

        Assert.Equal(expected, dialog.SmallMessage);
    }

    [Theory]
    [InlineData(true, 1)]
    [InlineData(false, 0)]
    public void CancelClickNotifiesOnceAndDoesNotOwnTheCancellationSource(bool enabled, int expected)
    {
        var button = new TestButton { IsClicked = true, IsEnabled = enabled };
        var dialog = CreateDialog(button);
        using var cancellation = new CancellationTokenSource();
        var requests = 0;
        dialog.CancelRequested += cancellation.Cancel;
        dialog.CancelRequested += () => requests++;

        dialog.Update();
        dialog.Update();

        Assert.Equal(expected, requests);
        Assert.Equal(enabled, cancellation.IsCancellationRequested);
        Assert.False(dialog.IsCancelButtonEnabled);
        cancellation.CancelAfter(Timeout.Infinite);
    }

    private static CancellableBusyDialog CreateDialog(TestButton cancel)
    {
        // Exercise presentation and input logic without loading fonts or graphics resources.
        var dialog = (CancellableBusyDialog)RuntimeHelpers.GetUninitializedObject(typeof(CancellableBusyDialog));
        SetField("_cancelButtonWidget", cancel);
        SetField("_hideButtonWidget", new TestButton());
        SetField("_smallLabelWidget", RuntimeHelpers.GetUninitializedObject(typeof(LabelWidget)));
        return dialog;

        void SetField(string name, object value) => typeof(CancellableBusyDialog)
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(dialog, value);
    }

    private sealed class TestButton : ButtonWidget
    {
        public override bool IsClicked { get; set; }

        public override bool IsChecked { get; set; }

        public override bool IsAutoCheckingEnabled { get; set; }

        public override string Text { get; set; } = string.Empty;

        public override BitmapFont Font { get; set; } = null!;

        public override Color Color { get; set; }
    }
}
