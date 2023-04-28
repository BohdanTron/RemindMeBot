using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;
using RemindMeBot.Services;

namespace RemindMeBot.Bots
{
    public class MainBot<T> : ActivityHandler where T : Dialog
    {
        private readonly T _dialog;
        private readonly IStateService _stateService;

        public MainBot(T dialog, IStateService stateService)
        {
            _dialog = dialog;
            _stateService = stateService;
        }

        public override async Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken = new())
        {
            await base.OnTurnAsync(turnContext, cancellationToken);

            await _stateService.UserState.SaveChangesAsync(turnContext, cancellationToken: cancellationToken);
            await _stateService.ConversationState.SaveChangesAsync(turnContext, cancellationToken: cancellationToken);
        }

        protected override Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
        {
            return _dialog.RunAsync(turnContext, _stateService.DialogStatePropertyAccessor, cancellationToken);
        }
    }
}
