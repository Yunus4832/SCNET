namespace ModServer;

public sealed class ModServerOptions
{
    public const string SectionName = "ModServer";

    public string DataDirectory { get; set; } = "Data";

    public List<string> UploadApiKeys { get; set; } = ["change-me"];
}
