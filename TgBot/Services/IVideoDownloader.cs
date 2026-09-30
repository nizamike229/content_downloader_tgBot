using TgBot.Models;

namespace TgBot.Services;

public interface IVideoDownloader
{
    Task<DownloadedVideo> DownloadAsync(string url, CancellationToken cancellationToken);
    Task<DownloadedVideo> DownloadAudioAsync(string url, CancellationToken cancellationToken);
}
