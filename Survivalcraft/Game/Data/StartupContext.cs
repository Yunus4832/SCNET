namespace Game;

public sealed class StartupContext(
    RunningSetting settings,
    StartupRequest request,
    SessionInfo session)
{
    public RunningSetting Settings { get; } = settings;

    public StartupRequest Request { get; } = request;

    public SessionInfo Session { get; } = session;
}
