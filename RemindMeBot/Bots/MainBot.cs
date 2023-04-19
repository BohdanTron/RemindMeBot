using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;

namespace RemindMeBot.Bots
{
    public class MainBot<T> : ActivityHandler where T : Dialog
    {
        private readonly T _dialog;
        private readonly ConversationState _conversationState;
        private readonly IStatePropertyAccessor<DialogState> _dialogStateAccessor;

        public MainBot(T dialog, ConversationState conversationState)
        {
            _dialog = dialog;
            _conversationState = conversationState;
            _dialogStateAccessor = _conversationState.CreateProperty<DialogState>(nameof(DialogState));
        }

        public override async Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken = new())
        {
            await base.OnTurnAsync(turnContext, cancellationToken);

            await _conversationState.SaveChangesAsync(turnContext, cancellationToken: cancellationToken);
        }

        protected override Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
        {
            return _dialog.RunAsync(turnContext, _dialogStateAccessor, cancellationToken);
        }
    }
}
