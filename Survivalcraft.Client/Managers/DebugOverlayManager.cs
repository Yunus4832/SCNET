using System.Net.Sockets;

using Engine.Graphics;
using Engine.Input;
using Engine.Network;

using Game.Application;
using Game.Commands;

namespace Game.Managers;

public static class DebugOverlayManager
{
    private static readonly PrimitivesRenderer2D _primitivesRenderer = new();

    private static string _contextText = string.Empty;

    private static string _worldText = string.Empty;

    private static double _lastUpdateTime = double.NegativeInfinity;

    public static bool IsVisible => Configuration.ClientConfiguration.Preferences.DisplayDebugInfo;

    public static void Update()
    {
        if (Keyboard.IsKeyDownOnce(Key.F3))
        {
            ToggleVisibility();
        }

        if (!IsVisible || Time.RealTime - _lastUpdateTime < 0.25)
        {
            return;
        }

        _lastUpdateTime = Time.RealTime;
        _contextText = BuildContextText();
        _worldText = BuildWorldText();
    }

    public static void Draw()
    {
        if (!IsVisible || !LabelWidget.IsFontInitialized)
        {
            return;
        }

        var font = LabelWidget.BitmapFont;
        var scale = new Vector2(
            MathUtils.Round(MathUtils.Clamp(ScreensManager.RootWidget.GlobalScale, 1f, 4f)) *
            12f /
            (font.GlyphHeight * font.Scale)
        );
        var margin = 4f * scale.X;
        var maximumPanelWidth =
            MathUtils.Min(MeasurePanel(new string('W', 42), scale).X, Display.Viewport.Width * 0.45f);
        var contentWidth = MathUtils.Max(1f, maximumPanelWidth - 10f * scale.X);
        var contextText = WrapPanelText(_contextText, contentWidth, scale.X);
        var worldText = WrapPanelText(_worldText, contentWidth, scale.X);
        var contextSize = MeasurePanel(contextText, scale);
        var worldSize = string.IsNullOrEmpty(worldText) ? Vector2.Zero : MeasurePanel(worldText, scale);
        var minimumPanelWidth = MathUtils.Min(MeasurePanel(new string('W', 36), scale).X, maximumPanelWidth);
        var panelWidth = MathUtils.Max(minimumPanelWidth, MathUtils.Max(contextSize.X, worldSize.X));
        var stackedHeight = contextSize.Y + worldSize.Y + (worldSize.Y > 0f ? 3f : 2f) * margin;
        var fit = MathUtils.Min(
            1f,
            MathUtils.Min(
                (Display.Viewport.Width - 2f * margin) / panelWidth,
                Display.Viewport.Height / stackedHeight
            )
        );
        var sideBySide = false;
        if (worldSize.Y > 0f && fit < 1f)
        {
            var sideFit = MathUtils.Min(
                1f,
                MathUtils.Min(
                    (Display.Viewport.Width - 3f * margin) / (2f * panelWidth),
                    (Display.Viewport.Height - 2f * margin) / MathUtils.Max(contextSize.Y, worldSize.Y)
                )
            );
            if (sideFit > fit)
            {
                sideBySide = true;
                fit = sideFit;
            }
        }

        fit = MathUtils.Max(0.01f, fit);
        scale *= fit;
        margin *= fit;
        contextSize *= fit;
        worldSize *= fit;
        panelWidth *= fit;
        contextSize.X = panelWidth;
        worldSize.X = panelWidth;
        var contextPosition = new Vector2(Display.Viewport.Width - margin - contextSize.X, margin);
        DrawPanel(contextText, contextPosition, contextSize, scale);

        if (!string.IsNullOrEmpty(_worldText))
        {
            var worldPosition = sideBySide
                ? new Vector2(margin, margin)
                : new Vector2(contextPosition.X, contextPosition.Y + contextSize.Y + margin);
            DrawPanel(worldText, worldPosition, worldSize, scale);
        }

        _primitivesRenderer.Flush();
    }

    public static void ToggleVisibility()
    {
        Configuration.ClientConfiguration.Preferences.DisplayDebugInfo =
            !Configuration.ClientConfiguration.Preferences.DisplayDebugInfo;
        _lastUpdateTime = double.NegativeInfinity;
    }

