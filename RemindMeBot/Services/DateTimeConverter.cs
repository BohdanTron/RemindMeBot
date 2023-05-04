using NodaTime;
using NodaTime.Extensions;

namespace RemindMeBot.Services
{
    public interface IDateTimeConverter
    {
        DateTime ToLocalDateTime(string timeZone);
    }

    public class DateTimeConverter : IDateTimeConverter
    {
        public DateTime ToLocalDateTime(string timeZone)
        {
            var userTimeZone = DateTimeZoneProviders.Tzdb[timeZone];
            var clock = SystemClock.Instance.InZone(userTimeZone);

            var localDateTime = clock.GetCurrentZonedDateTime().LocalDateTime;
            return localDateTime.ToDateTimeUnspecified();
        }
    }
}
