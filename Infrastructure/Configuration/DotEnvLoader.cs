namespace otw.fings.api.management.Infrastructure.Configuration;

internal static class DotEnvLoader
{
    internal static void LoadIfPresent(string? directory = null)
    {
        var path = Path.Combine(directory ?? Directory.GetCurrentDirectory(), ".env");
        if (!File.Exists(path)) return;

        foreach (var rawLine in File.ReadLines(path))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            if (line.StartsWith("export ", StringComparison.Ordinal)) line = line[7..].TrimStart();

            var separator = line.IndexOf('=');
            if (separator <= 0) continue;
            var key = line[..separator].Trim();
            if (!IsValidKey(key) || Environment.GetEnvironmentVariable(key) is not null) continue;

            var value = line[(separator + 1)..].Trim();
            if (value.Length >= 2 &&
                ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\'')))
            {
                value = value[1..^1];
            }
            Environment.SetEnvironmentVariable(key, value);
        }
    }

    private static bool IsValidKey(string key) =>
        key.Length > 0 &&
        (char.IsLetter(key[0]) || key[0] == '_') &&
        key.All(character => char.IsLetterOrDigit(character) || character == '_');
}
