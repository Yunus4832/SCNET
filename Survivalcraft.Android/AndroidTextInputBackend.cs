using Android.App;
using Android.Text;
using Android.Views;
using Android.Views.InputMethods;
using Android.Widget;

using Engine.Input;

using AndroidAlertDialog = Android.App.AlertDialog;
using EngineWindow = Engine.Windowing.Window;

namespace Survivalcraft.Android;

internal sealed class AndroidTextInputBackend : ITextInputBackend
{
    private sealed class DialogRequest(ITextInputSink sink)
    {
        public readonly ITextInputSink Sink = sink;

        public int CompletionState;
    }

    private AndroidAlertDialog? _dialog;

    private DialogRequest? _request;

    public TextInputStyle InputStyle => TextInputStyle.NativeDialog;

    public bool IsAvailable => EngineWindow.ActivityInstance is not null;

    public bool SuppressDirectText => _dialog is not null;

    public void Initialize()
    {
    }

    public void BeginInput(TextInputOptions options, ITextInputSink sink)
    {
        EndInput();
        var request = new DialogRequest(sink);
        _request = request;
        EngineWindow.ActivityInstance.RunOnUiThread(() => ShowDialog(options, request));
    }

    public void EndInput()
    {
        var dialog = _dialog;
        var request = _request;
        _dialog = null;
        _request = null;
        if (request is not null)
        {
            Interlocked.Exchange(ref request.CompletionState, 1);
        }

        if (dialog is not null)
        {
            EngineWindow.ActivityInstance.RunOnUiThread(dialog.Dismiss);
        }
    }

    public void SetCursorRectangle(TextInputRectangle rectangle)
    {
    }

    public bool ProcessKey(TextInputKeyEvent keyEvent) => false;

    public void Update()
    {
    }

    public void OnWindowFocusChanged(bool focused)
    {
    }

    public void Dispose()
    {
        EndInput();
    }

    private void ShowDialog(TextInputOptions options, DialogRequest request)
    {
        if (!ReferenceEquals(_request, request))
        {
            return;
        }

        var editText = new EditText(EngineWindow.ActivityInstance)
        {
            Text = options.InitialText,
            InputType = options.PasswordMode
                ? InputTypes.ClassText | InputTypes.TextVariationPassword
                : InputTypes.ClassText
        };
        editText.SetSelection(editText.Text?.Length ?? 0);

        var builder = new AndroidAlertDialog.Builder(EngineWindow.ActivityInstance);
        builder.SetTitle(options.Title);
        builder.SetMessage(options.Description);
        builder.SetView(editText);
        builder.SetPositiveButton(
            global::Android.Resource.String.Ok,
            (_, _) => Complete(request, editText.Text ?? string.Empty));
        builder.SetNegativeButton(global::Android.Resource.String.Cancel, (_, _) => Cancel(request));

        var dialog = builder.Create();
        if (dialog is null)
        {
            Cancel(request);
            return;
        }

        _dialog = dialog;
        dialog.DismissEvent += (_, _) => Cancel(request);
        dialog.CancelEvent += (_, _) => Cancel(request);
        dialog.Show();
        dialog.Window?.SetGravity(GravityFlags.Center);
        dialog.Window?.SetSoftInputMode(SoftInput.StateAlwaysVisible);
        editText.RequestFocus();
    }

    private static void Complete(DialogRequest request, string text)
    {
        if (Interlocked.Exchange(ref request.CompletionState, 1) == 0)
        {
            request.Sink.Complete(text);
        }
    }

    private static void Cancel(DialogRequest request)
    {
        if (Interlocked.Exchange(ref request.CompletionState, 1) == 0)
        {
            request.Sink.Cancel();
        }
    }
}
