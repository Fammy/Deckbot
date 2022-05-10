namespace Deckbot.Console.Models;

public class RedditConfig
{
    public string? AppId { get; set; }
    public string? AppSecret { get; set; }
    public string? RefreshToken { get; set; }

    public int RateLimitCooldown { get; set; }
    public bool MonitorSubreddit { get; set; }
    public bool MonitorBotUserPosts { get; set; }
    public bool MonitorBotPrivateMessages { get; set; }
    public string[]? PostsToMonitor { get; set; }
    public int ReplyCooldownMs { get; set; }
}