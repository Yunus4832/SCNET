namespace ServerSource.Protocol;

public enum ServerSourceValidationCode
{
    UnsupportedProtocolVersion = 0,

    InvalidSource = 1,

    InvalidSourceId = 2,

    InvalidSourceName = 3,

    InvalidServerCollection = 4,

    TooManyEntries = 5,

    InvalidEntry = 6,

    InvalidEntryId = 7,

    DuplicateEntryId = 8,

    InvalidEntryName = 9,

    InvalidServerAddress = 10,

    DescriptionTooLong = 11,

    TooManyTags = 12,

    InvalidTag = 13,

    InvalidCursor = 14
}
