using Microsoft.Extensions.Localization;
using RemindMeBot.Models;
using RemindMeBot.Resources;

namespace RemindMeBot.Services
{
    public class RepeatedIntervalMapper
    {
        private static readonly Dictionary<string, RepeatedInterval> StringsToEnumsMap =
            new(StringComparer.InvariantCultureIgnoreCase)
            {
                { "D", RepeatedInterval.Daily },
                { "W", RepeatedInterval.Weekly },
                { "M", RepeatedInterval.Monthly },
                { "Y", RepeatedInterval.Yearly },

                { "daily", RepeatedInterval.Daily },
                { "weekly", RepeatedInterval.Weekly },
                { "monthly", RepeatedInterval.Monthly },
                { "yearly", RepeatedInterval.Yearly }
            };

        private readonly IStringLocalizer<BotMessages> _localizer;

        public RepeatedIntervalMapper(IStringLocalizer<BotMessages> localizer) =>
            _localizer = localizer;

        public virtual RepeatedInterval MapToEnum(string? value)
        {
            if (value is null) return RepeatedInterval.None;

            return StringsToEnumsMap.TryGetValue(value, out var result)
                ? result
                : RepeatedInterval.None;
        }

        public virtual RepeatedInterval MapToEnumFromLocalized(string? value)
        {
            if (value is null) return RepeatedInterval.None;

            var map = new Dictionary<string, RepeatedInterval>(StringComparer.InvariantCultureIgnoreCase)
            {
                { _localizer[ResourceKeys.Daily], RepeatedInterval.Daily },
                { _localizer[ResourceKeys.Weekly], RepeatedInterval.Weekly },
                { _localizer[ResourceKeys.Monthly], RepeatedInterval.Monthly },
                { _localizer[ResourceKeys.Yearly], RepeatedInterval.Yearly }
            };

            return map.TryGetValue(value, out var result)
                ? result
                : RepeatedInterval.None;
        }

        public virtual string? MapToLocalizedString(RepeatedInterval value)
        {
            var map = new Dictionary<RepeatedInterval, string>
            {
                { RepeatedInterval.Daily, _localizer[ResourceKeys.Daily] },
                { RepeatedInterval.Weekly, _localizer[ResourceKeys.Weekly] },
                { RepeatedInterval.Monthly, _localizer[ResourceKeys.Monthly] },
                { RepeatedInterval.Yearly, _localizer[ResourceKeys.Yearly] }
            };

            return map.TryGetValue(value, out var result)
                ? result
                : null;
        }
    }
}
