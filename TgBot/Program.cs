
using System.Diagnostics;
using System.Text.RegularExpressions;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

const string botTokenVariable = "TELEGRAM_BOT_TOKEN";
var botToken = Environment.GetEnvironmentVariable(botTokenVariable);

if (string.IsNullOrWhiteSpace(botToken))
{
    Console.Error.WriteLine($"Set {botTokenVariable} before starting the bot.");
    return;
}

var bot = new TelegramBotClient(botToken);
var downloader = new YtDlpDownloader(
    Environment.GetEnvironmentVariable("YTDLP_PATH") ?? "yt-dlp");

using var cancellationTokenSource = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellationTokenSource.Cancel();
};

var receiverOptions = new ReceiverOptions
{
    AllowedUpdates = [UpdateType.Message]
};

bot.StartReceiving(
    updateHandler: HandleUpdateAsync,
    errorHandler: HandlePollingErrorAsync,
    receiverOptions: receiverOptions,
    cancellationToken: cancellationTokenSource.Token);

var me = await bot.GetMe(cancellationTokenSource.Token);
Console.WriteLine($"@{me.Username} is running. Press Ctrl+C to stop.");
await Task.Delay(Timeout.Infinite, cancellationTokenSource.Token)
    .ContinueWith(_ => { }, CancellationToken.None);

async Task HandleUpdateAsync(ITelegramBotClient client, Update update, CancellationToken cancellationToken)
{
    if (update.Message is not { Text: { } text } message)
        return;

    if (text.Equals("/start", StringComparison.OrdinalIgnoreCase))
    {
        await client.SendMessage(
            message.Chat.Id,
            "Send a video link from YouTube, Reddit, or Pinterest and I will download and send it here.",
            cancellationToken: cancellationToken);
        return;
    }

    var url = ExtractSupportedUrl(text);
    if (url is null)
    {
        await client.SendMessage(
            message.Chat.Id,
            "Please send a link from YouTube, Reddit, or Pinterest.",
            cancellationToken: cancellationToken);
        return;
    }

    await client.SendChatAction(
        message.Chat.Id,
        ChatAction.UploadVideo,
        cancellationToken: cancellationToken);
    var statusMessage = await client.SendMessage(
        message.Chat.Id,
        "Downloading the video...",
        cancellationToken: cancellationToken);

    try
    {
        await using var video = await downloader.DownloadAsync(url, cancellationToken);
        var inputFile = InputFile.FromStream(video.Stream, video.FileName);
        await client.SendVideo(
            message.Chat.Id,
            inputFile,
            caption: "Done",
            supportsStreaming: true,
            cancellationToken: cancellationToken);
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
        throw;
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine(exception);
        await client.SendMessage(
            message.Chat.Id,
            $"Could not download the video: {exception.Message}",
            cancellationToken: cancellationToken);
    }
    finally
    {
        await client.DeleteMessage(message.Chat.Id, statusMessage.Id, cancellationToken);
    }
}

Task HandlePollingErrorAsync(
    ITelegramBotClient _,
    Exception exception,
    CancellationToken cancellationToken)
{
    Console.Error.WriteLine($"Telegram polling error: {exception}");
    return Task.CompletedTask;
}

static string? ExtractSupportedUrl(string text)
{
    var match = Regex.Match(
        text,
        @"https?://[^\s<>()]+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    if (!match.Success)
        return null;

    var candidate = match.Value.TrimEnd('.', ',', '!', '?', ')', ']');
    if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri))
        return null;

    var host = uri.Host;
    return host.EndsWith("youtube.com", StringComparison.OrdinalIgnoreCase)
           || host.EndsWith("youtu.be", StringComparison.OrdinalIgnoreCase)
           || host.EndsWith("reddit.com", StringComparison.OrdinalIgnoreCase)
           || host.EndsWith("redd.it", StringComparison.OrdinalIgnoreCase)
           || host.EndsWith("pinterest.com", StringComparison.OrdinalIgnoreCase)
           || host.EndsWith("pin.it", StringComparison.OrdinalIgnoreCase)
        ? candidate
        : null;
}

sealed class YtDlpDownloader(string executable)
{
    public async Task<DownloadedVideo> DownloadAsync(string url, CancellationToken cancellationToken)
    {
        var directory = Directory.CreateTempSubdirectory("tgbot-");
        var outputTemplate = Path.Combine(directory.FullName, "%(id)s.%(ext)s");

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = executable,
                WorkingDirectory = directory.FullName,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add("--no-playlist");
            startInfo.ArgumentList.Add("--max-filesize");
            startInfo.ArgumentList.Add("49M");
            startInfo.ArgumentList.Add("--merge-output-format");
            startInfo.ArgumentList.Add("mp4");
            startInfo.ArgumentList.Add("-o");
            startInfo.ArgumentList.Add(outputTemplate);
            startInfo.ArgumentList.Add(url);

            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Could not start yt-dlp.");
            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            var error = await errorTask;

            if (process.ExitCode != 0)
                throw new InvalidOperationException(
                    string.IsNullOrWhiteSpace(error)
                        ? "yt-dlp exited with an error."
                        : error.Trim());

            var file = Directory.EnumerateFiles(directory.FullName)
                .FirstOrDefault(path => !path.EndsWith(".part", StringComparison.OrdinalIgnoreCase));
            if (file is null)
                throw new InvalidOperationException("The video was not found after downloading.");

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
}

sealed class DownloadedVideo(Stream stream, string fileName, DirectoryInfo directory) : IAsyncDisposable
{
    public Stream Stream { get; } = stream;
    public string FileName { get; } = fileName;

    public ValueTask DisposeAsync()
    {
        Stream.Dispose();
        directory.Delete(recursive: true);
        return ValueTask.CompletedTask;
    }
}