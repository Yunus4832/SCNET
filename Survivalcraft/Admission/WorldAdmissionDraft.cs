namespace Game.Admission;

/// <summary>Private editor state passed with world settings, never serialized into public WorldSettings.</summary>
public sealed class WorldAdmissionDraft
{
    public ServerAdmissionPolicy Configuration { get; set; } = ServerAdmissionPolicy.Open;

    /// <summary>Retains the derived password policy while switching modes in this unsaved editor draft.</summary>
    public ServerAdmissionPolicy? PasswordConfiguration { get; set; }
}
