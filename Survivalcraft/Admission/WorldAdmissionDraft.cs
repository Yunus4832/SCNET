namespace Game.Admission;

/// <summary>随世界设置传递的编辑器私有状态，不序列化进公开的 WorldSettings。</summary>
public sealed class WorldAdmissionDraft
{
    public ServerAdmissionPolicy Configuration { get; set; } = ServerAdmissionPolicy.Open;

    /// <summary>在尚未保存的编辑草稿中切换模式时，保留已推导的密码策略。</summary>
    public ServerAdmissionPolicy? PasswordConfiguration { get; set; }
}
