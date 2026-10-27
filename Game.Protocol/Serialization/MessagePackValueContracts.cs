using Game.Protocol.Serialization.Formatters;

using MessagePack;

[assembly: MessagePackKnownFormatter(typeof(ProtocolValueFormatter))]

[assembly: MessagePackAssumedFormattable(typeof(Game.Protocol.Identity.PlayerAddress))]
[assembly: MessagePackAssumedFormattable(typeof(Game.Protocol.ResourceId))]
[assembly: MessagePackAssumedFormattable(typeof(Game.Protocol.ModId))]
[assembly: MessagePackAssumedFormattable(typeof(Game.Protocol.Localization.LocalizedText))]
[assembly: MessagePackAssumedFormattable(typeof(Game.Protocol.Messaging.MessageContent))]
[assembly: MessagePackAssumedFormattable(typeof(Game.Protocol.Terrain.CellFace))]
