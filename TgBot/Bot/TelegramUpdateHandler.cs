using System.Collections.Concurrent;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Bot.Types.Enums;
using TgBot.Services;

namespace TgBot.Bot;

public sealed class TelegramUpdateHandler(IVideoDownloader downloader)
{
    private static readonly ConcurrentDictionary<string, string> PendingYouTubeUrls = new();

    public async Task HandleUpdateAsync(
        ITelegramBotClient client,
        Update update,
        CancellationToken cancellationToken)
    {
        if (update.CallbackQuery is { } callbackQuery)
        {
            await HandleCallbackQueryAsync(client, callbackQuery, cancellationToken);
            return;
        }

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

        if (SupportedUrlExtractor.IsYouTube(url))
        {
            var token = Guid.NewGuid().ToString("N");
            PendingYouTubeUrls[token] = url;
            await client.SendMessage(
                message.Chat.Id,
                "🎞️ What would you like to download?",
                replyMarkup: new InlineKeyboardMarkup(
                [
                    [
                        InlineKeyboardButton.WithCallbackData("🎬 Video", $"video:{token}"),
                        InlineKeyboardButton.WithCallbackData("🎵 Audio", $"audio:{token}")
                    ]
                ]),
                cancellationToken: cancellationToken);
            return;
        }

        await DownloadAndSendVideoAsync(client, message.Chat.Id, url, cancellationToken);
    }

    private async Task HandleCallbackQueryAsync(
        ITelegramBotClient client,
        CallbackQuery callbackQuery,
        CancellationToken cancellationToken)
    {
        await client.AnswerCallbackQuery(callbackQuery.Id, cancellationToken: cancellationToken);

        if (callbackQuery.Message is not { } message
            || callbackQuery.Data is not { } data)
            return;

        var separator = data.IndexOf(':');
        if (separator <= 0
            || separator == data.Length - 1
            || !PendingYouTubeUrls.TryRemove(data[(separator + 1)..], out var url))
        {
            await client.SendMessage(
                message.Chat.Id,
                "⌛ This download request has expired. Please send the link again.",
                cancellationToken: cancellationToken);
            return;
        }

        var isAudio = data.StartsWith("audio:", StringComparison.Ordinal);
        if (isAudio)
        {
            await DownloadAndSendAudioAsync(client, message.Chat.Id, url, cancellationToken);
            return;
        }

        await DownloadAndSendVideoAsync(client, message.Chat.Id, url, cancellationToken);
    }

    private async Task DownloadAndSendVideoAsync(
        ITelegramBotClient client,
        long chatId,
        string url,
        CancellationToken cancellationToken)
    {
        await client.SendChatAction(
            chatId,
            ChatAction.UploadVideo,
            cancellationToken: cancellationToken);
        var statusMessage = await client.SendMessage(
            chatId,
            "⏳ Downloading the video...",
            cancellationToken: cancellationToken);

        try
        {
            await using var video = await downloader.DownloadAsync(url, cancellationToken);
            await client.SendVideo(
                chatId,
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
                chatId,
                $"😕 Could not download the video: {exception.Message}",
                cancellationToken: cancellationToken);
        }
        finally
        {
            await client.DeleteMessage(chatId, statusMessage.Id, cancellationToken);
        }
    }

    private async Task DownloadAndSendAudioAsync(
        ITelegramBotClient client,
        long chatId,
        string url,
        CancellationToken cancellationToken)
    {
        await client.SendChatAction(
            chatId,
            ChatAction.UploadVoice,
            cancellationToken: cancellationToken);
        var statusMessage = await client.SendMessage(
            chatId,
            "⏳ Extracting the highest-quality audio...",
            cancellationToken: cancellationToken);

        try
        {
            await using var audio = await downloader.DownloadAudioAsync(url, cancellationToken);
            await client.SendAudio(
                chatId,
                InputFile.FromStream(audio.Stream, audio.FileName),
                caption: "✅ Done!",
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
                chatId,
                $"😕 Could not extract the audio: {exception.Message}",
                cancellationToken: cancellationToken);
        }
        finally
        {
            await client.DeleteMessage(chatId, statusMessage.Id, cancellationToken);
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
