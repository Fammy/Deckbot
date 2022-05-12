using System.Text.RegularExpressions;
using Deckbot.Console.Models;

namespace Deckbot.Console;

public class BotCommand
{
    private const int PreOrderStartTime = 1626454800;

    private readonly List<(string Pattern, Func<Match, string> ReplyFunc)> commands = new()
    {
        (RegexConsts.RegionModelTime, ReserveTimeRequest),
        (RegexConsts.ModelRegionTime, ReserveTimeRequest),
        (RegexConsts.TimeModelRegion, ReserveTimeRequest),
        (RegexConsts.TimeRegionModel, ReserveTimeRequest),
        (RegexConsts.RegionTimeModel, ReserveTimeRequest),
        (RegexConsts.ModelTimeRegion, ReserveTimeRequest),
        (RegexConsts.Help, HelpRequest)
    };

    public (bool Success, string Reply) ProcessComment(IncomingRequest request)
    {
        if (!request.IsAtValidLevel && request.Body.Contains("!deckbot", StringComparison.CurrentCultureIgnoreCase))
        {
            return (false, string.Empty);
            //return (true, @"Hi, while I do work in this post, I only work on replies to other comments to cut down on top level deckbot spam. You can also [PM](https://www.reddit.com/message/compose/?to=deck_bot&subject=deck&message=!deckbot%20) me if you want or comment (anywhere!) on [my post](https://www.reddit.com/r/SteamDeck/comments/ui642q/introducing_deckbot/).");
        }

        foreach (var command in commands)
        {
#if DEBUG
            var pattern = command.Pattern.Replace("deckbot", @"debugdeckbot").Replace("deck_bot", @"debugdeck_bot");
#else
            var pattern = command.Pattern;
#endif

            var match = Regex.Match(request.Body, pattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                return (true, command.ReplyFunc(match));
            }
        }

        return (false, string.Empty);
    }

    private static string HelpRequest(Match match) => @"Hi, I'm deckbot. Here's what you can do with me:

Find out how far the order queue is to your order: `!deckbot region model rtReserveTime`

Example: `!deckbot US 64 1626460525`

* region must be `US`, `UK`, or `EU`
* model must be `64`, `256`, or `512`
* rtReserveTime must be a valid 10 digit epoch number, in the starting with 16.

If you don't have your `rtReserveTime`, here's how to get it:

* Log into the [Steam website](https://store.steampowered.com/)
* Go to this [API link](https://store.steampowered.com/reservation/ajaxgetuserstate?rgReservationPackageIDs=%5B595603,595604,595605%5D). It should be a bunch of data. If you only see `{""success"":21}` then you aren't logged in. Repeat Step 1.
* Find the text `rtReserveTime` and copy the number immediately after. It will start with 16 and is ten digits long, like `1626460525` If the number is 0, then you've ordered yours and it's too late to find it.

*(I'm in beta. Direct feedback to Fammy.)*";

    private static string ReserveTimeRequest(Match match)
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
            return $@"Hi! It looks like you have a **{region} {model}GB** reservation. Your reservation time is in the future. Something seems off. Summon me with `!deckbot help` and I'll tell you how to find your rtReserveTime.";
        }

        if (reserveTime < PreOrderStartTime)
        {
            return $@"Hi! It looks like you have a **{region} {model}GB** reservation. Your reservation time is before pre-orders opened. Something seems off. Summon me with `!deckbot help` and I'll tell you how to find your rtReserveTime.";
        }

        var timeAfterSeconds = reserveTime - PreOrderStartTime;

        var timeAfter = new TimeSpan(0, 0, timeAfterSeconds);

        var bestTime = GetBestTimeForRegionAndModel(region, model);
        var timeLeft = new TimeSpan(0, 0, reserveTime - bestTime);

        var timeAfterStr = FormatTime(timeAfter);

        if (timeLeft.TotalSeconds < 0)
        {
            return $@"Hi! It looks like you have a **{region} {model}GB** reservation. You reserved your deck **{timeAfterStr}** after pre-orders opened. Order emails have likely passed your time. Have you received your order email yet? If not, you have an incorrect rtReserveTime. Summon me with `!deckbot help` and I'll tell you how to find your rtReserveTime.";
        }

        if (timeLeft.TotalSeconds == 0)
        {
            return $@"Whoa!! It looks like you have a **{region} {model}GB** reservation. You reserved your deck **{timeAfterStr}** after pre-orders opened. There are **0** worth of pre-orders before yours remaining. You may or may not have received your order email. If you haven't, you should next batch!";
        }

        var timeLeftStr = FormatTime(timeLeft);
        var percent = ((bestTime - PreOrderStartTime) / (double)(reserveTime - PreOrderStartTime)) * 100;

        var greeting = PickRandomly("Hi!", "Howdy!", "Hello!", "Greetings!", "Hola!", "Ciao!");
        var closing = percent >= 90 ?
            "! " + PickRandomly("Soon™️", "👀", "So close!", "Get hype") :
            percent.ToString().StartsWith("50.") ? ". Perfectly balanced" :
            percent < 1 ? ". " + PickRandomly("Oof", "😢", "Bruh", "Hang in there!", "Welp"):
        ".";

        return $@"{greeting} It looks like you have a **{region} {model}GB** reservation. You reserved your deck **{timeAfterStr}** after pre-orders opened. There are **{timeLeftStr}** worth of pre-orders before yours remaining. You're **{percent:N2}%** of the way there{closing}";
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
        if (Bot.ReservationData == null)
        {
            throw new ArgumentException($"{nameof(Bot.ReservationData)} is null");
        }

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