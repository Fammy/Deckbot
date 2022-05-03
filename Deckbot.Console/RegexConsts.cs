namespace Deckbot.Console;

public static class RegexConsts
{
    public const string Help = @"(!deckbot|!deck_bot)( )*(help)";
    public const string ModelRegionTime = @"(!deckbot|!deck_bot)(\.| |\/|\\|\w)*(?<model>64|256|512)(GB)?(\.| |\/|\\|\w)+(?<region>US|UK|EU)(\.| |\/|\\|\w)+(?<time>16[0-9]{8})";
    public const string RegionModelTime = @"(!deckbot|!deck_bot)(\.| |\/|\\|\w)*(?<region>US|UK|EU)(\.| |\/|\\|\w)+(?<model>64|256|512)(GB)?(\.| |\/|\\|\w)+(?<time>16[0-9]{8})";
    public const string ModelTimeRegion = @"(!deckbot|!deck_bot)(\.| |\/|\\|\w)*(?<model>64|256|512)(GB)?(\.| |\/|\\|\w)+(?<time>16[0-9]{8})(\.| |\/|\\|\w)+(?<region>US|UK|EU)";
    public const string RegionTimeModel = @"(!deckbot|!deck_bot)(\.| |\/|\\|\w)*(?<region>US|UK|EU)(\.| |\/|\\|\w)+(?<time>16[0-9]{8})(\.| |\/|\\|\w)+(?<model>64|256|512)(GB)?";
    public const string TimeModelRegion = @"(!deckbot|!deck_bot)(\.| |\/|\\|\w)*(?<time>16[0-9]{8})(\.| |\/|\\|\w)+(?<model>64|256|512)(GB)?(\.| |\/|\\|\w)+(?<region>US|UK|EU)";
    public const string TimeRegionModel = @"(!deckbot|!deck_bot)(\.| |\/|\\|\w)*(?<time>16[0-9]{8})(\.| |\/|\\|\w)+(?<region>US|UK|EU)(\.| |\/|\\|\w)+(?<model>64|256|512)(GB)?";
    public const string Thanks = @"(deckbot|deck_bot)(\.| |\/|\\|\w)+(thanks|thank you)|(thanks|thank you)(\.| |\/|\\|\w)+(deckbot|deck_bot)";
}
