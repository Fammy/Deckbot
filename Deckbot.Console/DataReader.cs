namespace Deckbot.Console;

public static class DataReader
{
    private const string ReservationDataFilename = "./config/data.tsv";

    private static readonly object _lock = new ();
    private static DateTime lastUpdated = DateTime.MinValue;

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

    public static bool NeedsUpdate
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
}