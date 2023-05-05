using NodaTime;
using NodaTime.Extensions;

namespace RemindMeBot.Services
{
    public interface IClock
    {
        DateTime GetLocalDateTime(string timeZone);
    }

    public class Clock : IClock
    {
        public DateTime GetLocalDateTime(string timeZone)
        {
            var userTimeZone = DateTimeZoneProviders.Tzdb[timeZone];
            var clock = SystemClock.Instance.InZone(userTimeZone);

            var localDateTime = clock.GetCurrentZonedDateTime().ToDateTimeUnspecified();

            return localDateTime;
        }
    }
}
