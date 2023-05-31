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
        private readonly ITranslationService _translationService;
        private readonly IClock _clock;

        private readonly ReminderTableService _reminderTableService;
        private readonly ReminderQueueService _reminderQueueService;

        private readonly IStringLocalizer<BotMessages> _localizer;

        public CreateQuickReminderDialog(
            IStateService stateService,
            ITranslationService translationService,
            IClock clock,
            ReminderTableService reminderTableService,
            ReminderQueueService reminderQueueService,
            IStringLocalizer<BotMessages> localizer) : base(nameof(CreateQuickReminderDialog), stateService, localizer)
        {
            _stateService = stateService;
            _translationService = translationService;
            _clock = clock;
            _reminderTableService = reminderTableService;
            _reminderQueueService = reminderQueueService;
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

            var originalText = stepContext.Context.Activity.Text;
            var input = stepContext.Context.Activity.Locale == "en-US"
                ? originalText
                : await _translationService.Translate(originalText, from: "uk-UA", to: "en-US");

            var localDateTime = _clock.GetLocalDateTime(userSettings.TimeZone!).DateTime;

            var reminder = ReminderRecognizer.Recognize(input, localDateTime);
            if (reminder is null)
            {
                var notRecognizedMsg = _localizer[ResourceKeys.ReminderNotRecognized];
                await stepContext.Context.SendActivityAsync(MessageFactory.Text(notRecognizedMsg, notRecognizedMsg),
                    cancellationToken: cancellationToken);

                return await stepContext.EndDialogAsync(cancellationToken: cancellationToken);
            }

            var conversation = stepContext.Context.Activity.GetConversationReference();
            var reminderText = stepContext.Context.Activity.Locale == "en-US"
                ? reminder.Text
                : originalText;

            var reminderEntity = new ReminderEntity
            {
                PartitionKey = conversation.User.Id,
                RowKey = Guid.NewGuid().ToString(),
                Text = reminderText,
                DueDateTimeLocal = reminder.DateTime.ToString("G", CultureInfo.InvariantCulture),
                RepeatInterval = reminder.Interval,
                TimeZone = userSettings.TimeZone!,
                ConversationReference = JsonConvert.SerializeObject(conversation)
            };

            await _reminderTableService.Add(reminderEntity, cancellationToken);
            await _reminderQueueService.PublishCreatedMessage(reminderEntity, cancellationToken);

            var displayDate = reminder.DateTime.ToString("g", CultureInfo.CurrentCulture);
            var reminderAddedMsg = reminder.Interval is null
                ? _localizer[ResourceKeys.ReminderAdded, reminderText, displayDate]
                : _localizer[ResourceKeys.RepeatedReminderAdded, reminderText, displayDate, reminder.Interval];

            await stepContext.Context.SendActivityAsync(MessageFactory.Text(reminderAddedMsg, reminderAddedMsg), cancellationToken);

            return await stepContext.EndDialogAsync(reminderEntity, cancellationToken);
        }
    }
}