    private static string BuildContextText()
    {
        var lines = new List<string>
        {
            Text("Context"),
            $"{RowLabel("Screen")}{ScreensManager.GetCurrentScreenName()}"
        };
        var dialog = DialogsManager.ReadOnlyDialogs.LastOrDefault();
        if (dialog != null)
        {
            lines.Add($"{RowLabel("Dialog")}{dialog.GetType().Name}");
            if (DialogsManager.ReadOnlyDialogs.Count > 1)
            {
                lines.Add($"{RowLabel("Depth")}{DialogsManager.ReadOnlyDialogs.Count}");
            }
        }

        var character = GetLocalCharacter();
        if (character?.Entity.FindComponent<ComponentGui>(true)!.ModalPanelWidget is { } panel)
        {
            lines.Add($"{RowLabel("HudPanel")}{panel.GetType().Name}");
        }

        lines.Add(string.Empty);
        lines.Add(Text("Performance"));
        var frameTime = PerformanceManager.AverageFrameTime;
        lines.Add(
            frameTime > 0f
                ? $"{RowLabel("Fps")}{1f / frameTime:0.0} ({frameTime * 1000f:0.0} ms)"
                : $"{RowLabel("Fps")}--"
        );
        lines.Add($"{RowLabel("Cpu")}{PerformanceManager.CpuUtilization * 100f:0}%");
        lines.Add($"{RowLabel("Managed")}{PerformanceManager.TotalMemoryUsed / 1024f / 1024f:0} MB");
        lines.Add($"{RowLabel("Gpu")}{PerformanceManager.TotalGpuMemoryUsed / 1024f / 1024f:0} MB");

        lines.Add(string.Empty);
        lines.Add(Text("Network"));
        var world = ClientApplication.World?.Runtime;
        var node = world is null ? null : WorldNetworkBinding.FindNode(world);
        lines.Add($"{RowLabel("Mode")}{Text($"Role_{node?.Role ?? NodeRole.Inactive}")}");
        if (node is { Role: NodeRole.Client } && node.Server?.Peer is { } peer)
        {
            lines.Add($"{RowLabel("Path")}{GetPathText(peer.PathKind)}");
            lines.Add(
                $"{RowLabel("Ip")}{(peer.TransportEndpoint.AddressFamily == AddressFamily.InterNetworkV6 ? "IPv6" : "IPv4")}"
            );
            lines.Add(
                RowLabel(peer.PathKind == NetworkPathKind.Relay ? "RelayPeer" : "ServerPeer") + peer.TransportEndpoint
            );
            var signalingAddress = node.RemoteTarget switch
            {
                InvitationCodeNetworkTarget target => target.ControlAddress,
                InvitationSessionNetworkTarget target => target.ControlAddress,
                _ => null
            };
            if (signalingAddress is not null)
            {
                lines.Add($"{RowLabel("Signaling")}{signalingAddress.AbsoluteUri}");
            }

            lines.Add($"{RowLabel("Ping")}{peer.RoundTripMilliseconds} ms");
            lines.Add($"{RowLabel("PacketLoss")}{peer.PacketLossPercent:0.##}%");
            lines.Add($"{RowLabel("PendingAck")}{peer.PendingReliableBytes / 1024f:0.##} KB");
        }
        else if (node is { Role: NodeRole.Authority })
        {
            var connections = node.Peers.Select(client => client.Peer).Where(peer => peer?.IsConnected == true)
                .ToArray();
            lines.Add($"{RowLabel("ListenUdp")}{node.LocalPort?.ToString() ?? "--"}");
            lines.Add($"{RowLabel("Clients")}{connections.Length}");
            lines.Add($"{RowLabel("Direct")}{connections.Count(peer => peer!.PathKind == NetworkPathKind.Direct)}");
            lines.Add(
                $"{RowLabel("P2PDirect")}{connections.Count(peer => peer!.PathKind == NetworkPathKind.HolePunch)}"
            );
            lines.Add($"{RowLabel("Relay")}{connections.Count(peer => peer!.PathKind == NetworkPathKind.Relay)}");
            var room = node.GetP2PStatus();
            lines.Add($"{RowLabel("P2PRoom")}{CommandText.Get($"P2PState_{room.State}", room.State.ToString())}");
            if (!string.IsNullOrEmpty(room.ControlUrl))
            {
                lines.Add($"{RowLabel("Signaling")}{room.ControlUrl}");
            }

            if (!string.IsNullOrEmpty(room.LastError))
            {
                lines.Add($"{RowLabel("P2PError")}{room.LastError}");
            }
        }

        return string.Join('\n', lines);
    }

    private static string GetPathText(NetworkPathKind kind) => kind switch
    {
        NetworkPathKind.Direct => Text("AddressDirect"),
        NetworkPathKind.HolePunch => Text("P2PDirect"),
        NetworkPathKind.Relay => Text("Relay"),
        _ => kind.ToString()
    };

    private static string Text(string key) => Localization.ClientLanguage.Catalog.Get("DebugOverlay", key);

    private static string RowLabel(string key) => Text(key).PadRight(11) + " ";

