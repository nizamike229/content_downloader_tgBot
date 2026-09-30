using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TgBot.Services;

namespace TgBot.Bot;

public sealed class TelegramUpdateHandler(IVideoDownloader downloader)
{
    public async Task HandleUpdateAsync(
        ITelegramBotClient client,
        Update update,
        CancellationToken cancellationToken)
    {
        if (update.Message is not { Text: { } text } message)
            return;

        if (text.Equals("/start", StringComparison.OrdinalIgnoreCase))
        {
            await client.SendMessage(
                message.Chat.Id,
                "👋 Send a video link from YouTube, Reddit, or Pinterest and I will download and send it here! 🎬",
                cancellationToken: cancellationToken);
            return;
        }

        var url = SupportedUrlExtractor.Extract(text);
        if (url is null)
        {
            await client.SendMessage(
                message.Chat.Id,
                "🔗 Please send a link from YouTube, Reddit, or Pinterest.",
                cancellationToken: cancellationToken);
            return;
        }

        await client.SendChatAction(
            message.Chat.Id,
            ChatAction.UploadVideo,
            cancellationToken: cancellationToken);
        var statusMessage = await client.SendMessage(
            message.Chat.Id,
            "⏳ Downloading the video...",
            cancellationToken: cancellationToken);

        try
        {
            await using var video = await downloader.DownloadAsync(url, cancellationToken);
            await client.SendVideo(
                message.Chat.Id,
                InputFile.FromStream(video.Stream, video.FileName),
                caption: "✅ Done!",
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
                $"😕 Could not download the video: {exception.Message}",
                cancellationToken: cancellationToken);
        }
        finally
        {
            await client.DeleteMessage(message.Chat.Id, statusMessage.Id, cancellationToken);
        }
    }

    public Task HandlePollingErrorAsync(
        ITelegramBotClient _,
        Exception exception,
        CancellationToken cancellationToken)
    {
        Console.Error.WriteLine($"Telegram polling error: {exception}");
        return Task.CompletedTask;
    }
}
