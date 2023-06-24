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
using static RemindMeBot.Helpers.Constants;

namespace RemindMeBot.Dialogs
{
    public class CreateQuickReminderDialog : CancelDialog
    {
        private readonly IStateService _stateService;
        private readonly IClock _clock;
        private readonly ISpeechTranscriptionService _speechTranscriptionService;

        private readonly ReminderTableService _reminderTableService;
        private readonly ReminderQueueService _reminderQueueService;

        private readonly ReminderRecognizersFactory _recognizersFactory;
        private readonly RepeatedIntervalMapper _repeatedIntervalMapper;

        private readonly IStringLocalizer<BotMessages> _localizer;

        public CreateQuickReminderDialog(
            IStateService stateService,
            IClock clock,
            ISpeechTranscriptionService speechTranscriptionService,
            ReminderTableService reminderTableService,
            ReminderQueueService reminderQueueService,
            ReminderRecognizersFactory recognizersFactory,
            RepeatedIntervalMapper repeatedIntervalMapper,
            IStringLocalizer<BotMessages> localizer) : base(nameof(CreateQuickReminderDialog), stateService, localizer)
        {
            _stateService = stateService;
            _clock = clock;
            _speechTranscriptionService = speechTranscriptionService;
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

        private async Task<DialogTurnResult> ProcessUserInputStep(WaterfallStepContext stepContext,
            CancellationToken cancellationToken)
        {
            var audioTranscript = GetAudioTranscript(stepContext);

            await stepContext.Context.SendActivityAsync(new Activity { Type = ActivityTypes.Typing }, cancellationToken);

            var userSettings = _stateService.UserSettingsPropertyAccessor.GetAsync(stepContext.Context,
                () => new UserSettings(), cancellationToken);

            await stepContext.Context.SendActivityAsync(new Activity { Type = ActivityTypes.Typing }, cancellationToken);

            var input = await audioTranscript ?? stepContext.Context.Activity.Text;
            if (input is null)
            {
                return await ReminderNotRecognized(stepContext, cancellationToken);
            }

            var userTimeZone = (await userSettings).TimeZone!;
            var localDateTime = _clock.GetLocalDateTime(userTimeZone).DateTime;

            var recognizer = _recognizersFactory.CreateRecognizer(stepContext.Context.Activity.Locale);
            var reminderTask = recognizer.RecognizeReminder(input, localDateTime);

            await stepContext.Context.SendActivityAsync(new Activity { Type = ActivityTypes.Typing }, cancellationToken);

            var reminder = await reminderTask;
            if (reminder is null)
            {
                return await ReminderNotRecognized(stepContext, cancellationToken);
            }

            var differenceMinutes = (reminder.DateTime - localDateTime).TotalMinutes;
            if (Math.Ceiling(differenceMinutes) < ReminderSetAheadMinutes)
            {
                var timeConstraintMsg = _localizer[ResourceKeys.ReminderTimeConstraint];
                await stepContext.Context.SendActivityAsync(MessageFactory.Text(timeConstraintMsg, timeConstraintMsg),
                    cancellationToken);

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
                TimeZone = userTimeZone,
                ConversationReference = JsonConvert.SerializeObject(conversation)
            };

            await _reminderTableService.Add(reminderEntity, cancellationToken);
            await _reminderQueueService.PublishCreatedMessage(reminderEntity, cancellationToken);

            var displayDate = reminder.DateTime.ToString("d", CultureInfo.CurrentCulture);
            var displayTime = $"{reminder.DateTime:t}";
            var displayInterval = _repeatedIntervalMapper.MapToLocalizedString(reminder.RepeatedInterval);

            var reminderAddedMsg = displayInterval is null
                ? _localizer[ResourceKeys.ReminderAdded, reminder.Text, displayDate, displayTime]
                : _localizer[ResourceKeys.RepeatedReminderAdded, reminder.Text, displayDate, displayTime, displayInterval];

            await stepContext.Context.SendActivityAsync(MessageFactory.Text(reminderAddedMsg, reminderAddedMsg),
                cancellationToken);

            return await stepContext.EndDialogAsync(reminderEntity, cancellationToken);
        }

        private Task<string?> GetAudioTranscript(WaterfallStepContext stepContext)
        {
            var language = stepContext.Context.Activity.Locale;

            var audio = stepContext.Context.Activity.Attachments?.FirstOrDefault(a => a.ContentType == "audio/ogg");

            return audio is null
                ? Task.FromResult<string?>(null)
                : _speechTranscriptionService.Transcribe(audio.ContentType, audio.ContentUrl, language);
        }

        private async Task<DialogTurnResult> ReminderNotRecognized(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var notRecognizedMsg = _localizer[ResourceKeys.ReminderNotRecognized];
            await stepContext.Context.SendActivityAsync(MessageFactory.Text(notRecognizedMsg, notRecognizedMsg),
                cancellationToken: cancellationToken);

            return await stepContext.EndDialogAsync(cancellationToken: cancellationToken);
        }
    }
}
