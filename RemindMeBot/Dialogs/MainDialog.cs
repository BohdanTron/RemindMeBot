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
        private readonly IStateService _stateService;

        private readonly UserSettingsDialog _userSettingsDialog;
        private readonly ChangeUserSettingsDialog _changeUserSettingsDialog;
        private readonly AddReminderDialog _addReminderDialog;

        private readonly IStringLocalizer<BotMessages> _localizer;

        public MainDialog(
            IStateService stateService,
            UserSettingsDialog userSettingsDialog,
            ChangeUserSettingsDialog changeUserSettingsDialog,
            AddReminderDialog addReminderDialog,
            IStringLocalizer<BotMessages> localizer) : base(nameof(MainDialog))
        {
            _stateService = stateService;
            
            _addReminderDialog = addReminderDialog;
            _userSettingsDialog = userSettingsDialog;
            _changeUserSettingsDialog = changeUserSettingsDialog;

            _localizer = localizer;

            AddDialog(_userSettingsDialog);
            AddDialog(_changeUserSettingsDialog);
            AddDialog(_addReminderDialog);

            AddDialog(new WaterfallDialog($"{nameof(MainDialog)}.{nameof(WaterfallDialog)}",
                new WaterfallStep[]
                {
                    ProcessInputStep
                }));

            InitialDialogId = $"{nameof(MainDialog)}.{nameof(WaterfallDialog)}";
        }

        private async Task<DialogTurnResult> ProcessInputStep(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var input = stepContext.Context.Activity.Text.ToLowerInvariant();

            switch (input)
            {
                case "/start":
                    var userSettings = await _stateService.UserSettingsPropertyAccessor.GetAsync(stepContext.Context,
                        () => new UserSettings(), cancellationToken);

                    if (userSettings?.TimeZone is null)
                    {
                        return await stepContext.BeginDialogAsync(_userSettingsDialog.Id, cancellationToken: cancellationToken);
                    }

                    await stepContext.Context.SendActivityAsync(MessageFactory.Text("What to remind you about?"), cancellationToken);
                    return await stepContext.EndDialogAsync(cancellationToken: cancellationToken);

                case "/my-settings":
                    return await stepContext.BeginDialogAsync(_changeUserSettingsDialog.Id, cancellationToken: cancellationToken);

                case "/add-reminder":
                    return await stepContext.BeginDialogAsync(_addReminderDialog.Id, cancellationToken: cancellationToken);

                case "/cancel":
                    var noActiveOperations = _localizer[ResourceKeys.NoActiveOperations];
                    await stepContext.Context.SendActivityAsync(
                        MessageFactory.Text(noActiveOperations, noActiveOperations), cancellationToken);

                    return await stepContext.EndDialogAsync(cancellationToken: cancellationToken);

                default:
                    var unknownCommand = _localizer[ResourceKeys.UnknownCommand];

                    await stepContext.Context.SendActivityAsync(MessageFactory.Text(unknownCommand, unknownCommand), cancellationToken);

                    return await stepContext.EndDialogAsync(cancellationToken: cancellationToken);
            }
        }
    }
}
