using System.Globalization;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Dialogs.Choices;
using Microsoft.Extensions.Localization;
using Newtonsoft.Json;
using RemindMeBot.Models;
using RemindMeBot.Resources;
using RemindMeBot.Services;
using DateTimePrompt = RemindMeBot.Dialogs.Prompts.DateTimePrompt;

namespace RemindMeBot.Dialogs
{
    public class AddReminderDialog : CancelDialog
    {
        private readonly IStateService _stateService;
        private readonly IClock _clock;
        
        private readonly ReminderTableService _reminderTableService;
        private readonly ReminderQueueService _reminderQueueService;

        private readonly IStringLocalizer<BotMessages> _localizer;

        public AddReminderDialog(
            IStateService stateService,
            ITranslationService translationService,
            IClock clock,
            ReminderTableService reminderTableService,
            ReminderQueueService reminderQueueService,
            IStringLocalizer<BotMessages> localizer) : base(nameof(AddReminderDialog), stateService, localizer)
        {
            _stateService = stateService;
            _clock = clock;
            _reminderTableService = reminderTableService;
            _reminderQueueService = reminderQueueService;
            _localizer = localizer;

            AddDialog(new TextPrompt($"{nameof(AddReminderDialog)}.reminderText"));
            AddDialog(new DateTimePrompt($"{nameof(AddReminderDialog)}.reminderDate", translationService));
            AddDialog(new ChoicePrompt($"{nameof(AddReminderDialog)}.whetherToRepeatReminder"));
            AddDialog(new ChoicePrompt($"{nameof(AddReminderDialog)}.repeatInterval"));

            AddDialog(new WaterfallDialog($"{nameof(AddReminderDialog)}.main",
                new WaterfallStep[]
                {
                    CheckUserSettingsExistStep,
                    AskForReminderTextStep,
                    AskForReminderDateStep,
                    AskWhetherToRepeatReminderStep,
                    AskForRepeatIntervalStep,
                    SaveReminderStep
                }));

            AddDialog(new WaterfallDialog($"{nameof(AddReminderDialog)}.retryReminderDate",
                new WaterfallStep[]
                {
                    AskToRetryReminderDateStep,
                    AskWhetherToRepeatReminderStep,
                    AskForRepeatIntervalStep,
                    SaveReminderStep
                }));

            InitialDialogId = $"{nameof(AddReminderDialog)}.main";
        }

        private async Task<DialogTurnResult> AskForReminderTextStep(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var askForReminderText = _localizer[ResourceKeys.AskForReminderText];
            return await stepContext.PromptAsync($"{nameof(AddReminderDialog)}.reminderText",
                new PromptOptions
                {
                    Prompt = MessageFactory.Text(askForReminderText, askForReminderText)
                }, cancellationToken);
        }

        private async Task<DialogTurnResult> AskForReminderDateStep(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            stepContext.Values["reminderText"] = (string) stepContext.Result;

            var askForReminderDate = _localizer[ResourceKeys.AskForReminderDate];
            var retryPrompt = _localizer[ResourceKeys.DateNotRecognized];

            return await stepContext.PromptAsync($"{nameof(AddReminderDialog)}.reminderDate",
                new PromptOptions
                {
                    Prompt = MessageFactory.Text(askForReminderDate, askForReminderDate),
                    RetryPrompt = MessageFactory.Text(retryPrompt, retryPrompt)
                }, cancellationToken);
        }

        private async Task<DialogTurnResult> AskToRetryReminderDateStep(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var options = (Dictionary<string, string>) stepContext.Options;

            stepContext.Values["reminderText"] = options["reminderText"];

            var retryMsg = _localizer[ResourceKeys.DateNotRecognized];
            return await stepContext.PromptAsync($"{nameof(AddReminderDialog)}.reminderDate",
                new PromptOptions
                {
                    Prompt = MessageFactory.Text(retryMsg, retryMsg),
                    RetryPrompt = MessageFactory.Text(retryMsg, retryMsg),
                }, cancellationToken);
        }

        private async Task<DialogTurnResult> AskWhetherToRepeatReminderStep(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var reminderText = (string) stepContext.Values["reminderText"];
            var dateTimeResolutions = (List<DateTimeResolution>?) stepContext.Result;

            var userSettings = await _stateService.UserSettingsPropertyAccessor.GetAsync(stepContext.Context,
                () => new UserSettings(), cancellationToken);

            var recognizedDate = RecognizeDate(dateTimeResolutions, userSettings.TimeZone!);
            if (recognizedDate is null)
            {
                return await stepContext.ReplaceDialogAsync($"{nameof(AddReminderDialog)}.retryReminderDate",
                    new Dictionary<string, string>
                    {
                        { nameof(reminderText), reminderText }
                    }, cancellationToken);
            }

            stepContext.Values["reminderDate"] = recognizedDate.Value;

            var askWhetherRepeat = _localizer[ResourceKeys.AskWhetherToRepeatReminder];
            var retryPrompt = _localizer[ResourceKeys.OptionListRetryPrompt];

            return await stepContext.PromptAsync($"{nameof(AddReminderDialog)}.whetherToRepeatReminder",
                new PromptOptions
                {
                    Prompt = MessageFactory.Text(askWhetherRepeat, askWhetherRepeat),
                    Choices = ChoiceFactory.ToChoices(new List<string> { _localizer[ResourceKeys.Yes], _localizer[ResourceKeys.No] }),
                    RetryPrompt = MessageFactory.Text(retryPrompt, retryPrompt)
                }, cancellationToken);
        }

