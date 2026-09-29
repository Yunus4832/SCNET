using Engine.Graphics;
using Engine.Input;
using Engine.Media;

using Game.Network;
using Game.Network.Enums;

namespace Game.Managers;

public static class DebugOverlayManager
{
    private static readonly PrimitivesRenderer2D _primitivesRenderer = new();

    private static string _contextText = string.Empty;

    private static string _worldText = string.Empty;

    private static double _lastUpdateTime = double.NegativeInfinity;

    public static bool IsVisible => SettingsManager.Current.DisplayDebugInfo;

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
        if (!IsVisible)
        {
            return;
        }

        var scale = new Vector2(MathUtils.Round(MathUtils.Clamp(ScreensManager.RootWidget.GlobalScale, 1f, 4f)));
        var margin = 4f * scale.X;
        var contextSize = MeasurePanel(_contextText, scale);
        var worldSize = string.IsNullOrEmpty(_worldText) ? Vector2.Zero : MeasurePanel(_worldText, scale);
        var minimumPanelWidth = MeasurePanel(new string('W', 36), scale).X;
        var panelWidth = MathUtils.Max(minimumPanelWidth, MathUtils.Max(contextSize.X, worldSize.X));
        contextSize.X = panelWidth;
        worldSize.X = panelWidth;
        var contextPosition = new Vector2(Display.Viewport.Width - margin - contextSize.X, margin);
        DrawPanel(_contextText, contextPosition, contextSize, scale);

        if (!string.IsNullOrEmpty(_worldText))
        {
            var worldPosition = new Vector2(Display.Viewport.Width - margin - worldSize.X,
                contextPosition.Y + contextSize.Y + margin);
            DrawPanel(_worldText, worldPosition, worldSize, scale);
        }

        _primitivesRenderer.Flush();
    }

    public static void ToggleVisibility()
    {
        SettingsManager.Current.DisplayDebugInfo = !SettingsManager.Current.DisplayDebugInfo;
        _lastUpdateTime = double.NegativeInfinity;
    }

    private static string BuildContextText()
    {
        var lines = new List<string>
        {
            "CONTEXT",
            $"Screen      {ScreensManager.GetCurrentScreenName()}"
        };
        var dialog = DialogsManager.ReadOnlyDialogs.LastOrDefault();
        if (dialog != null)
        {
            lines.Add($"Dialog      {dialog.GetType().Name}");
            if (DialogsManager.ReadOnlyDialogs.Count > 1)
            {
                lines.Add($"Depth       {DialogsManager.ReadOnlyDialogs.Count}");
            }
        }

        var player = GetMainPlayer();
        if (player?.ComponentGui.ModalPanelWidget is { } panel)
        {
            lines.Add($"HUD Panel   {panel.GetType().Name}");
        }

        lines.Add(string.Empty);
        lines.Add("PERFORMANCE");
        var frameTime = PerformanceManager.AverageFrameTime;
        lines.Add(frameTime > 0f
            ? $"FPS         {1f / frameTime:0.0} ({frameTime * 1000f:0.0} ms)"
            : "FPS         --");
        lines.Add($"CPU         {PerformanceManager.CpuUtilization * 100f:0}%");
        lines.Add($"Managed     {PerformanceManager.TotalMemoryUsed / 1024f / 1024f:0} MB");
        lines.Add($"GPU         {PerformanceManager.TotalGpuMemoryUsed / 1024f / 1024f:0} MB");

        lines.Add(string.Empty);
        lines.Add("NETWORK");
        lines.Add($"Mode        {CommonLib.WorkType}");
        if (CommonLib.WorkType == WorkType.Client && CommonLib.Net.Server?.Peer is { } peer)
        {
            lines.Add($"Ping        {peer.Ping} ms");
            lines.Add($"Packet Loss {peer.Statistics.PacketLossPercent:0.##}%");
        }
        else if (CommonLib.WorkType == WorkType.Server)
        {
            lines.Add($"Clients     {CommonLib.Net.ClientCount - 1}");
        }

        return string.Join('\n', lines);
    }

    private static string BuildWorldText()
    {
        if (GameManager.Project is not { } project || GetMainPlayer() is not { } player)
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

        var position = player.ComponentBody.Position;
        var cell = new Point3(Terrain.ToCell(position.X), Terrain.ToCell(position.Y), Terrain.ToCell(position.Z));
        var chunk = Terrain.ToChunk(cell.X, cell.Z);
        var terrain = subsystemTerrain.Terrain;
        var groundHeight = terrain.GetTopHeight(cell.X, cell.Z);
        var temperature = terrain.GetSeasonalTemperature(cell.X, cell.Z) +
                          SubsystemWeather.GetTemperatureAdjustmentAtHeight(cell.Y);
        var humidity = terrain.GetSeasonalHumidity(cell.X, cell.Z);
        var timeOfDay = subsystemTimeOfDay.TimeOfDay;

        return string.Join('\n',
            "PLAYER",
            $"Name        {player.PlayerData.Name}",
            $"Position    {position.X:0.00}, {position.Y:0.00}, {position.Z:0.00}",
            $"Cell        {cell.X}, {cell.Y}, {cell.Z}",
            $"Chunk       {chunk.X}, {chunk.Y}",
            $"Altitude    {position.Y:0.00}",
            $"Ground      {groundHeight} ({position.Y - groundHeight:0.00} above)",
            string.Empty,
            "WORLD",
            $"Time        {timeOfDay:0.000}",
            $"Season      {subsystemSeasons.Season} ({subsystemSeasons.TimeOfSeason:P0})",
            $"Temperature {temperature}",
            $"Humidity    {humidity}",
            $"Light       {terrain.GetCellLight(cell.X, cell.Y, cell.Z)}");
    }

    private static ComponentPlayer? GetMainPlayer()
    {
        return GameManager.Project?.FindSubsystem<SubsystemPlayers>(false)?.MainPlayer;
    }

    private static Vector2 MeasurePanel(string text, Vector2 scale)
    {
        var padding = new Vector2(5f) * scale;
        return BitmapFont.DebugFont.MeasureText(text, scale, Vector2.Zero) + 2f * padding;
    }

    private static void DrawPanel(string text, Vector2 position, Vector2 size, Vector2 scale)
    {
        var padding = new Vector2(5f) * scale;
        _primitivesRenderer.FlatBatch(0, DepthStencilState.None, null, BlendState.AlphaBlend)
            .QueueQuad(position, position + size, 0f, new Color(0, 0, 0, 110));
        _primitivesRenderer.FontBatch(BitmapFont.DebugFont, 1, DepthStencilState.None, null, BlendState.AlphaBlend,
                SamplerState.PointClamp)
            .QueueText(text, position + padding, 0f, Color.White, TextAnchor.Left, scale, Vector2.Zero);
    }
}
