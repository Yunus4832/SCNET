namespace Game.Managers;

public static class ProgressManager
{
    public static string OperationName { get; set; } = string.Empty;

    public static float Progress { get; set; }

    public static void UpdateProgress(string operationName, float progress)
    {
        OperationName = operationName;
        Progress = MathUtils.Saturate(progress);
    }
}
