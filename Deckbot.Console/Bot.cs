using Reddit;
using Reddit.Controllers;
using Reddit.Controllers.EventArgs;
using Serilog;

namespace Deckbot.Console;

public static class Bot
{
    private static int commentsSeenCount;

    public static void Go()
    {
        var config = ConfigReader.GetConfig("./config/config.json");
        ReservationData = DataReader.GetReservationData();

        var client = new RedditClient(config.AppId, config.RefreshToken, config.AppSecret);

        BotName = client.Account.Me.Name;

        var subs = client.Account.MySubscribedSubreddits();

        foreach (var sub in subs)
        {
            ProcessSub(sub);
        }

        var myPosts = client.Account.Me.PostHistory;
        foreach (var post in myPosts)
        {
            ProcessPost(post);
        }
    }

    public static List<(string Model, string Region, int ReserveTime)> ReservationData { get; set; }
    private static string BotName { get; set; }

    private static void ProcessSub(Subreddit sub)
    {
        if (sub.Name.Equals("Announcements", StringComparison.CurrentCultureIgnoreCase)) return;

        WriteLine($"Flushing new comments in /r/{sub.Name}...");
        sub.Comments.GetNew();
        sub.Comments.NewUpdated += OnNewComment;
        WriteLine($"Monitoring new comments in /r/{sub.Name}...");
        sub.Comments.MonitorNew(monitoringBaseDelayMs: 5000);
    }

    private static void ProcessPost(Post post)
    {
        WriteLine($"Flushing new comments in my post {post.Title}...");
        post.Comments.GetNew();
        post.Comments.NewUpdated += OnNewComment;
        WriteLine($"Monitoring new comments in my post {post.Title}...");
        post.Comments.MonitorNew(monitoringBaseDelayMs: 5000);
    }

    private static void OnNewComment(object? sender, CommentsUpdateEventArgs e)
    {
        try
        {
            if (DataReader.NeedsUpdate)
            {
                ReservationData = DataReader.GetReservationData();
            }

            foreach (var comment in e.Added)
            {
                commentsSeenCount++;
                ParseComment(comment);

                if (commentsSeenCount % 100 == 0)
                {
                    WriteLine($"## Processed {commentsSeenCount} comments");
                }
            }
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"\nException: {ex}");
            Log.Error(ex, "Error processing comments");
        }
    }

    private static void ParseComment(Comment comment)
    {
        if (comment.Author.Equals(BotName, StringComparison.CurrentCultureIgnoreCase)) return;

        var command = new BotCommand();

        var (success, reply) = command.ProcessComment(comment);

        if (!success)
        {
            return;
        }

        WriteLine($"{comment.Author} @ {comment.Created}: {comment.Body.Substring(0, Math.Min(comment.Body.Length, 100))}");

        if (!string.IsNullOrWhiteSpace(reply))
        {
            //if (AlreadyReplied(comment)) return;

            WriteLine($"--> Replied: {reply}");
            // TODO
            comment.Reply(reply);
        }
    }

    private static bool AlreadyReplied(Comment comment)
    {
        var replies = comment.Comments.GetNew();

        if (replies.Count == 0) return false;

        return replies.Any(c => c.Author == BotName);
    }

    private static void WriteLine(string text)
    {
        System.Console.WriteLine($"\n{text}");
        Log.Information(text);
    }
}