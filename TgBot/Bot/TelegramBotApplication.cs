using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;
using TgBot.Services;

namespace TgBot.Bot;

public sealed class TelegramBotApplication(string botToken, IVideoDownloader downloader)
{
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var bot = new TelegramBotClient(botToken);
        var updateHandler = new TelegramUpdateHandler(downloader);
        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = [UpdateType.Message, UpdateType.CallbackQuery]
        };

        bot.StartReceiving(
            updateHandler.HandleUpdateAsync,
            updateHandler.HandlePollingErrorAsync,
            receiverOptions,
            cancellationToken);

        var me = await bot.GetMe(cancellationToken);
        Console.WriteLine($"@{me.Username} is running. Press Ctrl+C to stop.");

        try
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }
}
