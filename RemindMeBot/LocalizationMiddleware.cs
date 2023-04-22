using System.Globalization;
using Microsoft.Bot.Builder;
using RemindMeBot.Models;
using RemindMeBot.Services;
using IMiddleware = Microsoft.Bot.Builder.IMiddleware;

namespace RemindMeBot
{
    public class LocalizationMiddleware : IMiddleware
    {
        private readonly StateService _stateService;

        public LocalizationMiddleware(StateService stateService) => 
            _stateService = stateService;

        public async Task OnTurnAsync(ITurnContext turnContext, NextDelegate next, CancellationToken cancellationToken = new())
        {
            var userSettings = await _stateService.UserSettingsPropertyAccessor.GetAsync(turnContext,
                () => new UserSettings(), cancellationToken);

            var cultureInfo = new CultureInfo(userSettings.LanguageCode ?? "en-US");

            CultureInfo.CurrentCulture = cultureInfo;
            CultureInfo.CurrentUICulture = cultureInfo;

            await next(cancellationToken);
        }
    }
}
