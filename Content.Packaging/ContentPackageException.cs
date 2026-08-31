namespace Content.Packaging;

public sealed class ContentPackageException(
    string message,
    Exception? innerException = null
) : Exception(message, innerException);
