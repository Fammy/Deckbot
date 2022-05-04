using System.Text.RegularExpressions;
using Reddit.Controllers;

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
        //(RegexConsts.Thanks, ThanksRequest),
        (RegexConsts.Help, HelpRequest),
        //(@"16[0-9]{8}", NullResponse),
        //(@"rtReserveTime", NullResponse),
        //(@"fammy", NullResponse),
        //(@"(!deckbot|!deck_bot)", HelpRequest),
    };

    public (bool Success, string Reply) ProcessComment(Comment comment)
    {
        foreach (var command in commands)
        {
#if DEBUG
            var pattern = command.Pattern.Replace("deckbot", @"debugdeckbot").Replace("deck_bot", @"debugdeck_bot");
#else
            var pattern = command.Pattern;
#endif

            var match = Regex.Match(comment.Body, pattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                return (true, command.ReplyFunc(comment, match));
            }
        }

        return (false, string.Empty);
    }

    private static string NullResponse(Comment comment, Match match)
    {
        /*var body = comment.Body;

        System.Console.WriteLine($"\n## {body}\n");
        Log.Information(body);*/

        return string.Empty;
    }

    private static string HelpRequest(Comment comment, Match match) => @"Hi, I'm deckbot. Here's what you can do with me:

Find out how far the order queue is to your order: `!deckbot region model rtReserveTime`

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
            return $@"Hi! It looks like you have a **{region} {model}GB** reservation. Your reservation time is in the future. Something seems off.";
        }

        if (reserveTime < PreOrderStartTime)
        {
            return $@"Hi! It looks like you have a **{region} {model}GB** reservation. Your reservation time is before pre-orders opened. Something seems off.";
        }

        var timeAfterSeconds = reserveTime - PreOrderStartTime;

        var timeAfter = new TimeSpan(0, 0, timeAfterSeconds);

        var bestTime = GetBestTimeForRegionAndModel(region, model);
        var timeLeft = new TimeSpan(0, 0, reserveTime - bestTime);

        var timeAfterStr = FormatTime(timeAfter);

        if (timeLeft.TotalSeconds <= 0)
        {
            return $@"Hi! It looks like you have a **{region} {model}GB** reservation. You reserved your deck **{timeAfterStr}** after pre-orders opened. Order emails have likely passed your time. Have you received your order email yet?";
        }

        var timeLeftStr = FormatTime(timeLeft);
        var percent = ((bestTime - PreOrderStartTime) / (double)(reserveTime - PreOrderStartTime)) * 100;

        var closing = percent >= 90 ? "! " + PickRandomly("Soon™️", "👀", "So close!", "Get hype") : ".";

        return $@"Hi! It looks like you have a **{region} {model}GB** reservation. You reserved your deck **{timeAfterStr}** after pre-orders opened. There are **{timeLeftStr}** worth of pre-orders before yours remaining. You're **{percent:N2}%** of the way there{closing}";
    }

    private static string PickRandomly(params string[] options)
    {
        var random = new Random();
        var index = random.Next(1, options.Length) - 1;

        return options[index];
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

    private static string ThanksRequest(Comment comment, Match match)
    {
        if (comment.Body.Contains("no")) return string.Empty;

        return @$"You're welcome {comment.Author}!";
    }
}