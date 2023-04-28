using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Localization;
using RemindMeBot.Resources;

namespace RemindMeBot.Dialogs
{
    public class CancelDialog : ComponentDialog
    {
        private readonly IStringLocalizer<BotMessages> _localizer;

        public CancelDialog(string id, IStringLocalizer<BotMessages> localizer) : base(id) =>
            _localizer = localizer;

        protected override async Task<DialogTurnResult> OnContinueDialogAsync(DialogContext innerDc, CancellationToken cancellationToken = new CancellationToken())
        {
            var canceledResult = await TryCancel(innerDc, cancellationToken);
            if (canceledResult is not null)
            {
                return canceledResult;
            }

            return await base.OnContinueDialogAsync(innerDc, cancellationToken);
        }

        private async Task<DialogTurnResult?> TryCancel(DialogContext innerDc, CancellationToken cancellationToken)
        {
            if (innerDc.Context.Activity.Type != ActivityTypes.Message) return null;

            var input = innerDc.Context.Activity.Text.ToLowerInvariant();

            if (input != "/cancel") return null;

            var cancelMessage = _localizer[ResourceKeys.OperationCancelled].Value;
            await innerDc.Context.SendActivityAsync(MessageFactory.Text(cancelMessage, cancelMessage), cancellationToken);

            return await innerDc.CancelAllDialogsAsync(cancellationToken);
        }
    }
}
