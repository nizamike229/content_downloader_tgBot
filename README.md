# Telegram Video Bot

The bot accepts video links from YouTube, Reddit, and Pinterest, downloads them
with `yt-dlp`, and sends them back to Telegram.

## Project structure

```text
TgBot/
├── Bot/       Telegram application lifecycle and update handling
├── Models/    Downloaded media models
├── Services/  URL extraction and video download services
└── Program.cs Application composition and startup
```

## Running

1. Install the [.NET 10 SDK](https://dotnet.microsoft.com/download), `yt-dlp`,
   and `ffmpeg`. `ffmpeg` is required to merge or convert downloads to MP4.
   On macOS:

   ```bash
   brew install yt-dlp ffmpeg
   ```

2. Create a bot using [@BotFather](https://t.me/BotFather) and set its token:

   ```bash
   export TELEGRAM_BOT_TOKEN="123456:replace-with-your-token"
   ```

3. Run the project:

   ```bash
   dotnet run --project TgBot/TgBot.csproj
   ```

If `yt-dlp` is not available in `PATH`, set its path using `YTDLP_PATH`.
Telegram limits the size of videos sent by bots; this project downloads files
up to 49 MB.
