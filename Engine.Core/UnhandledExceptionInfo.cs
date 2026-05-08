namespace Engine.Core;

public class UnhandledExceptionInfo(Exception e)
{
    public readonly Exception Exception = e;

    public bool IsHandled;
}
