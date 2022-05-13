using Deckbot.Console.Models;
using Newtonsoft.Json;

namespace Deckbot.Console;

public static class FileSystemOperations
{
    private const string ConfigFilename = "./config/config.json";
    private const string ReservationDataFilename = "./config/data.tsv";
    private const string CommentReplyQueueFilename = "./data/commentreplyqueue.json";
    private const string MessageReplyQueueFilename = "./data/messagereplyqueue.json";
    private const string ErrorFilename = "./logs/{0}.log";
    private const string ReservationDataDownloadUrl = "https://docs.google.com/spreadsheets/d/1ngfg2eP8E_Ue81lqGl6v34uVJ73qrfnq9S-H1aCZGD0/export?format=csv&gid=277245429";

    private static readonly object _lock = new();
    private static DateTime lastConfigUpdate = DateTime.MinValue;
    private static DateTime lastReservationDataUpdate = DateTime.MinValue;
    private static DateTime lastReservationDataDownload = DateTime.MinValue;

    public static RedditConfig? GetConfig()
    {
        lastConfigUpdate = DateTime.Now;

        var json = File.ReadAllText(ConfigFilename);
        return JsonConvert.DeserializeObject<RedditConfig>(json);
    }

    public static List<(string Model, string Region, int ReserveTime)> GetReservationData()
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

            lastReservationDataUpdate = DateTime.Now;
            return data;
        }
    }

    public static bool ReservationDataNeedsUpdate
    {
        get
        {
            if (DateTime.Now - lastReservationDataUpdate <= TimeSpan.FromSeconds(30)) return false;

            var fileInfo = new FileInfo(ReservationDataFilename);

            if (fileInfo.LastWriteTime >= lastReservationDataUpdate)
            {
                return true;
            }

            return false;
        }
    }

    public static bool ConfigNeedsUpdate
    {
        get
        {
            if (DateTime.Now - lastConfigUpdate <= TimeSpan.FromSeconds(30)) return false;

            var fileInfo = new FileInfo(ConfigFilename);

            if (fileInfo.LastWriteTime >= lastConfigUpdate)
            {
                return true;
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

    public static async Task DownloadNewReservationData(int frequency)
    {
        if (DateTime.Now - lastReservationDataDownload < TimeSpan.FromMinutes(frequency))
        {
            return;
        }

        lastReservationDataDownload = DateTime.Now;

        try
        {
            var client = new HttpClient();
            var data = await client.GetStringAsync(ReservationDataDownloadUrl);
            var tsv = data.Replace(",", "\t");

            lock (_lock)
            {
                File.WriteAllText(ReservationDataFilename, tsv);
            }
        }
        catch
        {
            // Failed, we'll try again later
        }
    }
}