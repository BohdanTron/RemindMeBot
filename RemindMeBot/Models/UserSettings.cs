namespace RemindMeBot.Models
{
    public record UserSettings
    {
        public string? Location { get; set; }
        public string? Language { get; set; }
        public string? Culture { get; set; }
        public string? TimeZone { get; set; }

        public void Deconstruct(out string? location, out string? language, out string? timeZone) =>
            (location, language, timeZone) = (Location, Language, TimeZone);
    }
}
