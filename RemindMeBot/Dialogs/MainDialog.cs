using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using RemindMeBot.Helpers;
using RemindMeBot.Models;
using RemindMeBot.Services;

namespace RemindMeBot.Dialogs
{
    public class MainDialog : ComponentDialog
    {
        private readonly StateService _stateService;
        private readonly UserSettingsDialog _userSettingsDialog;

        public MainDialog(StateService stateService, UserSettingsDialog userSettingsDialog) : base(nameof(MainDialog))
        {
            _stateService = stateService;
            _userSettingsDialog = userSettingsDialog;

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
                    var userSettings = await _stateService.UserSettingsPropertyAccessor.GetAsync(stepContext.Context,
                        () => new UserSettings(), cancellationToken);

                    var message = ResourceKeys.UnknownCommand.ToLocalized(userSettings.Language ?? "en");

                    await stepContext.Context.SendActivityAsync(MessageFactory.Text(message), cancellationToken);
                    break;
            }

            return await stepContext.EndDialogAsync(cancellationToken: cancellationToken);
        }
    }
}
