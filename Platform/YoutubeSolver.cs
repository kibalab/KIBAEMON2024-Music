using System.Diagnostics;
using OpenCvSharp;
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

        // 1) GIF 파일로부터 모든 프레임 읽어오기
        using var capture = new VideoCapture(path);

        // 총 프레임 수 및 FPS
        int frameCount = (int)capture.Get(VideoCaptureProperties.FrameCount);
        double fps = capture.Get(VideoCaptureProperties.Fps);

        // 혹은 FPS가 0으로 잡히면 기본값으로 설정
        if (fps <= 0) fps = 10;

        // 2) 배경 이미지 로드 (PNG)
        Mat background = Cv2.ImRead(".//PlayerTemplateF3.png");
        int width = background.Width;
        int height = background.Height;

        var frames = new List<Mat>();

        // GIF 프레임들을 리스트로 추출
        for (int i = 0; i < frameCount; i++)
        {
            var frame = new Mat();
            bool success = capture.Read(frame);
            if (!success)
                break;

            // OpenCV에서 Read한 프레임은 내부 버퍼가 바뀔 수 있으므로 Clone() 해서 리스트에 보관
            frames.Add(frame.Clone());
        }

        capture.Release(); // 모든 프레임 추출 후 닫기

        // 3) 병렬로 각 프레임 처리
        Parallel.ForEach(frames, new ParallelOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount * 4
            },
            frame =>
            {
                // (a) preview = frame.Clone()
                Mat preview = frame.Clone();

                // (b) preview.Resize(width, height)
                Cv2.Resize(preview, preview, new Size(width, height));

                // (c) preview.Crop(width - 18, 448, Gravity.Center) 와 유사한 처리
                //    - Gravity.Center는 중앙 기준으로 crop하는 것을 의미하므로,
                //      여기서는 단순히 중앙을 기준으로 Rect를 계산합니다.
                int cropW = width - 18;
                int cropH = 448;
                int offsetX = (preview.Width - cropW) / 2;
                int offsetY = (preview.Height - cropH) / 2;
                // 범위 보호
                offsetX = Math.Max(0, offsetX);
                offsetY = Math.Max(0, offsetY);
                cropW = Math.Min(cropW, preview.Width - offsetX);
                cropH = Math.Min(cropH, preview.Height - offsetY);

                var cropRect = new Rect(offsetX, offsetY, cropW, cropH);
                Mat croppedPreview = new Mat(preview, cropRect);

                // (d) 원본코드 frame.Alpha(AlphaOption.Transparent) → 
                //     OpenCV에서는 RGBA를 다루거나 별도의 마스크 연산이 필요.
                //     이 예제에선 알파 처리 부분 생략(불투명으로 처리).

                // (e) frame.Resize(width, height)
                Cv2.Resize(frame, frame, new Size(width, height));

                // (f) frame.Composite(background, CompositeOperator.Replace)
                //     - 배경을 frame 위에 "전부 교체"하는 효과이므로, background를 통째로 복사
                background.CopyTo(frame);

                // (g) frame.Composite(preview, 9, 9, CompositeOperator.Over)
                //     - preview 이미지를 (9, 9) 위치에 합성
                //     - OpenCV에선 ROI를 이용하거나, 부분 Mat에 CopyTo
                if (9 + croppedPreview.Width <= frame.Width &&
                    9 + croppedPreview.Height <= frame.Height)
                {
                    var roi = new Rect(9, 9, croppedPreview.Width, croppedPreview.Height);
                    croppedPreview.CopyTo(frame[roi]);
                }

                // (h) 텍스트 그리기
                //     - Magick.NET에서의 Drawables() → OpenCV에서는 Cv2.PutText 등을 이용
                //     - 폰트나 정렬, 문자열 인코딩 등은 환경에 맞춰 조정하세요.

                // 제목
                Cv2.PutText(frame,
                    track.Title ?? "",
                    new Point(25, frame.Height - 170), // 좌표
                    HersheyFonts.HersheySimplex, // 폰트
                    1.0, // 폰트 스케일
                    Scalar.White, // 글자 색
                    2, // 두께
                    LineTypes.AntiAlias);

                // 작곡가/가수
                Cv2.PutText(frame,
                    track.Author ?? "",
                    new Point(120, frame.Height - 70),
                    HersheyFonts.HersheySimplex,
                    1.0,
                    Scalar.White,
                    2,
                    LineTypes.AntiAlias);

                // 요청자
                Cv2.PutText(frame,
                    track.Requester ?? "",
                    new Point(frame.Width - 460, frame.Height - 70),
                    HersheyFonts.HersheySimplex,
                    1.0,
                    Scalar.White,
                    2,
                    LineTypes.AntiAlias);
            });

        // 4) 최종 출력
        //    OpenCVSharp는 애니메이션 GIF 직저장이 불가능하므로, 여기서는 MP4로 저장 예시
        //    (ffmpeg 등 추가 사용 시 GIF로 변환 가능)
        string outputFile = Path.ChangeExtension(path, ".mp4");

        // mp4 인코더 설정 (Windows 환경 가정)
        int fourcc = VideoWriter.FourCC('m', 'p', '4', 'v');
        using var writer = new VideoWriter(outputFile, fourcc, fps, new OpenCvSharp.Size(width, height));

        foreach (var frame in frames)
        {
            writer.Write(frame);
        }

        writer.Release();

        watch.Stop();
        Console.WriteLine($"Thumbnail processing took {watch.ElapsedMilliseconds}ms");

        // 만약 GIF로 내보내야 한다면, 별도의 라이브러리나 ffmpeg 등을 통해
        // frames 리스트를 GIF로 인코딩하는 로직을 직접 작성해야 합니다.
        await Task.CompletedTask;
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