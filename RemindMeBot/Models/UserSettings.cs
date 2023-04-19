namespace RemindMeBot.Models
{
    public record UserSettings
    {
        public string Location { get; init; } = string.Empty;
        public string Language { get; init; } = string.Empty;

        public void Deconstruct(out string location, out string language)
        {
            location = Location;
            language = Language;
        }
    }
}
