using NodaTime;
using NodaTime.Extensions;

namespace RemindMeBot.Models
{
    public record UserSettings
    {
        public string? Location { get; init; }
        public string? Language { get; init; }
        public string? Culture { get; init; }
        public string? TimeZoneId { get; init; }
        public string? LocalTime => GetLocalTime();

        private string? GetLocalTime()
        {
            if (TimeZoneId is null) return null;

            var userTimeZone = DateTimeZoneProviders.Tzdb[TimeZoneId];
            var clock = SystemClock.Instance.InZone(userTimeZone);

            var localTime = $"{clock.GetCurrentTimeOfDay():HH:mm}";
            return localTime;
        }
    }
}
