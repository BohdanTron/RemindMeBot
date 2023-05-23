using System.Globalization;
using AdaptiveCards;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Localization;
using RemindMeBot.Models;
using RemindMeBot.Resources;
using RemindMeBot.Services;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

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

            if (conversation.ChannelId != "telegram")
            {
                var card = BuildAdaptiveCard(reminders);
                var msg = MessageFactory.Attachment(new List<Attachment>
                    { new() { ContentType = AdaptiveCard.ContentType, Content = card } });

                await stepContext.Context.SendActivityAsync(msg, cancellationToken);

                return new DialogTurnResult(DialogTurnStatus.Waiting);
            }

            var reminderList = string.Join("\n",
                reminders.Select((reminder, index) =>
                {
                    var date = DateTime.ParseExact(reminder.DueDateTimeLocal, "G", CultureInfo.InvariantCulture, DateTimeStyles.None);

                    return $"{index + 1}\\) *{reminder.Text}* \n📅  {date.ToString("g", CultureInfo.CurrentCulture)}\n";
                }));

            var reminderButtons = reminders
                .Select((reminder, index) => new { Index = index, Reminder = reminder })
                .GroupBy(x => x.Index / 5)
                .Select(g => g.Select(x => InlineKeyboardButton.WithCallbackData($"🗑️ {x.Index + 1}", x.Reminder.RowKey)))
                .ToArray();

            var reply = _localizer[ResourceKeys.RemindersList];
            await _telegramBotClient.SendTextMessageAsync(
                chatId: conversation.Conversation.Id,
                text: $"{reply}\n\n{reminderList}",
                parseMode: ParseMode.MarkdownV2,
                replyMarkup: new InlineKeyboardMarkup(reminderButtons),
                cancellationToken: cancellationToken);

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

        private AdaptiveCard BuildAdaptiveCard(List<ReminderEntity> reminders)
        {
            var card = new AdaptiveCard(new AdaptiveSchemaVersion(1, 0))
            {
                Body = new List<AdaptiveElement>
                {
                    new AdaptiveTextBlock { Text = _localizer[ResourceKeys.RemindersList], Weight = AdaptiveTextWeight.Bolder }
                },
                Actions = reminders.Select((reminder, index) =>
                {
                    var date = DateTime.ParseExact(reminder.DueDateTimeLocal, "G", CultureInfo.InvariantCulture,
                        DateTimeStyles.None);
                    return new AdaptiveSubmitAction
                    {
                        Title = $"{index + 1}. {reminder.Text} - {date.ToString("g", CultureInfo.CurrentCulture)}",
                        Data = reminder.RowKey
                    };
                }).Cast<AdaptiveAction>().ToList()
            };

            return card;
        }
    }

    public record RemindersListDialogResult
    {
        public bool ItemDeleted { get; init; }
    }
}
