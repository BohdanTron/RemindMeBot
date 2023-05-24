using System.Globalization;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Localization;
using RemindMeBot.Resources;
using RemindMeBot.Services;
using Telegram.Bot;

namespace RemindMeBot.Dialogs
{
    public class RemindersListDialog : CancelDialog
    {
        private readonly ReminderTableService _reminderTableService;
        private readonly ReminderQueueService _reminderQueueService;
        private readonly TelegramBotClient _telegramBotClient;
        private readonly IStringLocalizer<BotMessages> _localizer;

        public RemindersListDialog(
            IStateService stateService,
            ReminderTableService reminderTableService,
            ReminderQueueService reminderQueueService,
            TelegramBotClient telegramBotClient,
            IStringLocalizer<BotMessages> localizer) : base(nameof(AddReminderDialog), stateService, localizer)
        {
            _reminderTableService = reminderTableService;
            _reminderQueueService = reminderQueueService;
            _telegramBotClient = telegramBotClient;
            _localizer = localizer;

            AddDialog(new WaterfallDialog($"{nameof(RemindersListDialog)}.main",
                new WaterfallStep[]
                {
                    CheckUserSettingsExistStep,
                    ShowRemindersStep,
                    HandleSelectionStep
                }));

            InitialDialogId = $"{nameof(RemindersListDialog)}.main";
        }

        private async Task<DialogTurnResult> ShowRemindersStep(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var conversation = stepContext.Context.Activity.GetConversationReference();

            var reminders = (await _reminderTableService.GetList(conversation.User.Id, cancellationToken))
                .OrderBy(r => r.DueDateTimeLocal)
                .ToList();

            if (!reminders.Any())
            {
                var noRemindersMsg = _localizer[ResourceKeys.NoRemindersCreated];
                await stepContext.Context.SendActivityAsync(MessageFactory.Text(noRemindersMsg, noRemindersMsg), cancellationToken: cancellationToken);

                return await stepContext.EndDialogAsync(cancellationToken: cancellationToken);
            }

            var reminderList = string.Join("\n",
                reminders.Select((reminder, index) =>
                {
                    var date = DateTime.ParseExact(reminder.DueDateTimeLocal, "G", CultureInfo.InvariantCulture, DateTimeStyles.None);

                    return $"{index + 1}) *{reminder.Text}* \n📅  {date.ToString("g", CultureInfo.CurrentCulture)}\n";
                }));

            var reminderListMsg = $"{_localizer[ResourceKeys.RemindersList]}\n\n{reminderList}";

            var reply = MessageFactory.Text(reminderListMsg, reminderListMsg);
            reply.SuggestedActions = new SuggestedActions
            {
                Actions = reminders
                    .Select((item, index) => new CardAction
                    {
                        Title = $"🗑️ {index + 1}",
                        Type = ActionTypes.ImBack,
                        Value = item.RowKey
                    }).ToList()
            };

            await stepContext.Context.SendActivityAsync(reply, cancellationToken);

            return new DialogTurnResult(DialogTurnStatus.Waiting);
        }

        private async Task<DialogTurnResult> HandleSelectionStep(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var conversation = stepContext.Context.Activity.GetConversationReference();

            var rowKey = stepContext.Context.Activity.Text;

            var reminder = await _reminderTableService.Get(conversation.User.Id, rowKey, cancellationToken);
            if (reminder is null)
            {
                return await stepContext.EndDialogAsync(new RemindersListDialogResult
                {
                    ItemDeleted = false
                }, cancellationToken: cancellationToken);
            }

            await _reminderTableService.Delete(conversation.User.Id, rowKey, cancellationToken);
            await _reminderQueueService.PublishDeletedMessage(conversation.User.Id, rowKey, cancellationToken);

            await stepContext.Context.SendActivityAsync("Reminder was deleted", cancellationToken: cancellationToken);

            return await stepContext.ReplaceDialogAsync(Id, cancellationToken: cancellationToken);
        }
    }

    public record RemindersListDialogResult
    {
        public bool ItemDeleted { get; init; }
    }
}
