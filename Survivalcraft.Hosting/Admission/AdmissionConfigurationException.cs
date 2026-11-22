namespace Game.Admission;

/// <summary>显式准入配置无效时拒绝准入，避免退回默认会话。</summary>
public sealed class AdmissionConfigurationException(string message, Exception? inner = null)
    : Exception(message, inner);
