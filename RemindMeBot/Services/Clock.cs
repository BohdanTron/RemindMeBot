using NodaTime;
using NodaTime.Extensions;

namespace RemindMeBot.Services
{
    public interface IClock
    {
        DateTimeOffset GetLocalDateTime(string timeZone);
        DateTimeOffset ToAnotherTimeZone(DateTime sourceDateTime, string sourceTimeZoneId, string targetTimeZoneId);
    }

    public class Clock : IClock
    {
        public DateTimeOffset GetLocalDateTime(string timeZone)
        {
            var userTimeZone = DateTimeZoneProviders.Tzdb[timeZone];
            var clock = SystemClock.Instance.InZone(userTimeZone);

            var localDateTime = clock.GetCurrentZonedDateTime().ToDateTimeOffset();

            return localDateTime;
        }

        public DateTimeOffset ToAnotherTimeZone(DateTime sourceDateTime, string sourceTimeZoneId, string targetTimeZoneId)
        {
            var sourceDateTimeLocal = LocalDateTime.FromDateTime(sourceDateTime);
            var sourceTimeZone = DateTimeZoneProviders.Tzdb[sourceTimeZoneId];
            var sourceZonedDateTime = sourceDateTimeLocal.InZoneLeniently(sourceTimeZone);

            var targetTimeZone = DateTimeZoneProviders.Tzdb[targetTimeZoneId];
            var targetZonedDateTime = sourceZonedDateTime.WithZone(targetTimeZone);
            
            return  targetZonedDateTime.ToDateTimeOffset();
        }
    }
}
