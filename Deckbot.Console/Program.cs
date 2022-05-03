using Deckbot.Console;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.File("./logs/deckbot.log")
    .CreateLogger();

Bot.Go();