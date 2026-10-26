namespace Game.Admission;

/// <summary>Explicit admission configuration must fail closed rather than become a default session.</summary>
public sealed class AdmissionConfigurationException(string message, Exception? inner = null)
    : Exception(message, inner);
