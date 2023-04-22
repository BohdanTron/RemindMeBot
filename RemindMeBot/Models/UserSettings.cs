namespace RemindMeBot.Models
{
    public record UserSettings
    {
        public string? Location { get; init; }
        public string? Language { get; init; }
        public string? LanguageCode { get; init; }
        public string? TimeZoneId { get; init; }
    }
}
