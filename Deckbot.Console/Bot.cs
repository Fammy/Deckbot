using Reddit;
using Reddit.Controllers;
using Reddit.Controllers.EventArgs;
using Reddit.Exceptions;
using Serilog;

namespace Deckbot.Console;

public static class Bot
{
    private static object _lock = new ();
    private static int commentsSeenCount;
    private static readonly Queue<(string CommentId, string Reply)> replyQueue = new();

    public static void Go()
    {
        var config = ConfigReader.GetConfig("./config/config.json");
        ReservationData = DataReader.GetReservationData();

        Client = new RedditClient(config.AppId, config.RefreshToken, config.AppSecret, userAgent: "bot:deck_bot:v0.1.0 (by /u/Fammy)");

        BotName = Client.Account.Me.Name;

        var subs = Client.Account.MySubscribedSubreddits();

#if !DEBUG
        foreach (var sub in subs)
        {
            ProcessSub(sub);
        }
#endif

        var myPosts = Client.Account.Me.PostHistory;
        foreach (var post in myPosts)
        {
            ProcessPost(post);
        }
    }

    private static RedditClient Client { get; set; }
    private static DateTime RateLimitedTime { get; set; } = DateTime.Now - TimeSpan.FromSeconds(120);
    private static string BotName { get; set; }

    public static List<(string Model, string Region, int ReserveTime)> ReservationData { get; private set; }

    private static void ProcessSub(Subreddit sub)
    {
        if (sub.Name.Equals("Announcements", StringComparison.CurrentCultureIgnoreCase)) return;

        WriteLine($"Flushing new comments in /r/{sub.Name}...");
        sub.Comments.GetNew();
        sub.Comments.NewUpdated += OnNewComment;
        WriteLine($"Monitoring new comments in /r/{sub.Name}...");
        sub.Comments.MonitorNew(monitoringBaseDelayMs: 1500);
    }

    private static void ProcessPost(Post post)
    {
        WriteLine($"Flushing new comments in my post {post.Title}...");
        post.Comments.GetNew();
        post.Comments.NewUpdated += OnNewComment;
        WriteLine($"Monitoring new comments in my post {post.Title}...");
        post.Comments.MonitorNew(monitoringBaseDelayMs: 1500);
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
                    WriteLine($"Reviewed {commentsSeenCount} comments");
                }
            }

            ProcessReplyQueue();
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"\nException: {ex}");
            Log.Error(ex, "Error processing comments");
        }
    }

    private static void ProcessReplyQueue()
    {
        lock (_lock)
        {
            var queueHasItems = replyQueue.Any();

            if (!queueHasItems)
            {
                return;
            }

            WriteLine($"Comments in reply queue: {replyQueue.Count}");

            var lastRateLimited = DateTime.Now - RateLimitedTime;
            if (lastRateLimited < TimeSpan.FromSeconds(120))
            {
                WriteLine($"Skipping reply queue due to rate limit {lastRateLimited.TotalSeconds:F1} seconds ago...");
                return;
            }

            var processed = 0;

            while (replyQueue.Count > 0)
            {
                var item = replyQueue.Peek();

                var comment = Client.Comment(item.CommentId);
                try
                {
                    comment.Reply(item.Reply);
                    //WriteLine($"--> Replied: {item.Reply}");

                    replyQueue.Dequeue();
                    processed++;
                }
                catch (RedditRateLimitException ex)
                {
                    RateLimitedTime = DateTime.Now;

                    System.Console.WriteLine(ex);
                    Log.Error(ex, $"Rate Limited, processed {processed}/{replyQueue.Count} comments in the reply queue");

                    return;
                }
                catch (RedditControllerException ex)
                {
                    replyQueue.Dequeue();

                    System.Console.WriteLine(ex);
                    Log.Error(ex, $"Controller exception, discarding comment. Processed {processed}/{replyQueue.Count} comments in the reply queue");

                    return;
                }
            }

            WriteLine($"Made it through the queue, processed {processed}/{replyQueue.Count}");
        }
    }

    private static void ParseComment(Comment comment)
    {
        if (string.IsNullOrWhiteSpace(comment.Body) || string.IsNullOrWhiteSpace(comment.Author)) return;

        if (comment.Author.Equals(BotName, StringComparison.CurrentCultureIgnoreCase)) return;

        var command = new BotCommand();

        var (success, reply) = command.ProcessComment(comment);

        if (!success)
        {
            return;
        }

        WriteLine($"/u/{comment.Author}: {comment.Body.Substring(0, Math.Min(comment.Body.Length, 100))}");

        if (!string.IsNullOrWhiteSpace(reply))
        {
            // TODO: not working
            //if (AlreadyReplied(comment)) return;

            //comment.Reply(reply);
            //WriteLine($"--> Replied: {reply}");
            lock (_lock)
            {
                replyQueue.Enqueue((comment.Fullname, reply));
            }
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