using NodaTime;
using NodaTime.Extensions;

namespace RemindMeBot.Models
{
    public record UserSettings
    {
        public string? Location { get; set; }
        public string? Language { get; set; }
        public string? Culture { get; set; }
        public string? TimeZone { get; set; }
        public string? LocalTime => GetLocalTime();

        private string? GetLocalTime()
        {
            if (TimeZone is null) return null;

            var userTimeZone = DateTimeZoneProviders.Tzdb[TimeZone];
            var clock = SystemClock.Instance.InZone(userTimeZone);

            var localTime = $"{clock.GetCurrentTimeOfDay():HH:mm}";
            return localTime;
        }

        public void Deconstruct(out string? location, out string? language, out string? timeZone, out string? localTime) =>
            (location, language, timeZone, localTime) = (Location, Language, TimeZone, LocalTime);
    }
}
