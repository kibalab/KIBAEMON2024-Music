#nullable enable

namespace KIBAEMON2024_Audio;

public class AudioTrack
{
    public static AudioTrack Empty => new();

    public string Url { get; init; } = "Unknown";
    public string Title { get; init; } = "Unknown";
    public string Author { get; init; } = "Unknown";
    public TimeSpan Duration { get; init; }

    public string Requester { get; set; } = "Unknown";

    // Duration, Author, Thumbnail, 등등 확장 가능
}