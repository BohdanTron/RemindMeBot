using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;

namespace RemindMeBot.Dialogs
{
    public class MainDialog : ComponentDialog
    {
        private readonly UserSettingsDialog _userSettingsDialog;

        public MainDialog(UserSettingsDialog userSettingsDialog) : base(nameof(MainDialog))
        {
            _userSettingsDialog = userSettingsDialog;

            AddDialog(_userSettingsDialog);
            AddDialog(new WaterfallDialog($"{nameof(MainDialog)}.{nameof(WaterfallDialog)}",
                new WaterfallStep[]
                {
                    ProcessInputStep,
                }));

            InitialDialogId = $"{nameof(MainDialog)}.{nameof(WaterfallDialog)}";
        }

        private async Task<DialogTurnResult> ProcessInputStep(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var input = stepContext.Context.Activity.Text;

            switch (input)
            {
                case "/start":
                    return await stepContext.BeginDialogAsync(_userSettingsDialog.Id, cancellationToken: cancellationToken);

                // TODO: Implement other commands here

                default:
                    await stepContext.Context.SendActivityAsync(MessageFactory.Text("Unknown command. Please enter a valid command."), cancellationToken);
                    break;
            }

            return await stepContext.EndDialogAsync(cancellationToken: cancellationToken);
        }
    }
}
