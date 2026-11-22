namespace Game.Network.Presentation;

/// <summary>在客户端展示时格式化断开连接的原因和上下文。</summary>
public static class DisconnectReasonText
{
    public static string Format(NodeStopReason reason)
    {
        var message = Localization.ClientLanguage.Resolver.Resolve(reason.Message);
        return !string.IsNullOrEmpty(message)
            ? message
            : reason.TransportReason is { } code
                ? Localization.ClientLanguage.Catalog.GetContentWidgets("NetPlayScreen", $"Disconnect_{code}")
                : string.Empty;
    }
}
