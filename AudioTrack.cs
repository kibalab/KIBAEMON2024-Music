#nullable enable

namespace KIBAEMON2024_Audio;

public class AudioTrack
{
    public static AudioTrack Empty => new();

    public string Url { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Author { get; init; } = string.Empty;
    public TimeSpan Duration { get; init; }

    public string Requester { get; set; } = string.Empty;

    // Duration, Author, Thumbnail, 등등 확장 가능
}