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
        private readonly RemindersListDialog _remindersListDialog;
        private readonly CreateQuickReminderDialog _createQuickReminderDialog;

        private readonly IStringLocalizer<BotMessages> _localizer;

        public MainDialog(
            IStateService stateService,
            UserSettingsDialog userSettingsDialog,
            ChangeUserSettingsDialog changeUserSettingsDialog,
            AddReminderDialog addReminderDialog,
            RemindersListDialog remindersListDialog,
            CreateQuickReminderDialog createQuickReminderDialog,
            IStringLocalizer<BotMessages> localizer) : base(nameof(MainDialog))
        {
            _stateService = stateService;

            _addReminderDialog = addReminderDialog;
            _remindersListDialog = remindersListDialog;
            _userSettingsDialog = userSettingsDialog;
            _changeUserSettingsDialog = changeUserSettingsDialog;
            _createQuickReminderDialog = createQuickReminderDialog;

            _localizer = localizer;

            AddDialog(_userSettingsDialog);
            AddDialog(_changeUserSettingsDialog);
            AddDialog(_addReminderDialog);
            AddDialog(_remindersListDialog);
            AddDialog(_createQuickReminderDialog);

            AddDialog(new WaterfallDialog($"{nameof(MainDialog)}.{nameof(WaterfallDialog)}",
                new WaterfallStep[]
                {
                    ProcessInputStep,
                    ProcessResultStep
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

                    var whatToRemindMsg = _localizer[ResourceKeys.WhatToRemindYouAbout];
                    await stepContext.Context.SendActivityAsync(MessageFactory.Text(whatToRemindMsg, whatToRemindMsg), cancellationToken);

                    return await stepContext.EndDialogAsync(cancellationToken: cancellationToken);

                case "/my-settings":
                    return await stepContext.BeginDialogAsync(_changeUserSettingsDialog.Id, cancellationToken: cancellationToken);

                case "/add-reminder":
                    return await stepContext.BeginDialogAsync(_addReminderDialog.Id, cancellationToken: cancellationToken);

                case "/list":
                    return await stepContext.BeginDialogAsync(_remindersListDialog.Id, cancellationToken: cancellationToken);

                case "/cancel":
                    var noActiveOperationsMsg = _localizer[ResourceKeys.NoActiveOperations];
                    await stepContext.Context.SendActivityAsync(
                        MessageFactory.Text(noActiveOperationsMsg, noActiveOperationsMsg), cancellationToken);

                    return await stepContext.EndDialogAsync(cancellationToken: cancellationToken);

                default:
                    return await stepContext.BeginDialogAsync(_createQuickReminderDialog.Id, cancellationToken: cancellationToken);
            }
        }

        private async Task<DialogTurnResult> ProcessResultStep(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            if (stepContext.Result is RemindersListDialogResult { ItemDeleted: false })
            {
                if (stepContext.Context.Activity.Text.StartsWith("/"))
                {
                    return await stepContext.ReplaceDialogAsync(Id, cancellationToken: cancellationToken);
                }

                return await stepContext.BeginDialogAsync(_createQuickReminderDialog.Id, cancellationToken: cancellationToken);
            }

            return await stepContext.EndDialogAsync(cancellationToken: cancellationToken);
        }
    }
}
