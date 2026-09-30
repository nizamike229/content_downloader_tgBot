using System.Diagnostics;
using TgBot.Models;

namespace TgBot.Services;

public sealed class YtDlpDownloader(string executable) : IVideoDownloader
{
    public async Task<DownloadedVideo> DownloadAsync(
        string url,
        CancellationToken cancellationToken)
    {
        return await DownloadAsync(url, isAudioOnly: false, cancellationToken);
    }

    public async Task<DownloadedVideo> DownloadAudioAsync(
        string url,
        CancellationToken cancellationToken)
    {
        return await DownloadAsync(url, isAudioOnly: true, cancellationToken);
    }

    private async Task<DownloadedVideo> DownloadAsync(
        string url,
        bool isAudioOnly,
        CancellationToken cancellationToken)
    {
        var directory = Directory.CreateTempSubdirectory("tgbot-");
        var outputTemplate = Path.Combine(
            directory.FullName,
            isAudioOnly ? "%(id)s.%(ext)s" : "%(id)s.%(ext)s");

        try
        {
            var startInfo = CreateProcessStartInfo(
                url,
                directory.FullName,
                outputTemplate,
                isAudioOnly);
            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Could not start yt-dlp.");
            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

            await process.WaitForExitAsync(cancellationToken);
            var error = await errorTask;

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    string.IsNullOrWhiteSpace(error)
                        ? "yt-dlp exited with an error."
                        : error.Trim());
            }

            var file = Directory.EnumerateFiles(directory.FullName)
                .FirstOrDefault(path => !path.EndsWith(".part", StringComparison.OrdinalIgnoreCase));
            if (file is null)
                throw new InvalidOperationException(
                    isAudioOnly
                        ? "The audio was not found after downloading."
                        : "The video was not found after downloading.");

            return new DownloadedVideo(
                new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read),
                Path.GetFileName(file),
                directory);
        }
        catch
        {
            directory.Delete(recursive: true);
            throw;
        }
    }

    private ProcessStartInfo CreateProcessStartInfo(
        string url,
        string workingDirectory,
        string outputTemplate,
        bool isAudioOnly)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("--no-playlist");
        startInfo.ArgumentList.Add("--max-filesize");
        startInfo.ArgumentList.Add("49M");
        if (isAudioOnly)
        {
            startInfo.ArgumentList.Add("-f");
            startInfo.ArgumentList.Add("bestaudio/best");
            startInfo.ArgumentList.Add("--extract-audio");
            startInfo.ArgumentList.Add("--audio-format");
            startInfo.ArgumentList.Add("m4a");
            startInfo.ArgumentList.Add("--audio-quality");
            startInfo.ArgumentList.Add("0");
        }
        else
        {
            startInfo.ArgumentList.Add("-f");
            startInfo.ArgumentList.Add("bestvideo+bestaudio/best");
            startInfo.ArgumentList.Add("-S");
            startInfo.ArgumentList.Add("res,fps,br");
            startInfo.ArgumentList.Add("--merge-output-format");
            startInfo.ArgumentList.Add("mp4");
            startInfo.ArgumentList.Add("--recode-video");
            startInfo.ArgumentList.Add("mp4");
        }
        startInfo.ArgumentList.Add("-o");
        startInfo.ArgumentList.Add(outputTemplate);
        startInfo.ArgumentList.Add(url);
        return startInfo;
    }
}
