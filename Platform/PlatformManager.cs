#nullable enable

namespace KIBAEMON2024_Music.Platform;

public static class PlatformManager
{
    private static List<IPlatformSolver> PlatformSolvers { get; set; } =
    [
        new YoutubeSolver(),
    ];

    public static async Task<Stream> StreamSolve(string url)
    {
        var solver = PlatformSolvers.FirstOrDefault(solver => solver.IsMine(url));
        if (solver is not null) return await solver.Solve(url);

        return new MemoryStream();
    }

    public static async Task<string> DownloadPreview(string url, AudioTrack track)
    {
        var solver = PlatformSolvers.FirstOrDefault(solver => solver.IsMine(url));
        if (solver is not null)
        {
            var path = await solver.DownloadPreview(url);
            try
            {
                await solver.ProcessPreview(path, track);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }

            return path;
        }

        return url;
    }

    public static async Task<AudioTrack> GetTrackInfo(string url)
    {
        var solver = PlatformSolvers.FirstOrDefault(solver => solver.IsMine(url));
        if (solver is not null) return await solver.FetchTrackInfo(url);

        return AudioTrack.Empty;
    }
}

public interface IPlatformSolver
{
    public bool IsMine(string url);

    public Task<Stream> Solve(string url);

    public Task<string> DownloadPreview(string url);
    Task ProcessPreview(string url, AudioTrack track);

    Task<AudioTrack> FetchTrackInfo(string url);
}