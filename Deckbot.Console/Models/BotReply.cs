namespace Deckbot.Console.Models;

public class BotReply
{
    public string? CommentId { get; set; }
    public string? Reply { get; set; }
    public string? OriginalAuthor { get; set; }
    public DateTime? ReplyTime { get; set; }
}