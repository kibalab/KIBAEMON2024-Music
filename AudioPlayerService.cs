#nullable enable

using Discord.WebSocket;

namespace KIBAEMON2024_Audio;

public class AudioPlayerService : IAudioPlayerService
{
    private Dictionary<ulong, AudioScheduler> Schedulers { get; set; } = new();

    public async Task EnqueueAsync(ulong guildId, ulong voiceChannelId, string queryUrl, ISocketMessageChannel textChannel)
    {
        if (!Schedulers.ContainsKey(guildId))
        {
            Schedulers[guildId] = new AudioScheduler(textChannel);
        }

        var track = await PlatformManager.GetTrackInfo(queryUrl);
        var scheduler = Schedulers[guildId];

        try
        {
            _ = Task.Run(async () =>
            {
                var path = await PlatformManager.DownloadPreview(queryUrl);
                await textChannel.SendFileAsync(path);
            });

            await scheduler.EnqueueAsync(track, voiceChannelId);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    public async Task SkipAsync(ulong guildId)
    {
        if (Schedulers.TryGetValue(guildId, out var queueManager))
        {
            queueManager.Skip();
        }

        await Task.CompletedTask;
    }

    public async Task StopAsync(ulong guildId)
    {
        if (Schedulers.TryGetValue(guildId, out var queueManager))
        {
            queueManager.Stop();
        }

        await Task.CompletedTask;
    }
}