namespace BloggerBazar.Infrastructure.Configuration;

public static class DotEnvConfiguration
{
    private static readonly (string Source, string Target)[] Aliases =
    [
        ("TELEGRAM_BOT_TOKEN", "Telegram__BotToken"),
        ("TELEGRAM_BOT_USERNAME", "Telegram__BotUsername"),
        ("TELEGRAM_MINIAPP_URL", "Telegram__MiniAppUrl"),
        ("TELEGRAM_WEBHOOK_SECRET", "Telegram__WebhookSecret")
    ];

    public static void Load()
    {
        var path = FindEnvironmentFile();
        if (path is not null)
        {
            foreach (var line in File.ReadLines(path))
            {
                LoadLine(line);
            }
        }

        foreach (var (source, target) in Aliases)
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(target)))
            {
                var value = Environment.GetEnvironmentVariable(source);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    Environment.SetEnvironmentVariable(target, value);
                }
            }
        }
    }

    private static string? FindEnvironmentFile()
    {
        for (var directory = new DirectoryInfo(Directory.GetCurrentDirectory()); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, ".env");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static void LoadLine(string line)
    {
        var value = line.Trim();
        if (string.IsNullOrWhiteSpace(value) || value.StartsWith('#'))
        {
            return;
        }

        if (value.StartsWith("export ", StringComparison.Ordinal))
        {
            value = value[7..].TrimStart();
        }

        var separatorIndex = value.IndexOf('=');
        if (separatorIndex <= 0)
        {
            return;
        }

        var key = value[..separatorIndex].Trim();
        var environmentValue = value[(separatorIndex + 1)..].Trim();
        if (environmentValue.Length >= 2 && environmentValue[0] == environmentValue[^1] && environmentValue[0] is '\"' or '\'')
        {
            environmentValue = environmentValue[1..^1];
        }

        if (!string.IsNullOrWhiteSpace(key) && Environment.GetEnvironmentVariable(key) is null)
        {
            Environment.SetEnvironmentVariable(key, environmentValue);
        }
    }
}
