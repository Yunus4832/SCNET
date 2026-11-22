namespace Game.Admission;

/// <summary>可恢复的启动覆盖项仅保存提供方的公开数据和密码文件引用。</summary>
public sealed record ServerAdmissionOverrides
{
    public ServerAdmissionMode? Mode { get; init; }

    public string? PasswordFile { get; init; }

    public string? AuthenticationUrl { get; init; }

    public string? AuthenticationKey { get; init; }

    public string? AuthenticationName { get; init; }

    public bool IsEmpty => Mode is null &&
                           PasswordFile is null &&
                           AuthenticationUrl is null &&
                           AuthenticationKey is null &&
                           AuthenticationName is null;
}
