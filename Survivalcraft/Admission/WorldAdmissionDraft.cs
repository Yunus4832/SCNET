namespace Game.Admission;

/// <summary>Private editor state passed with world settings, never serialized into public WorldSettings.</summary>
public sealed class WorldAdmissionDraft
{
    public ServerAdmissionPolicy Configuration { get; set; } = ServerAdmissionPolicy.Open;
}
