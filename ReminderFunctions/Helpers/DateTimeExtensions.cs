using System;
using NodaTime;

namespace ReminderFunctions.Helpers
{
    public static class DateTimeExtensions
    {
        public static DateTime ToDateTimeUtc(this DateTime dateTimeLocal, string timeZoneId)
        {
            var localTimeZone = DateTimeZoneProviders.Tzdb[timeZoneId];

            var localDateTime = LocalDateTime.FromDateTime(dateTimeLocal);

            var zonedDateTime = localDateTime.InZoneLeniently(localTimeZone);

            return zonedDateTime.ToDateTimeUtc();
        }
    }
}