        private async Task<DialogTurnResult> AskForRepeatIntervalStep(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var repeatResponse = ((FoundChoice) stepContext.Result).Value;

            var shouldRepeat = string.Equals(repeatResponse, _localizer[ResourceKeys.Yes], StringComparison.CurrentCulture);

            stepContext.Values["shouldRepeat"] = shouldRepeat;

            if (!shouldRepeat)
            {
                return await stepContext.NextAsync(null, cancellationToken);
            }

            var askForRepeatInterval = _localizer[ResourceKeys.AskForRepeatInterval];
            var retryPrompt = _localizer[ResourceKeys.OptionListRetryPrompt];

            return await stepContext.PromptAsync($"{nameof(AddReminderDialog)}.repeatInterval",
                new PromptOptions
                {
                    Prompt = MessageFactory.Text(askForRepeatInterval, askForRepeatInterval),
                    RetryPrompt = MessageFactory.Text(retryPrompt, retryPrompt),
                    Choices = ChoiceFactory.ToChoices(new List<string>
                    {
                        _localizer[ResourceKeys.Daily],
                        _localizer[ResourceKeys.Weekly],
                        _localizer[ResourceKeys.Monthly],
                        _localizer[ResourceKeys.Yearly]
                    })
                }, cancellationToken);
        }

        private async Task<DialogTurnResult> SaveReminderStep(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var text = (string) stepContext.Values["reminderText"];
            var date = (DateTimeOffset) stepContext.Values["reminderDate"];

            var shouldRepeat = (bool) stepContext.Values["shouldRepeat"];
            var repeatInterval = ((FoundChoice) stepContext.Result)?.Value;

            var userSettings = await _stateService.UserSettingsPropertyAccessor.GetAsync(stepContext.Context,
                () => new UserSettings(), cancellationToken);

            var conversation = stepContext.Context.Activity.GetConversationReference();
            var reminder = new ReminderEntity
            {
                PartitionKey = conversation.User.Id,
                RowKey = $"{conversation.Conversation.Id}_{Guid.NewGuid()}",
                Text = text,
                DueDateTimeLocal = date.ToString("G", CultureInfo.InvariantCulture),
                RepeatInterval = repeatInterval,
                TimeZone = userSettings.TimeZone!,
                ConversationReference = JsonConvert.SerializeObject(conversation)
            };

            await _reminderTableService.Add(reminder, cancellationToken);
            await _reminderQueueService.PublishCreatedMessage(reminder.PartitionKey, reminder.RowKey, cancellationToken);

            var displayDate = date.ToString("g", CultureInfo.CurrentCulture);
            var reminderAddedMsg = shouldRepeat
                ? _localizer[ResourceKeys.RepeatedReminderAdded, text, displayDate, repeatInterval!]
                : _localizer[ResourceKeys.ReminderAdded, text, displayDate];

            await stepContext.Context.SendActivityAsync(MessageFactory.Text(reminderAddedMsg, reminderAddedMsg), cancellationToken);

            return await stepContext.EndDialogAsync(reminder, cancellationToken);
        }

        private DateTimeOffset? RecognizeDate(List<DateTimeResolution>? dateTimeResolutions, string timeZone)
        {
            if (dateTimeResolutions is null) return null;

            var localDateTimeOffset = _clock.GetLocalDateTime(timeZone);

            foreach (var dateTimeResolution in dateTimeResolutions)
            {
                if (!DateTime.TryParse(dateTimeResolution.Value, CultureInfo.CurrentCulture, DateTimeStyles.NoCurrentDateDefault,
                        out var recognizedDateTime))
                    continue;

                // Use current date if only the time is specified
                if (recognizedDateTime.Date == default)
                {
                    recognizedDateTime = new DateTime(localDateTimeOffset.Year, localDateTimeOffset.Month, localDateTimeOffset.Day,
                        recognizedDateTime.Hour, recognizedDateTime.Minute, recognizedDateTime.Second);
                }

                var recognizedDateTimeOffset = new DateTimeOffset(recognizedDateTime, localDateTimeOffset.Offset);

                // Ignore past dates and time
                if (DateTimeOffset.Compare(localDateTimeOffset, recognizedDateTimeOffset) > 0)
                    continue;

                return recognizedDateTimeOffset;
            }

            return null;
        }
    }
}
