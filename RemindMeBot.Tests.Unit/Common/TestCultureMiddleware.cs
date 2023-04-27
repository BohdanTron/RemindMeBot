using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;

namespace RemindMeBot.Tests.Unit.Common
{
    public class TestCultureMiddleware : IMiddleware
    {
        private readonly CultureInfo _culture;

        public TestCultureMiddleware(CultureInfo culture)
        {
            _culture = culture;
        }

        public async Task OnTurnAsync(ITurnContext turnContext, NextDelegate next, CancellationToken cancellationToken = default)
        {
            CultureInfo.CurrentCulture = _culture;
            CultureInfo.CurrentUICulture = _culture;

            await next(cancellationToken);
        }
    }
}
