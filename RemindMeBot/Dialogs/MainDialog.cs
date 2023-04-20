using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Extensions.Localization;
using RemindMeBot.Resources;

namespace RemindMeBot.Dialogs
{
    public class MainDialog : ComponentDialog
    {
        private readonly UserSettingsDialog _userSettingsDialog;
        private readonly IStringLocalizer<BotMessages> _localizer;

        public MainDialog(UserSettingsDialog userSettingsDialog, IStringLocalizer<BotMessages> localizer)
            : base(nameof(MainDialog))
        {
            _userSettingsDialog = userSettingsDialog;
            _localizer = localizer;

            AddDialog(_userSettingsDialog);
            AddDialog(new WaterfallDialog($"{nameof(MainDialog)}.{nameof(WaterfallDialog)}",
                new WaterfallStep[]
                {
                    ProcessInputStep
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
                    var message = _localizer[ResourcesKeys.UnknownCommand];

                    await stepContext.Context.SendActivityAsync(MessageFactory.Text(message), cancellationToken);
                    break;
            }

            return await stepContext.EndDialogAsync(cancellationToken: cancellationToken);
        }
    }
}
