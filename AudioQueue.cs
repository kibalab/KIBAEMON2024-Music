#nullable enable

namespace KIBAEMON2024_Audio;

public class AudioQueue
{
    private Queue<AudioTrack> Queue { get; set; } = new();

    public void Enqueue(AudioTrack track)
    {
        Queue.Enqueue(track);
    }

    public AudioTrack? Dequeue()
    {
        return Queue.Count == 0 ? null : Queue.Dequeue();
    }

    public bool Any()
    {
        return Queue.Count != 0;
    }

    public void Clear()
    {
        Queue.Clear();
    }
}