using System.Collections;
using System.Text;
using System.Text.RegularExpressions;
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
    private static Queue<BotReply> commentReplyQueue = new();
    private static Queue<BotReply> messageReplyQueue = new();

    private static RedditConfig? Config { get; set; }
    private static RedditClient? Client { get; set; }
    private static DateTime CommentRateLimitedTime { get; set; }
    private static DateTime MessageRateLimitedTime { get; set; }
    private static string? BotName { get; set; }
    private static List<string> ValidPostIds { get; set; } = new();
    private static List<string> BotPostIds { get; set; } = new();
    private static int OverrideCommentRateLimitCooldown { get; set; }
    private static int OverrideMessageRateLimitCooldown { get; set; }

    public static List<(string Model, string Region, int ReserveTime)>? ReservationData { get; private set; }

    public static void Go()
    {
        Config = FileSystemOperations.GetConfig();

        if (Config == null)
        {
            WriteLine("Cannot read './config/config.json'");
            return;
        }

        ReloadReservationData();
        Client = new RedditClient(Config.AppId, Config.RefreshToken, Config.AppSecret, userAgent: "bot:deck_bot:v0.4.6 (by /u/Fammy)");
        CommentRateLimitedTime = DateTime.Now - TimeSpan.FromSeconds(Config.CommentRateLimitCooldown);
        MessageRateLimitedTime = DateTime.Now - TimeSpan.FromSeconds(Config.MessageRateLimitCooldown);
        BotName = Client.Account.Me.Name;

        UpdatePostsToMonitor();

        lock (_lock)
        {
            commentReplyQueue = FileSystemOperations.GetReplyQueue(RequestSource.Post);
            WriteLine($"Restored {commentReplyQueue.Count} comment replies from disk...");
            messageReplyQueue = FileSystemOperations.GetReplyQueue(RequestSource.PrivateMessage);
            WriteLine($"Restored {messageReplyQueue.Count} message replies from disk...");
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
                BotPostIds.Add(post.Id);
                ValidPostIds.Add(post.Id);
                MonitorPost(post);
            }
        }

        if (Config.MonitorBotPrivateMessages)
        {
            MonitorPrivateMessages(Client.Account);
        }

        ProcessReplyQueues();
    }

    private static void UpdatePostsToMonitor()
    {
        if (Config?.PostsToMonitor != null)
        {
            lock (_lock)
            {
                ValidPostIds.Clear();
                ValidPostIds.AddRange(Config.PostsToMonitor);
                ValidPostIds.AddRange(BotPostIds);
            }
        }
    }

    private static void MonitorSub(Subreddit sub)
    {
        if (sub.Name.Equals("Announcements", StringComparison.CurrentCultureIgnoreCase)) return;

        WriteLine($"Flushing new comments in /r/{sub.Name}...");
        sub.Comments.GetNew();
        sub.Comments.NewUpdated += async (sender, args) => await OnNewComment(sender, args);
        WriteLine($"Monitoring new comments in /r/{sub.Name}...");
        sub.Comments.MonitorNew(monitoringBaseDelayMs: 15000);
    }

    private static void MonitorPost(Post post)
    {
        WriteLine($"Flushing new comments in post {post.Title ?? post.Fullname}...");
        post.Comments.GetNew();
        post.Comments.NewUpdated += async (sender, args) => await OnNewComment(sender, args);
        WriteLine($"Monitoring new comments in post {post.Title ?? post.Fullname}...");
        post.Comments.MonitorNew(monitoringBaseDelayMs: 15000);
    }

    private static void MonitorPrivateMessages(Account account)
    {
        WriteLine($"Flushing new private messages...");
        account.Messages.GetMessagesUnread();
        account.Messages.UnreadUpdated += async (sender, args) => await MessagesUpdated(sender, args);
        WriteLine($"Monitoring new private messages...");
        account.Messages.MonitorUnread(monitoringBaseDelayMs: 15000);
    }

    private static async Task MessagesUpdated(object? sender, MessagesUpdateEventArgs e)
    {
        try
        {
            foreach (var message in e.Added)
            {
                commentsSeenCount++;

                var request = new IncomingRequest(message.Author, message.Body, message.Fullname, RequestSource.PrivateMessage)
                {
                    IsAtValidLevel = true
                };

#if DEBUG
                WriteLine($"PM from /u/{request.Author}: {message.Body.Substring(0, Math.Min(25, request.Body.Length))}");
#endif
                await ParseIncomingRequest(request);

                if (commentsSeenCount % 100 == 0)
                {
                    WriteLine($"Reviewed {commentsSeenCount} comments");
                }
            }

            ProcessReplyQueues();
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"\nException: {ex}");
            Log.Error(ex, "Error processing private messages");
            LogExceptionData(ex);
        }
    }

    private static async Task OnNewComment(object? sender, CommentsUpdateEventArgs e)
    {
        try
        {
            foreach (var comment in e.Added)
            {
                commentsSeenCount++;

                var request = new IncomingRequest(comment.Author, comment.Body, comment.Fullname, RequestSource.Post);

                if (CommentIsInAuthorizedPost(comment.Permalink))
                {
#if DEBUG
                    WriteLine($"Comment from /u/{request.Author}: {comment.Body.Substring(0, Math.Min(25, request.Body.Length))}");
#endif
                    request.IsAtValidLevel = CommentIsAtAllowedLevel(comment.ParentId);

                    await ParseIncomingRequest(request);
                }

                if (commentsSeenCount % 100 == 0)
                {
                    WriteLine($"Reviewed {commentsSeenCount} comments");
                }
            }

            ProcessReplyQueues();
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"\nException: {ex}");
            Log.Error(ex, "Error processing comments");
            LogExceptionData(ex);
        }
    }

    private static bool CommentIsAtAllowedLevel(string parentId)
    {
        // Introducing Deckbot
        if (parentId.Equals("ui642q", StringComparison.CurrentCultureIgnoreCase))
        {
            return true;
        }

        return !ValidPostIds.Any(postId => postId.Equals(parentId, StringComparison.CurrentCultureIgnoreCase));
    }

    private static bool CommentIsInAuthorizedPost(string permalink)
    {
        if (ValidPostIds.Count == 0)
        {
            return false;
        }

        return ValidPostIds.Any(postId => permalink.Contains($"/{postId}/"));
    }

    private static void ProcessReplyQueues()
    {
        lock (_lock)
        {
            FileSystemOperations.WriteReplyQueue(messageReplyQueue, RequestSource.PrivateMessage);
            FileSystemOperations.WriteReplyQueue(commentReplyQueue, RequestSource.Post);

            ProcessReplyQueue(commentReplyQueue, RequestSource.Post);
            ProcessReplyQueue(messageReplyQueue, RequestSource.PrivateMessage);
        }
    }

    private static void ProcessReplyQueue(Queue<BotReply> replyQueue, RequestSource source)
    {
        if (Config == null || Client == null)
        {
            return;
        }

        var queueHasItems = replyQueue.Any();

        if (!queueHasItems)
        {
            return;
        }

        var queueSize = replyQueue.Count;

        var rateLimitedTime = source == RequestSource.PrivateMessage ? MessageRateLimitedTime : CommentRateLimitedTime;
        var queueName = source == RequestSource.PrivateMessage ? "message" : "comment";

        var lastRateLimited = DateTime.Now - rateLimitedTime;
        int rateLimitCooldown;
        
        if (source == RequestSource.PrivateMessage)
        {
            rateLimitCooldown = OverrideMessageRateLimitCooldown > 0 ? OverrideMessageRateLimitCooldown : Config.MessageRateLimitCooldown;
        }
        else
        {
            rateLimitCooldown = OverrideCommentRateLimitCooldown > 0 ? OverrideCommentRateLimitCooldown : Config.CommentRateLimitCooldown;
        }

        if (lastRateLimited < TimeSpan.FromSeconds(rateLimitCooldown))
        {
            WriteLine($"Skipping {queueName} reply queue due to rate limit {lastRateLimited.TotalSeconds:F1}s ago. Queue size is {queueSize}, cooldown is {rateLimitCooldown}");
            return;
        }

        OverrideMessageRateLimitCooldown = 0;
        OverrideCommentRateLimitCooldown = 0;

        if (replyQueue.Count >= 10)
        {
            WriteLine($"Starting processing of {replyQueue.Count} replies in the {queueName} reply queue.");
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

                var replyCooldown = source == RequestSource.PrivateMessage ? Config.MessageReplyCooldownMs : Config.CommentReplyCooldownMs;

                // Throttle replies if queue has more than 1 item and we aren't on the last item
                if (queueSize > 1 && replyQueue.Count > 0)
                {
                    Thread.Sleep(replyCooldown);
                }
            }
            catch (RedditRateLimitException ex)
            {
                if (source == RequestSource.PrivateMessage)
                {
                    MessageRateLimitedTime = DateTime.Now;
                }
                else
                {
                    CommentRateLimitedTime = DateTime.Now;
                }

                var behindMessage = string.Empty;
                if (reply.ReplyTime.HasValue)
                {
                    var behindTime = DateTime.Now - reply.ReplyTime.Value;
                    behindMessage = $". Deckbot is {behindTime.Hours:D2}:{behindTime.Minutes:D2}:{behindTime.Seconds:D2} behind";
                }
                var errors = new List<string>();

                foreach (DictionaryEntry entry in ex.Data)
                {
                    if (entry.Value is List<List<string>> nestedList)
                    {
                        errors.AddRange(nestedList.SelectMany(l => l));
                    }
                    else if (entry.Value != null)
                    {
                        errors.Add(entry.Value?.ToString() ?? string.Empty);
                    }
                }

                var breakMessages = errors.Where(e => e.Contains("Take a break")).ToList();

                OverrideCommentRateLimitCooldown = 0;
                OverrideMessageRateLimitCooldown = 0;

                if (breakMessages.Any())
                {
                    var match = new Regex(@"[0-9]+").Match(breakMessages[0]);

                    if (int.TryParse(match.Value, out var wait))
                    {
                        if (breakMessages[0].Contains("second"))
                        {
                            wait += 2;
                        }
                        else if (breakMessages[0].Contains("minute"))
                        {
                            wait = (wait * 60) + 30;
                        }
                        else if (breakMessages[0].Contains("hour"))
                        {
                            wait = (wait * 3600);
                        }

                        if (source == RequestSource.PrivateMessage)
                        {
                            OverrideMessageRateLimitCooldown = wait;
                        }
                        else
                        {
                            OverrideCommentRateLimitCooldown = wait;
                        }

                        WriteLine($"Rate limited with message {breakMessages[0]}, adjusting cooldown to {wait} seconds");
                    }
                }

                System.Console.WriteLine(ex);
                Log.Error(ex, $"Rate limited, processed {processed}/{queueSize} replies in the {queueName} reply queue{behindMessage}");
                LogExceptionData(ex);

                FileSystemOperations.WriteReplyQueue(replyQueue, source);

                return;
            }
            catch (RedditControllerException ex)
            {
                FileSystemOperations.WriteException("controller_exception", reply, ex);

                replyQueue.Dequeue();

                System.Console.WriteLine(ex);
                Log.Error(ex, $"Controller exception, discarding reply. Processed {processed}/{queueSize} replies in the {queueName} reply queue");
                LogExceptionData(ex);
            }
            catch (RedditForbiddenException ex)
            {
                FileSystemOperations.WriteException("forbidden_exception", reply, ex);

                replyQueue.Dequeue();

                System.Console.WriteLine(ex);
                Log.Error(ex, $"Forbidden exception, discarding reply to {reply.OriginalAuthor}. Processed {processed}/{queueSize} replies in the {queueName} reply queue");
                LogExceptionData(ex);
            }
        }

        WriteLine($"Made it through the {queueName} queue, processed {processed}/{queueSize}");

        FileSystemOperations.WriteReplyQueue(replyQueue, source);
    }

    private static void LogExceptionData(Exception ex)
    {
        try
        {
            var sb = new StringBuilder();

            foreach (DictionaryEntry entry in ex.Data)
            {
                if (entry.Value is List<List<string>> nestedList)
                {
                    foreach (var s in nestedList.SelectMany(l => l))
                    {
                        sb.AppendLine($"{entry.Key} = {s}");
                    }
                }
                else
                {
                    sb.AppendLine($"{entry.Key} = {entry.Value}");
                }
            }

            if (sb.Length > 0)
            {
                Log.Error($"\nData: {sb}");
            }
        }
        catch { }
    }

    private static async Task ParseIncomingRequest(IncomingRequest request)
    {
        await DoPeriodicWork();

        if (string.IsNullOrWhiteSpace(request.Body) || string.IsNullOrWhiteSpace(request.Author)) return;

        if (request.Author.Equals(BotName, StringComparison.CurrentCultureIgnoreCase)) return;

        var command = new BotCommand();

        var (success, reply) = command.ProcessComment(request);

        if (!success)
        {
            return;
        }

#if !DEBUG
        var source = request.Source == RequestSource.PrivateMessage ? "PM" : "Comment";
        WriteLine($"{source} from /u/{request.Author}: {request.Body.Substring(0, Math.Min(request.Body.Length, 100))}");
#endif
        ProcessReply(request, reply);
    }

    private static async Task DoPeriodicWork()
    {
        if (FileSystemOperations.ConfigNeedsUpdate)
        {
            var newConfig = FileSystemOperations.GetConfig();

            if (newConfig != null)
            {
                Config = newConfig;
                UpdatePostsToMonitor();
            }

            WriteLine("Reloaded config.json");
        }

        if (Config?.DownloadReservationDataFrequency > 0)
        {
            await FileSystemOperations.DownloadNewReservationData(Config.DownloadReservationDataFrequency);
        }

        if (FileSystemOperations.ReservationDataNeedsUpdate)
        {
            var newData = FileSystemOperations.GetReservationData();

            if (ReservationData != null)
            {
                var updated = false;

                for (var i = 0; i < newData.Count; ++i)
                {
                    if (!newData[i].Region.Equals(ReservationData[i].Region, StringComparison.CurrentCultureIgnoreCase) ||
                        !newData[i].Model.Equals(ReservationData[i].Model, StringComparison.CurrentCultureIgnoreCase) ||
                        newData[i].ReserveTime != ReservationData[i].ReserveTime)
                    {
                        updated = true;
                        break;
                    }
                }

                if (updated)
                {
                    WriteLine("Found new reservation data");
                }
            }

            ReservationData = newData;
        }
    }

    private static void ProcessReply(IncomingRequest request, string reply)
    {
        if (string.IsNullOrWhiteSpace(reply)) return;

        // TODO: don't process if already replied, which may consume too many API calls

        lock (_lock)
        {
            if (request.Source == RequestSource.PrivateMessage)
            {
                messageReplyQueue.Enqueue(new BotReply
                {
                    CommentId = request.MessageId,
                    Reply = reply,
                    ReplyTime = DateTime.Now,
                    OriginalAuthor = request.Author
                });
            }
            else
            {
                commentReplyQueue.Enqueue(new BotReply
                {
                    CommentId = request.MessageId,
                    Reply = reply,
                    ReplyTime = DateTime.Now,
                    OriginalAuthor = request.Author
                });
            }
        }
    }

    private static void WriteLine(string text)
    {
        System.Console.WriteLine($"\n{text}");
        Log.Information(text);
    }

    public static void ReloadReservationData()
    {
        WriteLine("Reloading reservation data...");
        ReservationData = FileSystemOperations.GetReservationData();
    }
}