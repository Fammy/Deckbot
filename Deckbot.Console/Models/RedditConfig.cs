namespace Deckbot.Console.Models;

public class RedditConfig
{
    public string? AppId { get; set; }
    public string? AppSecret { get; set; }
    public string? RefreshToken { get; set; }

    public int MessageRateLimitCooldown { get; set; }
    public int CommentRateLimitCooldown { get; set; }
    public bool MonitorSubreddit { get; set; }
    public bool MonitorBotUserPosts { get; set; }
    public bool MonitorBotPrivateMessages { get; set; }
    public string[]? PostsToMonitor { get; set; }
    public int MessageReplyCooldownMs { get; set; }
    public int CommentReplyCooldownMs { get; set; }
}