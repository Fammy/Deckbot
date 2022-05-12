namespace Deckbot.Console.Models;

public class IncomingRequest
{
    public IncomingRequest(string author, string body, string messageId, RequestSource source)
    {
        Author = author;
        Body = body;
        MessageId = messageId;
        Source = source;
    }

    public string Author { get; set; }
    public string Body { get; set; }
    public string MessageId { get; set; }
    public bool IsAtValidLevel { get; set; }
    public RequestSource Source { get; set; }

}

public enum RequestSource
{
    PrivateMessage,
    Post
}