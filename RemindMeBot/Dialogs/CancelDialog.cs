using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Localization;
using RemindMeBot.Models;
using RemindMeBot.Resources;
using RemindMeBot.Services;

namespace RemindMeBot.Dialogs
{
    public class CancelDialog : ComponentDialog
    {
        private readonly IStateService _stateService;
        private readonly IStringLocalizer<BotMessages> _localizer;

        public CancelDialog(string id, IStateService stateService, IStringLocalizer<BotMessages> localizer) : base(id)
        {
            _stateService = stateService;
            _localizer = localizer;
        }

        protected override async Task<DialogTurnResult> OnContinueDialogAsync(DialogContext innerDc, CancellationToken cancellationToken = new())
        {
            var canceledResult = await TryCancel(innerDc, cancellationToken);
            if (canceledResult is not null)
            {
                return canceledResult;
            }

            return await base.OnContinueDialogAsync(innerDc, cancellationToken);
        }

        protected virtual async Task<DialogTurnResult> CheckUserSettingsExistStep(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var userSettings = await _stateService.UserSettingsPropertyAccessor.GetAsync(stepContext.Context,
                () => new UserSettings(), cancellationToken);

            if (userSettings.TimeZone is not null)
            {
                return await stepContext.NextAsync(userSettings, cancellationToken);
            }

            var message = _localizer[ResourceKeys.NoUserSettings];
            await stepContext.Context.SendActivityAsync(MessageFactory.Text(message, message), cancellationToken);

            return await stepContext.EndDialogAsync(null, cancellationToken);
        }

        private async Task<DialogTurnResult?> TryCancel(DialogContext innerDc, CancellationToken cancellationToken)
        {
            if (innerDc.Context.Activity.Type != ActivityTypes.Message) return null;

            var input = innerDc.Context.Activity.Text.ToLowerInvariant();

            if (input != "/cancel") return null;

            var cancelMessage = _localizer[ResourceKeys.OperationCancelled];
            await innerDc.Context.SendActivityAsync(MessageFactory.Text(cancelMessage, cancelMessage), cancellationToken);

            return await innerDc.CancelAllDialogsAsync(cancellationToken);
        }
    }
}
