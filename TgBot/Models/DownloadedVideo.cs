namespace TgBot.Models;

public sealed class DownloadedVideo(
    Stream stream,
    string fileName,
    DirectoryInfo temporaryDirectory) : IAsyncDisposable
{
    public Stream Stream { get; } = stream;
    public string FileName { get; } = fileName;

    public ValueTask DisposeAsync()
    {
        Stream.Dispose();
        temporaryDirectory.Delete(recursive: true);
        return ValueTask.CompletedTask;
    }
}
