using Reddit;
using Reddit.Controllers;
using Reddit.Controllers.EventArgs;
using Reddit.Exceptions;
using Serilog;

namespace Deckbot.Console;

public static class Bot
{
    private static readonly object _lock = new();
    private static int commentsSeenCount;
    private static Queue<BotReply> replyQueue = new();

    private static RedditConfig? Config { get; set; }
    private static RedditClient? Client { get; set; }
    private static DateTime RateLimitedTime { get; set; }
    private static string? BotName { get; set; }

    public static List<(string Model, string Region, int ReserveTime)>? ReservationData { get; private set; }

    public static void Go()
    {
        Config = FileSystemOperations.GetConfig("./config/config.json");

        if (Config == null)
        {
            WriteLine("Cannot read './config/config.json'");
            return;
        }

        ReservationData = FileSystemOperations.GetReservationData();
        Client = new RedditClient(Config.AppId, Config.RefreshToken, Config.AppSecret, userAgent: "bot:deck_bot:v0.3.0 (by /u/Fammy)");
        RateLimitedTime = DateTime.Now - TimeSpan.FromSeconds(Config.RateLimitCooldown);

        BotName = Client.Account.Me.Name;

        lock (_lock)
        {
            replyQueue = FileSystemOperations.GetReplyQueue();
            WriteLine($"Restored {replyQueue.Count} replies from disk...");
        }

#if !DEBUG
        if (Config.MonitorSubreddit)
        {
            var subs = Client.Account.MySubscribedSubreddits();

            foreach (var sub in subs)
            {
                ProcessSub(sub);
            }
        }
#endif

        if (Config.MonitorBotUserPosts)
        {
            var myPosts = Client.Account.Me.PostHistory;
            foreach (var post in myPosts)
            {
                ProcessPost(post);
            }
        }

        if (Config.PostsToMonitor != null)
        {
            foreach (var postId in Config.PostsToMonitor)
            {
                ProcessPost(Client.Post($"t3_{postId}"));
            }
        }
    }

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
        WriteLine($"Flushing new comments in my post {post.Title ?? post.Fullname}...");
        post.Comments.GetNew();
        post.Comments.NewUpdated += OnNewComment;
        WriteLine($"Monitoring new comments in my post {post.Title ?? post.Fullname}...");
        post.Comments.MonitorNew(monitoringBaseDelayMs: 15000);
    }

    private static void ProcessComment(Comment comment)
    {
        WriteLine($"Flushing new comments in comment {comment.Fullname}...");
        comment.Comments.GetNew();
        comment.Comments.NewUpdated += OnNewComment;
        WriteLine($"Monitoring new comments in comment {comment.Fullname}...");
        comment.Comments.MonitorNew(monitoringBaseDelayMs: 15000);
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
        if (Config == null || Client == null)
        {
            return;
        }

        lock (_lock)
        {
            FileSystemOperations.WriteReplyQueue(replyQueue);

            var queueHasItems = replyQueue.Any();

            if (!queueHasItems)
            {
                return;
            }

            var queueSize = replyQueue.Count;

            var lastRateLimited = DateTime.Now - RateLimitedTime;
            if (lastRateLimited < TimeSpan.FromSeconds(Config.RateLimitCooldown))
            {
                WriteLine($"Skipping reply queue due to rate limit {lastRateLimited.TotalSeconds:F1}s ago. Queue size is {queueSize}");
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

                    replyQueue.Dequeue();
                    processed++;

                    Thread.Sleep(Config.ReplyCooldownMs);
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
            // TODO: don't process if already replied, which may consume too many API calls

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

    private static void WriteLine(string text)
    {
        System.Console.WriteLine($"\n{text}");
        Log.Information(text);
    }
}