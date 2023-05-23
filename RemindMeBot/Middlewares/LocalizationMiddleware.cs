using System.Globalization;
using Microsoft.Bot.Builder;
using RemindMeBot.Models;
using RemindMeBot.Services;
using IMiddleware = Microsoft.Bot.Builder.IMiddleware;

namespace RemindMeBot.Middlewares
{
    public class LocalizationMiddleware : IMiddleware
    {
        private readonly IStateService _stateService;
        private readonly IClock _clock;

        public LocalizationMiddleware(IStateService stateService, IClock clock)
        {
            _stateService = stateService;
            _clock = clock;
        }

        public async Task OnTurnAsync(ITurnContext turnContext, NextDelegate next, CancellationToken cancellationToken = new())
        {
            var userSettings = await _stateService.UserSettingsPropertyAccessor.GetAsync(turnContext,
                () => new UserSettings(), cancellationToken);

            var cultureInfo = new CultureInfo(userSettings.Culture ?? "en-US");

            CultureInfo.CurrentCulture = cultureInfo;
            CultureInfo.CurrentUICulture = cultureInfo;

            CultureInfo.CurrentCulture.DateTimeFormat.DateSeparator = "/";
            CultureInfo.CurrentUICulture.DateTimeFormat.DateSeparator = "/";

            CultureInfo.CurrentCulture.DateTimeFormat.ShortDatePattern =
                CultureInfo.CurrentCulture.DateTimeFormat.ShortDatePattern.Replace(".", "/");

            CultureInfo.CurrentUICulture.DateTimeFormat.ShortDatePattern =
                CultureInfo.CurrentCulture.DateTimeFormat.ShortDatePattern.Replace(".", "/");

            turnContext.Activity.Locale = cultureInfo.Name;

            turnContext.Activity.LocalTimezone = userSettings.TimeZone ?? turnContext.Activity.LocalTimezone;
            turnContext.Activity.LocalTimestamp = userSettings.TimeZone is not null
                ? _clock.GetLocalDateTime(userSettings.TimeZone)
                : turnContext.Activity.LocalTimestamp;

            await next(cancellationToken);
        }
    }
}
