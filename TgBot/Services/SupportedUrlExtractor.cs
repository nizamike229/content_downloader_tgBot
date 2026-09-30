using System.Text.RegularExpressions;

namespace TgBot.Services;

public static partial class SupportedUrlExtractor
{
    public static string? Extract(string text)
    {
        var match = UrlPattern().Match(text);
        if (!match.Success)
            return null;

        var candidate = match.Value.TrimEnd('.', ',', '!', '?', ')', ']');
        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri))
            return null;

        return IsSupportedHost(uri.Host) ? candidate : null;
    }

    private static bool IsSupportedHost(string host) =>
        host.EndsWith("youtube.com", StringComparison.OrdinalIgnoreCase)
        || host.EndsWith("youtu.be", StringComparison.OrdinalIgnoreCase)
        || host.EndsWith("reddit.com", StringComparison.OrdinalIgnoreCase)
        || host.EndsWith("redd.it", StringComparison.OrdinalIgnoreCase)
        || host.EndsWith("pinterest.com", StringComparison.OrdinalIgnoreCase)
        || host.EndsWith("pin.it", StringComparison.OrdinalIgnoreCase);

    [GeneratedRegex(@"https?://[^\s<>()]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex UrlPattern();
}
