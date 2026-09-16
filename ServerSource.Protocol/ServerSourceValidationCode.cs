namespace ServerSource.Protocol;

public enum ServerSourceValidationCode
{
    UnsupportedProtocolVersion,
    InvalidSource,
    InvalidSourceId,
    InvalidSourceName,
    InvalidServerCollection,
    TooManyEntries,
    InvalidEntry,
    InvalidEntryId,
    DuplicateEntryId,
    InvalidEntryName,
    InvalidServerAddress,
    DescriptionTooLong,
    TooManyTags,
    InvalidTag,
    InvalidCursor
}
