using Reddit;
using Reddit.Controllers;
using Reddit.Controllers.EventArgs;
using Reddit.Exceptions;
using Serilog;

namespace Deckbot.Console;

public static class Bot
{
    private static object _lock = new();
    private static int commentsSeenCount;
    private static Queue<BotReply> replyQueue = new();

    public static void Go()
    {
        var config = FileSystemOperations.GetConfig("./config/config.json");
        ReservationData = FileSystemOperations.GetReservationData();
        Client = new RedditClient(config.AppId, config.RefreshToken, config.AppSecret, userAgent: "bot:deck_bot:v0.1.2 (by /u/Fammy)");

        BotName = Client.Account.Me.Name;

        lock (_lock)
        {
            replyQueue = FileSystemOperations.GetReplyQueue();
        }

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
        sub.Comments.MonitorNew(monitoringBaseDelayMs: 15000);
    }

    private static void ProcessPost(Post post)
    {
        WriteLine($"Flushing new comments in my post {post.Title}...");
        post.Comments.GetNew();
        post.Comments.NewUpdated += OnNewComment;
        WriteLine($"Monitoring new comments in my post {post.Title}...");
        post.Comments.MonitorNew(monitoringBaseDelayMs: 15000);
    }

    private static void OnNewComment(object? sender, CommentsUpdateEventArgs e)
    {
        try
        {
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
            FileSystemOperations.WriteReplyQueue(replyQueue);

            var queueHasItems = replyQueue.Any();

            if (!queueHasItems)
            {
                return;
            }

            var queueSize = replyQueue.Count;

            WriteLine($"Comments in reply queue: {queueSize}");

            var lastRateLimited = DateTime.Now - RateLimitedTime;
            if (lastRateLimited < TimeSpan.FromSeconds(120))
            {
                WriteLine($"Skipping reply queue due to rate limit {lastRateLimited.TotalSeconds:F1} seconds ago...");
                return;
            }

            var processed = 0;

            while (replyQueue.Count > 0)
            {
                var reply = replyQueue.Peek();

                var comment = Client.Comment(reply.CommentId);

                try
                {
                    comment.Reply(reply.Reply);
                    //WriteLine($"--> Replied: {item.Reply}");

                    replyQueue.Dequeue();
                    processed++;
                }
                catch (RedditRateLimitException ex)
                {
                    RateLimitedTime = DateTime.Now;

                    System.Console.WriteLine(ex);
                    Log.Error(ex, $"Rate Limited, processed {processed}/{queueSize} comments in the reply queue");

                    FileSystemOperations.WriteReplyQueue(replyQueue);

                    return;
                }
                catch (RedditControllerException ex)
                {
                    replyQueue.Dequeue();

                    System.Console.WriteLine(ex);
                    Log.Error(ex, $"Controller exception, discarding comment. Processed {processed}/{queueSize} comments in the reply queue");
                }
            }

            WriteLine($"Made it through the queue, processed {processed}/{queueSize}");

            FileSystemOperations.WriteReplyQueue(replyQueue);
        }
    }

    private static void ParseComment(Comment comment)
    {
        if (string.IsNullOrWhiteSpace(comment.Body) || string.IsNullOrWhiteSpace(comment.Author)) return;

        if (comment.Author.Equals(BotName, StringComparison.CurrentCultureIgnoreCase)) return;

        if (FileSystemOperations.ReservationDataNeedsUpdate)
        {
            ReservationData = FileSystemOperations.GetReservationData();
        }

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
                replyQueue.Enqueue(new BotReply
                {
                    CommentId = comment.Fullname,
                    Reply = reply
                });
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