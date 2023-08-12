using System.Globalization;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Dialogs.Choices;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Localization;
using Newtonsoft.Json;
using RemindMeBot.Helpers;
using RemindMeBot.Models;
using RemindMeBot.Resources;
using RemindMeBot.Services;
using static RemindMeBot.Helpers.Constants;
using DateTimePrompt = RemindMeBot.Dialogs.Prompts.DateTimePrompt;

namespace RemindMeBot.Dialogs
{
    public class AddReminderDialog : CancelDialog
    {
        private readonly IStateService _stateService;
        private readonly IClock _clock;

        private readonly ReminderTableService _reminderTableService;
        private readonly ReminderQueueService _reminderQueueService;
        private readonly IDistributedCache _cache;

        private readonly RepeatedIntervalMapper _repeatedIntervalMapper;

        private readonly IStringLocalizer<BotMessages> _localizer;

        public AddReminderDialog(
            IStateService stateService,
            ITranslationService translationService,
            IClock clock,
            ReminderTableService reminderTableService,
            ReminderQueueService reminderQueueService,
            IDistributedCache cache,
            RepeatedIntervalMapper repeatedIntervalMapper,
            IStringLocalizer<BotMessages> localizer) : base(nameof(AddReminderDialog), stateService, localizer)
        {
            _stateService = stateService;
            _clock = clock;
            _reminderTableService = reminderTableService;
            _reminderQueueService = reminderQueueService;
            _cache = cache;
            _repeatedIntervalMapper = repeatedIntervalMapper;
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

        private static async Task<DialogTurnResult> AskToRetryReminderDateStep(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var options = (Dictionary<string, string>) stepContext.Options;

            stepContext.Values["reminderText"] = options["reminderText"];

            var retryMsg = options["retryMsg"];
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

            var userTimeZone = (await _stateService.UserSettingsPropertyAccessor.GetAsync(stepContext.Context,
                () => new UserSettings(), cancellationToken)).TimeZone!;

            var recognizedTime = RecognizeDateTime(dateTimeResolutions, userTimeZone);
            if (recognizedTime is null)
            {
                return await stepContext.ReplaceDialogAsync($"{nameof(AddReminderDialog)}.retryReminderDate",
                    new Dictionary<string, string>
                    {
                        { nameof(reminderText), reminderText },
                        { "retryMsg", _localizer[ResourceKeys.DateNotRecognized] }
                    }, cancellationToken);
            }

            var userLocalTime = _clock.GetLocalDateTime(userTimeZone).DateTime;
            var differenceMinutes = (recognizedTime.Value - userLocalTime).TotalMinutes;

            if (Math.Ceiling(differenceMinutes) < ReminderSetAheadMinutes)
            {
                return await stepContext.ReplaceDialogAsync($"{nameof(AddReminderDialog)}.retryReminderDate",
                    new Dictionary<string, string>
                    {
                        { nameof(reminderText), reminderText },
                        { "retryMsg", _localizer[ResourceKeys.ReminderTimeConstraint] }
                    }, cancellationToken);
            }

            stepContext.Values["reminderDate"] = recognizedTime.Value;

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
            var date = (DateTime) stepContext.Values["reminderDate"];

            var shouldRepeat = (bool) stepContext.Values["shouldRepeat"];
            var repeatInterval = ((FoundChoice) stepContext.Result)?.Value;

            var userSettings = await _stateService.UserSettingsPropertyAccessor.GetAsync(stepContext.Context,
                () => new UserSettings(), cancellationToken);

            var conversation = stepContext.Context.Activity.GetConversationReference();
            var userId = conversation.User.Id;

            var reminder = new ReminderEntity
            {
                PartitionKey = userId,
                RowKey = Guid.NewGuid().ToString(),
                Text = text,
                DueDateTimeLocal = date.ToString("G", CultureInfo.InvariantCulture),
                RepeatedInterval = _repeatedIntervalMapper.MapToEnumFromLocalized(repeatInterval),
                TimeZone = userSettings.TimeZone!,
                ConversationReference = JsonConvert.SerializeObject(conversation)
            };

            await _reminderTableService.Add(reminder, cancellationToken);

            var allReminders = await _reminderTableService.GetList(userId, cancellationToken);
            await _cache.SetRecord(userId, allReminders);

            await _reminderQueueService.PublishCreatedMessage(reminder, cancellationToken);

            var displayDate = date.ToString("d", CultureInfo.CurrentCulture);
            var displayTime = $"{date:t}";

            var reminderAddedMsg = shouldRepeat
                ? _localizer[ResourceKeys.RepeatedReminderAdded, text, displayDate, displayTime, repeatInterval!]
                : _localizer[ResourceKeys.ReminderAdded, text, displayDate, displayTime];
            await stepContext.Context.SendActivityAsync(MessageFactory.Text(reminderAddedMsg, reminderAddedMsg), cancellationToken);

            var creationTipMsg = _localizer[ResourceKeys.ReminderCreationTip];
            await stepContext.Context.SendActivityAsync(MessageFactory.Text(creationTipMsg, creationTipMsg), cancellationToken);

            return await stepContext.EndDialogAsync(reminder, cancellationToken);
        }

        private DateTime? RecognizeDateTime(List<DateTimeResolution>? dateTimeResolutions, string timeZone)
        {
            if (dateTimeResolutions is null) return null;

            var localDateTime = _clock.GetLocalDateTime(timeZone).DateTime;

            foreach (var dateTimeResolution in dateTimeResolutions)
            {
                if (!DateTime.TryParse(dateTimeResolution.Value, CultureInfo.CurrentCulture, DateTimeStyles.NoCurrentDateDefault,
                        out var recognizedDateTime))
                    continue;

                // Use current date if only the time is specified
                if (recognizedDateTime.Date == default)
                {
                    recognizedDateTime = new DateTime(localDateTime.Year, localDateTime.Month, localDateTime.Day,
                        recognizedDateTime.Hour, recognizedDateTime.Minute, recognizedDateTime.Second);
                }

                // Ignore past dates and time
                if (DateTime.Compare(localDateTime, recognizedDateTime) > 0)
                    continue;

                return recognizedDateTime;
            }

            return null;
        }
    }
}
