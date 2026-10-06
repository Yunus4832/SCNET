using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace ServerLoadTool;

public sealed class ServerHttpProbe : IDisposable
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(5) };
    private Task<JsonElement>? _pending;
    private readonly string _endpoint;
    private bool _discovered;
    public string? Error { get; private set; }
    public JsonElement? Latest { get; private set; }

    public ServerHttpProbe(int port)
    {
        var token = Environment.GetEnvironmentVariable("SCNET_LOAD_TOKEN");
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new ArgumentException("--server-http-port requires SCNET_LOAD_TOKEN in the environment; it is not recorded.");
        }

        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        _endpoint = $"http://127.0.0.1:{port}/commands";
    }

    public void Sample()
    {
        if (_pending?.IsCompleted == true)
        {
            try
            {
                Latest = _pending.GetAwaiter().GetResult();
            }
            catch (Exception exception)
            {
                Error ??= exception.Message;
            }

            _pending = null;
        }

        _pending ??= ReadAsync();
    }

    private async Task<JsonElement> ReadAsync()
    {
        if (!_discovered)
        {
            using var discovery = await _http.GetAsync(_endpoint);
            discovery.EnsureSuccessStatusCode();
            using var commands = JsonDocument.Parse(await discovery.Content.ReadAsStringAsync());
            if (!commands.RootElement.GetProperty("commands").EnumerateArray()
                    .Any(command => command.GetProperty("identity").GetString() == "game:diagnostics/network/get"))
            {
                throw new InvalidOperationException("Server diagnostics are not available on this HTTP host.");
            }

            _discovered = true;
        }

        using var content = new StringContent("""{"identity":"game:diagnostics/network/get","arguments":{}}""", Encoding.UTF8, "application/json");
        using var response = await _http.PostAsync(_endpoint, content);
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        if (!json.RootElement.GetProperty("success").GetBoolean())
        {
            throw new InvalidOperationException("Server diagnostic command failed.");
        }

        return json.RootElement.GetProperty("data").Clone();
    }

    public void Dispose() => _http.Dispose();
}
