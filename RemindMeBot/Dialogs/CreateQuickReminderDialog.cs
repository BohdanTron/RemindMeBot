using System.Globalization;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Extensions.Localization;
using Newtonsoft.Json;
using RemindMeBot.Helpers;
using RemindMeBot.Models;
using RemindMeBot.Resources;
using RemindMeBot.Services;

namespace RemindMeBot.Dialogs
{
    public class CreateQuickReminderDialog : CancelDialog
    {
        private readonly IStateService _stateService;
        private readonly IClock _clock;

        private readonly ReminderTableService _reminderTableService;
        private readonly ReminderQueueService _reminderQueueService;

        private readonly OpenAiService _openAiService;

        private readonly IStringLocalizer<BotMessages> _localizer;

        public CreateQuickReminderDialog(
            IStateService stateService,
            IClock clock,
            ReminderTableService reminderTableService,
            ReminderQueueService reminderQueueService,
            OpenAiService openAiService,
            IStringLocalizer<BotMessages> localizer) : base(nameof(CreateQuickReminderDialog), stateService, localizer)
        {
            _stateService = stateService;
            _clock = clock;
            _reminderTableService = reminderTableService;
            _reminderQueueService = reminderQueueService;
            _openAiService = openAiService;
            _localizer = localizer;

            AddDialog(new WaterfallDialog($"{nameof(CreateQuickReminderDialog)}.main",
                new WaterfallStep[]
                {
                    CheckUserSettingsExistStep,
                    ProcessUserInputStep,
                }));

            InitialDialogId = $"{nameof(CreateQuickReminderDialog)}.main";
        }

        private async Task<DialogTurnResult> ProcessUserInputStep(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var userSettings = await _stateService.UserSettingsPropertyAccessor.GetAsync(stepContext.Context,
                () => new UserSettings(), cancellationToken);

            var input = stepContext.Context.Activity.Text;
            var localDateTime = _clock.GetLocalDateTime(userSettings.TimeZone!).DateTime;

            var reminder = stepContext.Context.Activity.Locale == "uk-UA"
                ? await _openAiService.RecognizeReminder(input, localDateTime)
                : ReminderRecognizer.Recognize(input, localDateTime);
            
            if (reminder is null)
            {
                var notRecognizedMsg = _localizer[ResourceKeys.ReminderNotRecognized];
                await stepContext.Context.SendActivityAsync(MessageFactory.Text(notRecognizedMsg, notRecognizedMsg),
                    cancellationToken: cancellationToken);

                return await stepContext.EndDialogAsync(cancellationToken: cancellationToken);
            }
            var conversation = stepContext.Context.Activity.GetConversationReference();
            var reminderEntity = new ReminderEntity
            {
                PartitionKey = conversation.User.Id,
                RowKey = Guid.NewGuid().ToString(),
                Text = reminder.Text,
                DueDateTimeLocal = reminder.DateTime.ToString("G", CultureInfo.InvariantCulture),
                RepeatInterval = reminder.RepeatedInterval,
                TimeZone = userSettings.TimeZone!,
                ConversationReference = JsonConvert.SerializeObject(conversation)
            };

            await _reminderTableService.Add(reminderEntity, cancellationToken);
            await _reminderQueueService.PublishCreatedMessage(reminderEntity, cancellationToken);

            var displayDate = reminder.DateTime.ToString("g", CultureInfo.CurrentCulture);
            var displayInterval = reminder.RepeatedInterval switch
            {
                "daily" => _localizer[ResourceKeys.Daily],
                "weekly" => _localizer[ResourceKeys.Weekly],
                "monthly" => _localizer[ResourceKeys.Monthly],
                "yearly" => _localizer[ResourceKeys.Yearly],
                _ => null
            };

            var reminderAddedMsg = displayInterval is null
                ? _localizer[ResourceKeys.ReminderAdded, reminder.Text, displayDate]
                : _localizer[ResourceKeys.RepeatedReminderAdded, reminder.Text, displayDate, displayInterval];

            await stepContext.Context.SendActivityAsync(MessageFactory.Text(reminderAddedMsg, reminderAddedMsg), cancellationToken);

            return await stepContext.EndDialogAsync(reminderEntity, cancellationToken);
        }
    }
}
