using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Extensions.Localization;
using RemindMeBot.Models;
using RemindMeBot.Resources;
using RemindMeBot.Services;

namespace RemindMeBot.Dialogs
{
    public class MainDialog : ComponentDialog
    {
        private readonly UserSettingsDialog _userSettingsDialog;
        private readonly StateService _stateService;

        private readonly IStringLocalizer<BotMessages> _localizer;

        public MainDialog(UserSettingsDialog userSettingsDialog, StateService stateService, IStringLocalizer<BotMessages> localizer)
            : base(nameof(MainDialog))
        {
            _userSettingsDialog = userSettingsDialog;
            _stateService = stateService;
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
                    var userSettings = await _stateService.UserSettingsPropertyAccessor.GetAsync(stepContext.Context,
                        () => new UserSettings(), cancellationToken);

                    if (userSettings?.LocalTime is not null)
                    {
                        await stepContext.Context.SendActivityAsync(MessageFactory.Text("What to remind you about?"), cancellationToken);
                    }
                    else
                    {
                        return await stepContext.BeginDialogAsync(_userSettingsDialog.Id, cancellationToken: cancellationToken);
                    }
                    break;

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
