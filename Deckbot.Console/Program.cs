using Deckbot.Console;
using Serilog;

MakeDirectory("data");
MakeDirectory("config");
MakeDirectory("logs");

Log.Logger = new LoggerConfiguration()
    .WriteTo.File("./logs/deckbot.log", rollOnFileSizeLimit: true, fileSizeLimitBytes: 1000 * 1000) // Rollover at a meg
    .CreateLogger();

Bot.Go();

static void MakeDirectory(string path)
{
    path = $"./{path}";

    if (!Directory.Exists(path))
    {
        Directory.CreateDirectory(path);
    }
}