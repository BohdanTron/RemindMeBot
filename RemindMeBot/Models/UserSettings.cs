namespace RemindMeBot.Models
{
    public record UserSettings
    {
        public string? Location { get; init; }
        public string? Language { get; init; }

        public void Deconstruct(out string? location, out string? language)
        {
            location = Location;
            language = Language;
        }
    }
}
