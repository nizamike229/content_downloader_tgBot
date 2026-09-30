using TgBot.Bot;
using TgBot.Services;

var botToken = Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN");

if (string.IsNullOrWhiteSpace(botToken))
{
    Console.Error.WriteLine("Set TELEGRAM_BOT_TOKEN before starting the bot.");
    return;
}

var downloader = new YtDlpDownloader(
    Environment.GetEnvironmentVariable("YTDLP_PATH") ?? "yt-dlp");
var application = new TelegramBotApplication(botToken, downloader);

using var cancellationTokenSource = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellationTokenSource.Cancel();
};

await application.RunAsync(cancellationTokenSource.Token);
