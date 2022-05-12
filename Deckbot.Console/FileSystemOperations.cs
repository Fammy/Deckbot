﻿using Deckbot.Console.Models;
using Newtonsoft.Json;

namespace Deckbot.Console;

public static class FileSystemOperations
{
    private const string ReservationDataFilename = "./config/data.tsv";
    private const string CommentReplyQueueFilename = "./data/commentreplyqueue.json";
    private const string MessageReplyQueueFilename = "./data/messagereplyqueue.json";
    private const string ErrorFilename = "./logs/{0}.log";

    private static readonly object _lock = new();
    private static DateTime lastUpdated = DateTime.MinValue;

    public static RedditConfig? GetConfig(string filename)
    {
        var json = File.ReadAllText(filename);
        return JsonConvert.DeserializeObject<RedditConfig>(json);
    }

    public static List<(string, string, int)> GetReservationData()
    {
        lock (_lock)
        {
            var lines = File.ReadAllLines(ReservationDataFilename);

            var data = new List<(string, string, int)>();

            foreach (var line in lines)
            {
                var values = line.Split("\t");
                if (values.Length >= 3 && int.TryParse(values[2], out var reserveTime))
                {
                    data.Add((values[0], values[1], reserveTime));
                }
            }

            lastUpdated = DateTime.Now;
            return data;
        }
    }

    public static bool ReservationDataNeedsUpdate
    {
        get
        {
            if (DateTime.Now - lastUpdated > TimeSpan.FromMinutes(5))
            {
                var fileInfo = new FileInfo(ReservationDataFilename);

                if (fileInfo.LastWriteTime >= lastUpdated)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public static void WriteReplyQueue(Queue<BotReply> replyQueue, RequestSource source)
    {
        var fileName = source == RequestSource.PrivateMessage ? MessageReplyQueueFilename : CommentReplyQueueFilename;
        WriteReplyQueue(replyQueue, fileName);
    }

    private static void WriteReplyQueue(Queue<BotReply> replyQueue, string fileName)
    {
        var json = JsonConvert.SerializeObject(replyQueue);
        File.WriteAllText(fileName, json);
    }

    public static Queue<BotReply> GetReplyQueue(RequestSource source)
    {
        var fileName = source == RequestSource.PrivateMessage ? MessageReplyQueueFilename : CommentReplyQueueFilename;
        return GetReplyQueue(fileName);
    }

    private static Queue<BotReply> GetReplyQueue(string fileName)
    {
        if (File.Exists(fileName))
        {
            var json = File.ReadAllText(fileName);

            if (string.IsNullOrWhiteSpace(json))
            {
                return new Queue<BotReply>();
            }

            return JsonConvert.DeserializeObject<Queue<BotReply>>(json) ?? new Queue<BotReply>();
        }

        return new Queue<BotReply>();
    }

    public static void WriteException(string filename, BotReply reply, Exception ex)
    {
        lock (_lock)
        {
            var errorFilename = string.Format(ErrorFilename, filename);

            var json = JsonConvert.SerializeObject(reply);
            var message = DateTime.Now + ": " + json + "\n" + ex + "\n\n";
            File.AppendAllText(errorFilename, message);
        }
    }
}