namespace Deckbot.Console;

public class IncomingRequest
{
    public IncomingRequest(string author, string body, string messageId)
    {
        Author = author;
        Body = body;
        MessageId = messageId;
    }

    public string Author { get; set; }
    public string Body { get; set; }
    public string MessageId { get; set; }
}