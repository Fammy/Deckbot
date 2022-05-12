using Deckbot.Console.Models;
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
    private static List<string> ValidPostsIds { get; set; } = new();

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
        Client = new RedditClient(Config.AppId, Config.RefreshToken, Config.AppSecret, userAgent: "bot:deck_bot:v0.4.4 (by /u/Fammy)");
        RateLimitedTime = DateTime.Now - TimeSpan.FromSeconds(Config.RateLimitCooldown);
        BotName = Client.Account.Me.Name;

        if (Config.PostsToMonitor != null)
        {
            ValidPostsIds.AddRange(Config.PostsToMonitor);
        }

        lock (_lock)
        {
            replyQueue = FileSystemOperations.GetReplyQueue();
            WriteLine($"Restored {replyQueue.Count} replies from disk...");
        }

        if (Config.MonitorSubreddit)
        {
            var subs = Client.Account.MySubscribedSubreddits();

            foreach (var sub in subs)
            {
                MonitorSub(sub);
            }
        }
        else
        {
            if (Config.PostsToMonitor != null)
            {
                foreach (var postId in Config.PostsToMonitor)
                {
                    MonitorPost(Client.Post($"t3_{postId}"));
                }
            }
        }

        if (Config.MonitorBotUserPosts)
        {
            var myPosts = Client.Account.Me.PostHistory;
            foreach (var post in myPosts)
            {
                ValidPostsIds.Add(post.Id);
                MonitorPost(post);
            }
        }

        if (Config.MonitorBotPrivateMessages)
        {
            MonitorPrivateMessages(Client.Account);
        }

        ProcessReplyQueue();
    }

    private static void MonitorSub(Subreddit sub)
    {
        if (sub.Name.Equals("Announcements", StringComparison.CurrentCultureIgnoreCase)) return;

        WriteLine($"Flushing new comments in /r/{sub.Name}...");
        sub.Comments.GetNew();
        sub.Comments.NewUpdated += OnNewComment;
        WriteLine($"Monitoring new comments in /r/{sub.Name}...");
        sub.Comments.MonitorNew(monitoringBaseDelayMs: 15000);
    }

    private static void MonitorPost(Post post)
    {
        WriteLine($"Flushing new comments in post {post.Title ?? post.Fullname}...");
        post.Comments.GetNew();
        post.Comments.NewUpdated += OnNewComment;
        WriteLine($"Monitoring new comments in post {post.Title ?? post.Fullname}...");
        post.Comments.MonitorNew(monitoringBaseDelayMs: 15000);
    }

    private static void MonitorPrivateMessages(Account account)
    {
        WriteLine($"Flushing new private messages...");
        account.Messages.GetMessagesUnread();
        account.Messages.UnreadUpdated += MessagesUpdated;
        WriteLine($"Monitoring new private messages...");
        account.Messages.MonitorUnread(monitoringBaseDelayMs: 15000);
    }

    private static void MessagesUpdated(object? sender, MessagesUpdateEventArgs e)
    {
        try
        {
            foreach (var message in e.Added)
            {
                commentsSeenCount++;

                var request = new IncomingRequest(message.Author, message.Body, message.Fullname)
                {
                    IsAtValidLevel = true
                };

#if DEBUG
                WriteLine($"PM from /u/{request.Author}: {message.Body.Substring(0, Math.Min(25, request.Body.Length))}");
#endif
                ParseIncomingRequest(request);

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
            Log.Error(ex, "Error processing private messages");
        }
    }

    private static void OnNewComment(object? sender, CommentsUpdateEventArgs e)
    {
        try
        {
            foreach (var comment in e.Added)
            {
                commentsSeenCount++;

                var request = new IncomingRequest(comment.Author, comment.Body, comment.Fullname);

                if (CommentIsInAuthorizedPost(comment.Permalink))
                {
#if DEBUG
                    WriteLine($"Post from /u/{request.Author}: {comment.Body.Substring(0, Math.Min(25, request.Body.Length))}");
#endif
                    request.IsAtValidLevel = CommentIsAtAllowedLevel(comment.ParentId);

                    ParseIncomingRequest(request);
                }

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

    private static bool CommentIsAtAllowedLevel(string parentId)
    {
        // Introducing Deckbot
        if (parentId.Equals("ui642q", StringComparison.CurrentCultureIgnoreCase))
        {
            return true;
        }

        return !ValidPostsIds.Any(postId => postId.Equals(parentId, StringComparison.CurrentCultureIgnoreCase));
    }

    private static bool CommentIsInAuthorizedPost(string permalink)
    {
        if (ValidPostsIds.Count == 0)
        {
            return false;
        }

        return ValidPostsIds.Any(postId => permalink.Contains($"/{postId}/"));
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

                try
                {
                    var comment = Client.Comment(reply.CommentId);
                    comment.Reply(reply.Reply);

                    replyQueue.Dequeue();
                    processed++;

                    Thread.Sleep(Config.ReplyCooldownMs);
                }
                catch (RedditRateLimitException ex)
                {
                    RateLimitedTime = DateTime.Now;

                    var behindMessage = string.Empty;
                    if (reply.ReplyTime.HasValue)
                    {
                        var behindTime = DateTime.Now - reply.ReplyTime.Value;
                        behindMessage = $". Deckbot is {behindTime.Hours:D2}:{behindTime.Minutes:D2}:{behindTime.Seconds:D2} behind";
                    }

                    System.Console.WriteLine(ex);
                    Log.Error(ex, $"Rate Limited, processed {processed}/{queueSize} comments in the reply queue{behindMessage}");

                    FileSystemOperations.WriteReplyQueue(replyQueue);

                    return;
                }
                catch (RedditControllerException ex)
                {
                    FileSystemOperations.WriteException("controller_exception", reply, ex);

                    replyQueue.Dequeue();

                    System.Console.WriteLine(ex);
                    Log.Error(ex, $"Controller exception, discarding comment. Processed {processed}/{queueSize} comments in the reply queue");
                }
                catch (RedditForbiddenException ex)
                {
                    FileSystemOperations.WriteException("forbidden_exception", reply, ex);

                    replyQueue.Dequeue();

                    System.Console.WriteLine(ex);
                    Log.Error(ex, $"Forbidden exception, discarding comment. Processed {processed}/{queueSize} comments in the reply queue");
                }
            }

            WriteLine($"Made it through the queue, processed {processed}/{queueSize}");

            FileSystemOperations.WriteReplyQueue(replyQueue);
        }
    }

    private static void ParseIncomingRequest(IncomingRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Body) || string.IsNullOrWhiteSpace(request.Author)) return;

        if (request.Author.Equals(BotName, StringComparison.CurrentCultureIgnoreCase)) return;

        if (FileSystemOperations.ReservationDataNeedsUpdate)
        {
            ReservationData = FileSystemOperations.GetReservationData();
        }

        var command = new BotCommand();

        var (success, reply) = command.ProcessComment(request);

        if (!success)
        {
            return;
        }

#if !DEBUG
        WriteLine($"/u/{request.Author}: {request.Body.Substring(0, Math.Min(request.Body.Length, 100))}");
#endif

        if (!string.IsNullOrWhiteSpace(reply))
        {
            // TODO: don't process if already replied, which may consume too many API calls

            lock (_lock)
            {
                replyQueue.Enqueue(new BotReply
                {
                    CommentId = request.MessageId,
                    Reply = reply,
                    ReplyTime = DateTime.Now
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