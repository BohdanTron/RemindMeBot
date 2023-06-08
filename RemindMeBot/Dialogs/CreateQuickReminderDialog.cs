using System.Globalization;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Localization;
using Newtonsoft.Json;
using RemindMeBot.Models;
using RemindMeBot.Resources;
using RemindMeBot.Services;
using RemindMeBot.Services.Recognizers;

namespace RemindMeBot.Dialogs
{
    public class CreateQuickReminderDialog : CancelDialog
    {
        private readonly IStateService _stateService;
        private readonly IClock _clock;

        private readonly ReminderTableService _reminderTableService;
        private readonly ReminderQueueService _reminderQueueService;

        private readonly ReminderRecognizersFactory _recognizersFactory;
        private readonly RepeatedIntervalMapper _repeatedIntervalMapper;

        private readonly IStringLocalizer<BotMessages> _localizer;

        public CreateQuickReminderDialog(
            IStateService stateService,
            IClock clock,
            ReminderTableService reminderTableService,
            ReminderQueueService reminderQueueService,
            ReminderRecognizersFactory recognizersFactory,
            RepeatedIntervalMapper repeatedIntervalMapper,
            IStringLocalizer<BotMessages> localizer) : base(nameof(CreateQuickReminderDialog), stateService, localizer)
        {
            _stateService = stateService;
            _clock = clock;
            _reminderTableService = reminderTableService;
            _reminderQueueService = reminderQueueService;
            _recognizersFactory = recognizersFactory;
            _recognizersFactory = recognizersFactory;
            _repeatedIntervalMapper = repeatedIntervalMapper;
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

            var recognizer = _recognizersFactory.CreateRecognizer(stepContext.Context.Activity.Locale);
            var reminderTask = recognizer.RecognizeReminder(input, localDateTime);

            await stepContext.Context.SendActivityAsync(new Activity { Type = ActivityTypes.Typing }, cancellationToken);

            var reminder = await reminderTask;
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
                RepeatedInterval = reminder.RepeatedInterval,
                TimeZone = userSettings.TimeZone!,
                ConversationReference = JsonConvert.SerializeObject(conversation)
            };

            await _reminderTableService.Add(reminderEntity, cancellationToken);
            await _reminderQueueService.PublishCreatedMessage(reminderEntity, cancellationToken);

            var displayDate = reminder.DateTime.ToString("g", CultureInfo.CurrentCulture);
            var displayInterval = _repeatedIntervalMapper.MapToLocalizedString(reminder.RepeatedInterval);

            var reminderAddedMsg = displayInterval is null
                ? _localizer[ResourceKeys.ReminderAdded, reminder.Text, displayDate]
                : _localizer[ResourceKeys.RepeatedReminderAdded, reminder.Text, displayDate, displayInterval];

            await stepContext.Context.SendActivityAsync(MessageFactory.Text(reminderAddedMsg, reminderAddedMsg), cancellationToken);

            return await stepContext.EndDialogAsync(reminderEntity, cancellationToken);
        }
    }
}
