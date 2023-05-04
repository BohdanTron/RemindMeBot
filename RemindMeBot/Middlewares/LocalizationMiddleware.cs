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
        private readonly IDateTimeConverter _dateTimeConverter;

        public LocalizationMiddleware(IStateService stateService, IDateTimeConverter dateTimeConverter)
        {
            _stateService = stateService;
            _dateTimeConverter = dateTimeConverter;
        }

        public async Task OnTurnAsync(ITurnContext turnContext, NextDelegate next, CancellationToken cancellationToken = new())
        {
            var userSettings = await _stateService.UserSettingsPropertyAccessor.GetAsync(turnContext,
                () => new UserSettings(), cancellationToken);

            var cultureInfo = new CultureInfo(userSettings.Culture ?? "en-US");

            CultureInfo.CurrentCulture = cultureInfo;
            CultureInfo.CurrentUICulture = cultureInfo;

            turnContext.Activity.Locale = cultureInfo.Name;

            turnContext.Activity.LocalTimezone = userSettings.TimeZone ?? turnContext.Activity.LocalTimezone;
            turnContext.Activity.LocalTimestamp = userSettings.TimeZone is not null
                ? _dateTimeConverter.ToLocalDateTime(userSettings.TimeZone)
                : turnContext.Activity.LocalTimestamp;

            await next(cancellationToken);
        }
    }
}
