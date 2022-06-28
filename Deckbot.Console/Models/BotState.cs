namespace Deckbot.Console.Models;

public class BotState
{
    public DateTime ReservationDataLastUpdated { get; set; } = DateTime.Now;
    public string? LastMessageReplyId { get; set; }
    public string? LastCommentReplyId { get; set; }
}