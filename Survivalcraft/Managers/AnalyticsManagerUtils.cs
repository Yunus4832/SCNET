namespace Game.Managers;

public static class AnalyticsManagerUtils
{
    private static string AbbreviateStackTrace(string stackTrace)
    {
        stackTrace = stackTrace.Replace("System.Collections.Generic.", "");
        stackTrace = stackTrace.Replace("System.Collections.", "");
        stackTrace = stackTrace.Replace("System.IO.", "");
        stackTrace = stackTrace.Replace("Engine.Audio.", "");
        stackTrace = stackTrace.Replace("Engine.Input.", "");
        stackTrace = stackTrace.Replace("Engine.Graphics.", "");
        stackTrace = stackTrace.Replace("Engine.", "");
        if (stackTrace.StartsWith("Engine."))
        {
            stackTrace = stackTrace["Engine.".Length..];
        }

        if (stackTrace.StartsWith("Game."))
        {
            stackTrace = stackTrace["Game.".Length..];
        }

        if (stackTrace.StartsWith("System."))
        {
            stackTrace = stackTrace["System.".Length..];
        }

        return stackTrace;
    }

    private static string[] SplitStackTrace(string stackTrace)
    {
        var list = new List<string>();
        do
        {
            var text = stackTrace.Substring(0, MathUtils.Min(stackTrace.Length, 254));
            list.Add(text);
            stackTrace = stackTrace.Remove(0, text.Length);
        } while (stackTrace.Length > 0 && list.Count < 4);

        return list.ToArray();
    }

    public static AnalyticsParameter[] CreateAnalyticsParametersForError(string message, Exception error)
    {
        var text = ExceptionManager.MakeFullErrorMessage(message, error);
        if (text.Length > 254)
        {
            text = text[..254];
        }

        var array = SplitStackTrace(AbbreviateStackTrace(error.StackTrace ?? string.Empty));
        return
        [
            new AnalyticsParameter("FullMessage", text),
            new AnalyticsParameter("StackTrace1", array.Length >= 1 ? array[0] : string.Empty),
            new AnalyticsParameter("StackTrace2", array.Length >= 2 ? array[1] : string.Empty),
            new AnalyticsParameter("StackTrace3", array.Length >= 3 ? array[2] : string.Empty),
            new AnalyticsParameter("StackTrace4", array.Length >= 4 ? array[3] : string.Empty),
            new AnalyticsParameter("Time", DateTime.Now.ToString("HH:mm:ss.fff"))
        ];
    }
}
