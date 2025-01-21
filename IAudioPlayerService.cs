using Discord.WebSocket;
using KIBAEMON2024_Core.Struct;

namespace KIBAEMON2024_Music;

public interface IAudioPlayerService : IService
{
    Task EnqueueAsync(ulong guildId, ulong voiceChannelId, string queryUrl, ISocketMessageChannel textChannel);

    Task SkipAsync(ulong guildId);

    Task StopAsync(ulong guildId);
}