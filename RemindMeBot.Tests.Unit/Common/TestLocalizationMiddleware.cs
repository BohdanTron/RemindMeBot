using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;

namespace RemindMeBot.Tests.Unit.Common
{
    public class TestLocalizationMiddleware : IMiddleware
    {
        private readonly CultureInfo _culture;

        private readonly string? _localTimeZone;
        private readonly DateTimeOffset? _localTimeStamp;

        public TestLocalizationMiddleware(CultureInfo culture, string? localTimeZone = null, DateTimeOffset? localTimeStamp = null)
        {
            _culture = culture;
            _localTimeZone = localTimeZone;
            _localTimeStamp = localTimeStamp;
        }

        public async Task OnTurnAsync(ITurnContext turnContext, NextDelegate next, CancellationToken cancellationToken = default)
        {
            CultureInfo.CurrentCulture = _culture;
            CultureInfo.CurrentUICulture = _culture;

            CultureInfo.CurrentCulture.DateTimeFormat.DateSeparator = "/";
            CultureInfo.CurrentUICulture.DateTimeFormat.DateSeparator = "/";

            CultureInfo.CurrentCulture.DateTimeFormat.ShortDatePattern =
                CultureInfo.CurrentCulture.DateTimeFormat.ShortDatePattern.Replace(".", "/");

            CultureInfo.CurrentUICulture.DateTimeFormat.ShortDatePattern =
                CultureInfo.CurrentCulture.DateTimeFormat.ShortDatePattern.Replace(".", "/");

            turnContext.Activity.Locale = _culture.Name;

            turnContext.Activity.LocalTimezone = _localTimeZone ?? turnContext.Activity.LocalTimezone;
            turnContext.Activity.LocalTimestamp = _localTimeStamp ?? turnContext.Activity.LocalTimestamp;

            await next(cancellationToken);
        }
    }
}
