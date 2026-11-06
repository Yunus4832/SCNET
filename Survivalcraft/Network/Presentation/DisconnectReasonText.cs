namespace Game.Network.Presentation;

/// <summary>Formats disconnect facts at the client display boundary.</summary>
public static class DisconnectReasonText
{
    public static string Format(NodeStopReason reason) => !string.IsNullOrEmpty(reason.Message)
        ? reason.Message
        : reason.TransportReason is { } code
            ? Localization.ClientLanguage.Catalog.GetContentWidgets("NetPlayScreen", $"Disconnect_{code}")
            : string.Empty;
}
