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

        public LocalizationMiddleware(IStateService stateService) => 
            _stateService = stateService;

        public async Task OnTurnAsync(ITurnContext turnContext, NextDelegate next, CancellationToken cancellationToken = new())
        {
            var userSettings = await _stateService.UserSettingsPropertyAccessor.GetAsync(turnContext,
                () => new UserSettings(), cancellationToken);

            var cultureInfo = new CultureInfo(userSettings.Culture ?? "en-US");

            CultureInfo.CurrentCulture = cultureInfo;
            CultureInfo.CurrentUICulture = cultureInfo;

            await next(cancellationToken);
        }
    }
}
