using System.Diagnostics;
using System.Text;
using ImageMagick;
using ImageMagick.Drawing;
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
        try
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
            Console.WriteLine($"Downloading preview: {previewPath}");
            await ffmpeg.WaitForExitAsync();
            Console.WriteLine($"Downloaded preview: {previewPath}");

            return previewPath;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    public async Task ProcessPreview(string path, AudioTrack track)
    {
        var watch = Stopwatch.StartNew();
        using var collection = new MagickImageCollection(path);
        collection.Coalesce();
        var framesList = collection.Select(f => new MagickImage(f)).ToArray();

        var background = new MagickImage(new FileInfo(".//PlayerTemplateF3.png"));
        var width = background.Width;
        var height = background.Height;

        Parallel.ForEach(framesList, new ParallelOptions()
        {
            MaxDegreeOfParallelism = Environment.ProcessorCount * 4,
        }, frame =>
        {
            var preview = frame.Clone();
            preview.Resize(width, height);
            preview.Crop(width - 18, 448, Gravity.Center);

            frame.Alpha(AlphaOption.Transparent);
            frame.Resize(width, height);
            frame.Composite(background, CompositeOperator.Replace);
            frame.Composite(preview, 9, 9, CompositeOperator.Over);

            var drawables = new Drawables()
                .TextEncoding(Encoding.UTF8)
                .Font("Microsoft YaHei & Microsoft YaHei UI")
                .FontPointSize(17)
                .StrokeWidth(0)
                .FillColor(MagickColors.White)
                .TextAlignment(TextAlignment.Left)
                .FontPointSize(28)
                .Text(25, frame.Height - 170, $"{track.Title}")
                .FontPointSize(28)
                .Text(120, frame.Height - 70, $"{track.Author}")
                .FontPointSize(28)
                .Text(frame.Width - 460, frame.Height - 70, $"{track.Requester}")
                .EnableTextAntialias();
            drawables.Draw(frame);
        });

        using var resultCollection = new MagickImageCollection();
        foreach (var frame in framesList)
        {
            resultCollection.Add(frame);
        }

        resultCollection.OptimizePlus();

        await resultCollection.WriteAsync(path);

        watch.Stop();
        Console.WriteLine($"Thumbnail processing took {watch.ElapsedMilliseconds}ms");
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
}