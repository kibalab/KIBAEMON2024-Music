#nullable enable

using System.Diagnostics;
using Discord.Audio;
using Discord.WebSocket;
using KIBAEMON2024_Core.Extension;
using ConnectionState = Discord.ConnectionState;

namespace KIBAEMON2024_Audio;

public class AudioScheduler(ISocketMessageChannel textChannel)
{
    public bool IsPlaying { get; private set; }
    public ulong VoiceChannelId { get; private set; }

    private AudioQueue Queue { get; set; } = new();
    private IAudioClient? AudioClient { get; set; }
    private ISocketMessageChannel TextChannel { get; set; } = textChannel;

    public async Task EnqueueAsync(AudioTrack track, ulong voiceChannelId)
    {
        VoiceChannelId = voiceChannelId;
        Queue.Enqueue(track);
        Console.WriteLine($"Track added: {track.Title}");
        await TextChannel.SendMessageAsync($"`{track.Title}` 을(를) 재생 대기열에 추가했습니다.");

        if (!IsPlaying)
        {
            _ = Task.Run(PlayLoop);
        }
    }

    public void Skip()
    {
        IsPlaying = false;
    }

    public void Stop()
    {
        Queue.Clear();
        IsPlaying = false;
    }

    private async Task PlayLoop()
    {
        IsPlaying = true;

        while (Queue.Any())
        {
            var currentTrack = Queue.Dequeue();
            if (currentTrack == null)
            {
                IsPlaying = false;
                break;
            }

            if (AudioClient?.ConnectionState is not ConnectionState.Connected)
            {
                if (!TextChannel.TryGetGuild(out var guild) || guild is null)
                {
                    await TextChannel.SendMessageAsync("길드를 찾을 수 없습니다.");
                    IsPlaying = false;
                    return;
                }

                if (!guild.TryGetVoiceChannel(VoiceChannelId, out var voiceChannel) || voiceChannel is null)
                {
                    await TextChannel.SendMessageAsync("음성 채널을 찾을 수 없습니다.");
                    IsPlaying = false;
                    return;
                }

                AudioClient = await voiceChannel.ConnectAsync();
            }

            await TextChannel.SendMessageAsync($"`{currentTrack.Title}` 재생을 시작합니다.");

            try
            {
                await using var output = await PlatformManager.StreamSolve(currentTrack.Url);
                await using var discordStream = AudioClient.CreatePCMStream(AudioApplication.Mixed, 96000, packetLoss: 10);

                try
                {
                    await output.CopyToAsync(discordStream);
                }
                finally
                {
                    await discordStream.FlushAsync();
                }
            }
            catch (Exception ex)
            {
                await TextChannel.SendMessageAsync($"재생 중 오류 발생: {ex.Message}");
            }

            if (!Queue.Any())
            {
                IsPlaying = false;
                break;
            }

            if (!IsPlaying)
            {
                IsPlaying = true;
            }
        }

        IsPlaying = false;
    }
}