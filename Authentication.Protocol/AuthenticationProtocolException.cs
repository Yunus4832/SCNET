namespace SCNET.Authentication.Protocol;

/// <summary>供协议错误使用的安全提示，不含凭据或不受信任的令牌内容。</summary>
public class AuthenticationProtocolException(string message) : Exception(message);
