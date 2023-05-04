using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;

namespace RemindMeBot.Tests.Unit.Common
{
    public class TestCultureMiddleware : IMiddleware
    {
        private readonly CultureInfo _culture;

        private readonly string? _localTimeZone;
        private readonly DateTime? _localTmeStamp;

        public TestCultureMiddleware(CultureInfo culture, string? localTimeZone = null, DateTime? localTmeStamp = null)
        {
            _culture = culture;
            _localTimeZone = localTimeZone;
            _localTmeStamp = localTmeStamp;
        }

        public async Task OnTurnAsync(ITurnContext turnContext, NextDelegate next, CancellationToken cancellationToken = default)
        {
            CultureInfo.CurrentCulture = _culture;
            CultureInfo.CurrentUICulture = _culture;

            turnContext.Activity.Locale = _culture.Name;

            turnContext.Activity.LocalTimezone = _localTimeZone ?? turnContext.Activity.LocalTimezone;
            turnContext.Activity.LocalTimestamp = _localTmeStamp ?? turnContext.Activity.LocalTimestamp;

            await next(cancellationToken);
        }
    }
}
