namespace Engine.Audio;

internal readonly record struct AudioPlaybackRequest(
    SoundState State,
    long Revision,
    long Generation,
    float Volume,
    float Pitch,
    float Pan,
    bool Looped,
    bool DisposeOnStop
);
