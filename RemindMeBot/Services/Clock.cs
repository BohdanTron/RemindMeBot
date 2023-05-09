using NodaTime;
using NodaTime.Extensions;

namespace RemindMeBot.Services
{
    public interface IClock
    {
        DateTimeOffset GetLocalDateTime(string timeZone);
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
    }
}
