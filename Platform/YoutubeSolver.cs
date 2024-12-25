using System.Diagnostics;
using YoutubeExplode;

namespace KIBAEMON2024_Audio;

public class YoutubeSolver : IPlatformSolver
{
    public bool IsMine(string url)
    {
        return url.Contains("youtube.com") || url.Contains("youtu.be");
    }

    public Task<Stream> Solve(string url)
    {
        var ytdLp = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "yt-dlp",
                Arguments = $"-f bestaudio -o - \"{url}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

        var ffmpeg = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = "-hide_banner -loglevel error -i pipe:0 -b:a 384k -ac 2 -f s16le -ar 48000 pipe:1 -af loudnorm=I=-16:TP=-1.5:LRA=11:measured_I=-11.8:measured_TP=0.5:measured_LRA=7.8:measured_thresh=-21.9:offset=0:linear=true::print_format=summary",
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

        ytdLp.Start();
        ffmpeg.Start();
        StartLogger(ytdLp);
        StartLogger(ffmpeg);

        Task.Run(async () =>
        {
            try
            {
                await ytdLp.StandardOutput.BaseStream.CopyToAsync(ffmpeg.StandardInput.BaseStream);
            }
            catch (Exception ex)
            {
                Console.WriteLine("yt-dlp to ffmpeg pipe error: " + ex.Message);
            }
            finally
            {
                ffmpeg.StandardInput.Close();
            }
        });

        return Task.FromResult(ffmpeg.StandardOutput.BaseStream);
    }

    public async Task<string> DownloadPreview(string url)
    {
        var previewPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".webp");

        var ytdLp = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "yt-dlp",
                Arguments = $"-f bestvideo -g \"{url}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            }
        };
        ytdLp.Start();
        StartLogger(ytdLp);
        var output = await ytdLp.StandardOutput.ReadToEndAsync();
        await ytdLp.WaitForExitAsync();

        var ffmpeg = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = $"-ss 0 -t 3 -i \"{output}\" -vf \"scale=720:-1:force_original_aspect_ratio=decrease,fps=10\" -vcodec libwebp_anim -q:v 66 -loop 0 -y \"{previewPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        ffmpeg.Start();
        StartLogger(ytdLp);
        await ffmpeg.WaitForExitAsync();

        return previewPath;
    }

    public async Task<AudioTrack> FetchTrackInfo(string url)
    {
        var youtube = new YoutubeClient();

        var video = await youtube.Videos.GetAsync(url);

        return new AudioTrack
        {
            Title = video.Title,
            Author = video.Author.ChannelTitle,
            Duration = video.Duration ?? TimeSpan.Zero,
            Url = url,
        };
    }

    private void StartLogger(Process process)
    {
        Task.Run(StartProcessLogger);

        return;

        async Task? StartProcessLogger()
        {
            while (await process.StandardError.ReadLineAsync()! is { } line)
            {
                Console.WriteLine($"[{process.StartInfo.FileName}] {line}");
            }
        }
    }
}