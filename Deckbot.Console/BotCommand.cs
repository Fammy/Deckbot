using System.Text.RegularExpressions;
using Reddit.Controllers;
using Serilog;

namespace Deckbot.Console;

public class BotCommand
{
    private const int PreOrderStartTime = 1626454800;

    private readonly List<(string Pattern, Func<Comment, Match, string> ReplyFunc)> commands = new()
    {
        (RegexConsts.RegionModelTime, ReserveTimeRequest),
        (RegexConsts.ModelRegionTime, ReserveTimeRequest),
        (RegexConsts.TimeModelRegion, ReserveTimeRequest),
        (RegexConsts.TimeRegionModel, ReserveTimeRequest),
        (RegexConsts.RegionTimeModel, ReserveTimeRequest),
        (RegexConsts.ModelTimeRegion, ReserveTimeRequest),
        (RegexConsts.Help, HelpRequest),
        (@"16[0-9]{8}", NullResponse),
        (@"rtReserveTime", NullResponse),
        (@"fammy", NullResponse),
        //(@"(!deckbot|!deck_bot)", HelpRequest),
    };

    public (bool Success, string Reply) ProcessComment(Comment comment)
    {
        foreach (var command in commands)
        {
            var match = Regex.Match(comment.Body, command.Pattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                return (true, command.ReplyFunc(comment, match));
            }
        }

        return (false, string.Empty);
    }

    private static string NullResponse(Comment comment, Match match)
    {
        var body = comment.Body;

        System.Console.WriteLine($"\n## {body}\n");
        Log.Information(body);

        return string.Empty;
    }

    private static string HelpRequest(Comment comment, Match match) => @$"Hi {comment.Author}, I'm deck_bot. Here's what you can do with me:

Find out far the queue is to your order: `!deckbot region model rtReserveTime`

Example: `!deckbot US 64 1626460525`

* region must be US, UK, or EU
* model must be 64, 256, or 512
* rtReserveTime must be a valid 10 digit epoch number, in the starting with 16.

I only respond in /r/SteamDeck

*(I'm in beta. Direct feedback to Fammy.)*";

    private static string ReserveTimeRequest(Comment comment, Match match)
    {
        var region = match.Groups["region"].ToString().ToUpper();
        var model = match.Groups["model"].ToString().ToUpper();
        var reserveTimeStr = match.Groups["time"].ToString();

        if (string.IsNullOrWhiteSpace(region) || string.IsNullOrWhiteSpace(model) || string.IsNullOrWhiteSpace(reserveTimeStr))
        {
            return string.Empty;
        }

        var reserveTime = int.Parse(reserveTimeStr);

        if (IsInFuture(reserveTime))
        {
            return $@"Hi {comment.Author}, if looks like you have a **{region} {model}gb** reservation. Your reservation time is in the future. Something seems off.";
        }

        if (reserveTime < PreOrderStartTime)
        {
            return $@"Hi {comment.Author}, if looks like you have a **{region} {model}gb** reservation. Your reservation time is before pre-orders opened. Something seems off.";
        }

        var timeAfterSeconds = reserveTime - PreOrderStartTime;

        if (timeAfterSeconds < 0)
        {
            return string.Empty;
        }

        var timeAfter = new TimeSpan(0, 0, timeAfterSeconds);

        var bestTime = GetBestTimeForRegionAndModel(region, model);
        var timeLeft = new TimeSpan(0, 0, reserveTime - bestTime);

        var timeAfterStr = FormatTime(timeAfter);
        var timeLeftStr = FormatTime(timeLeft);
        var percent = ((bestTime - PreOrderStartTime) / (double)(reserveTime - PreOrderStartTime)) * 100;

        if (timeLeft.TotalSeconds <= 0)
        {
            return $@"Hi {comment.Author}, if looks like you have a **{region} {model}gb** reservation. You reserved your deck **{timeAfterStr}** after pre-orders opened. Order emails have likely passed your time. Have you received your order email yet?";
        }

        return $@"Hi {comment.Author}, if looks like you have a **{region} {model}gb** reservation. You reserved your deck **{timeAfterStr}** after pre-orders opened. You have **{timeLeftStr}** worth of pre-orders before yours remaining. You're **{percent:N2}%** of the way there!";
    }

    private static bool IsInFuture(int seconds)
    {
        return DateTime.UnixEpoch.AddSeconds(seconds) > DateTime.UtcNow;
    }

    private static int GetBestTimeForRegionAndModel(string region, string model)
    {
        var match = Bot.ReservationData.Single(d => d.Model.Equals(model, StringComparison.CurrentCultureIgnoreCase) &&
                                                                  d.Region.Equals(region, StringComparison.CurrentCultureIgnoreCase));
        return match.ReserveTime;
    }

    private static string FormatTime(TimeSpan span)
    {
        var formatted = BuildTime(string.Empty, span.Days, "day");
        formatted = BuildTime(formatted, span.Hours, "hour");
        formatted = BuildTime(formatted, span.Minutes, "minute");
        formatted = BuildTime(formatted, span.Seconds, "second");

        return formatted;
    }

    private static string BuildTime(string currentStr, double length, string unit)
    {
        if (length <= 0) return currentStr;

        var s = length > 1 ? "s" : string.Empty;

        if (string.IsNullOrWhiteSpace(currentStr))
        {
            return $"{length} {unit}{s}";
        }

        return $"{currentStr}, {length} {unit}{s}";
    }
}