    private static string WrapPanelText(string text, float width, float scale)
    {
        var lines = new List<string>();
        foreach (var line in text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            var offset = 0;
            var indent = line.Length > 12 && line[11] == ' ' ? new string(' ', 12) : string.Empty;
            do
            {
                var prefix = offset == 0 ? string.Empty : indent;
                var available = width - LabelWidget.BitmapFont.MeasureText(prefix, new Vector2(scale), Vector2.Zero).X;
                if (available <= 0f)
                {
                    prefix = string.Empty;
                    available = width;
                }

                var count = LabelWidget.BitmapFont.FitText(available, line, offset, line.Length - offset, scale, 0f);
                count = MathUtils.Min(line.Length - offset, MathUtils.Max(1, count));
                lines.Add(prefix + line.Substring(offset, count));
                offset += count;
            } while (offset < line.Length);
        }

        return string.Join('\n', lines);
    }

    private static string BuildWorldText()
    {
        if (ClientApplication.World?.Project is not { } project || GetLocalCharacter() is not { } character)
        {
            return string.Empty;
        }

        var subsystemTerrain = project.FindSubsystem<SubsystemTerrain>(false);
        var subsystemTimeOfDay = project.FindSubsystem<SubsystemTimeOfDay>(false);
        var subsystemSeasons = project.FindSubsystem<SubsystemSeasons>(false);
        if (subsystemTerrain == null || subsystemTimeOfDay == null || subsystemSeasons == null)
        {
            return string.Empty;
        }

        var position = character.ComponentBody.Position;
        var cell = new Point3(Terrain.ToCell(position.X), Terrain.ToCell(position.Y), Terrain.ToCell(position.Z));
        var chunk = Terrain.ToChunk(cell.X, cell.Z);
        var terrain = subsystemTerrain.Terrain;
        var groundHeight = terrain.GetTopHeight(cell.X, cell.Z);
        var temperature = terrain.GetSeasonalTemperature(cell.X, cell.Z) +
                          SubsystemWeather.GetTemperatureAdjustmentAtHeight(cell.Y);
        var humidity = terrain.GetSeasonalHumidity(cell.X, cell.Z);
        var timeOfDay = subsystemTimeOfDay.TimeOfDay;

        return string.Join(
            '\n',
            Text("Character"),
            $"{RowLabel("Name")}{character.CharacterData.Name}",
            $"{RowLabel("Position")}{position.X:0.00}, {position.Y:0.00}, {position.Z:0.00}",
            $"{RowLabel("Cell")}{cell.X}, {cell.Y}, {cell.Z}",
            $"{RowLabel("Chunk")}{chunk.X}, {chunk.Y}",
            $"{RowLabel("Altitude")}{position.Y:0.00}",
            RowLabel("Ground") +
            string.Format(Text("GroundValue"), groundHeight, (position.Y - groundHeight).ToString("0.00")),
            string.Empty,
            Text("World"),
            $"{RowLabel("Time")}{timeOfDay:0.000}",
            $"{RowLabel("Season")}{ClientApplication.Content.Seasons.GetTimeOfYearName(subsystemTimeOfDay.SubsystemGameInfo.WorldSettings.TimeOfYear)} ({subsystemSeasons.TimeOfSeason:P0})",
            $"{RowLabel("Temperature")}{temperature}",
            $"{RowLabel("Humidity")}{humidity}",
            $"{RowLabel("Light")}{LightingManager.SampleEffectiveLight(subsystemTerrain, cell.X, cell.Y, cell.Z)}"
        );
    }

    private static ComponentCharacter? GetLocalCharacter()
    {
        return ClientApplication.World?.Project?.FindSubsystem<SubsystemCharacters>(false)?.LocalCharacter;
    }

    private static Vector2 MeasurePanel(string text, Vector2 scale)
    {
        var padding = new Vector2(5f) * scale;
        return LabelWidget.BitmapFont.MeasureText(text, scale, Vector2.Zero) + 2f * padding;
    }

    private static void DrawPanel(string text, Vector2 position, Vector2 size, Vector2 scale)
    {
        var padding = new Vector2(5f) * scale;
        _primitivesRenderer.FlatBatch(0, DepthStencilState.None, null, BlendState.AlphaBlend)
            .QueueQuad(position, position + size, 0f, new Color(0, 0, 0, 110));
        _primitivesRenderer.FontBatch(
                LabelWidget.BitmapFont,
                1,
                DepthStencilState.None,
                null,
                BlendState.AlphaBlend,
                SamplerState.LinearClamp
            )
            .QueueText(text, position + padding, 0f, Color.White, TextAnchor.Left, scale, Vector2.Zero);
    }
}
