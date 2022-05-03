using System.Text.Json;

namespace Deckbot.Console;

public static class ConfigReader
{
    public static RedditConfig GetConfig(string filename)
    {
        var json = File.ReadAllText(filename);
        return JsonSerializer.Deserialize<RedditConfig>(json);
    }
}