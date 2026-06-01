using Engine.Input;

namespace Game.Managers;

public static class ExceptionManager
{
    public static Exception? Error { get; } = null;

    public static void ReportExceptionToUser(string additionalMessage, Exception e)
    {
        var arg = MakeFullErrorMessage(additionalMessage, e);
        Log.Error($"{arg}\n{e.StackTrace}");
    }

    public static void DrawExceptionScreen()
    {
    }

    public static void UpdateExceptionScreen()
    {
    }

    public static string MakeFullErrorMessage(Exception e)
    {
        return MakeFullErrorMessage(string.Empty, e);
    }

    public static string MakeFullErrorMessage(string additionalMessage, Exception e)
    {
        var text = string.Empty;
        if (!string.IsNullOrEmpty(additionalMessage))
        {
            text = additionalMessage;
        }

        for (var ex = e; ex != null; ex = ex.InnerException)
        {
            text = text + (text.Length > 0 ? Environment.NewLine : string.Empty) + ex.Message;
        }

        return text;
    }

    public static bool CheckContinueKey()
    {
        return Keyboard.IsKeyDown(Key.F12) || Keyboard.IsKeyDown(Key.Back);
    }
}
