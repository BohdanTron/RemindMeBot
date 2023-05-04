using System.Globalization;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Dialogs.Choices;
using Microsoft.Extensions.Localization;
using RemindMeBot.Models;
using RemindMeBot.Resources;
using RemindMeBot.Services;
using DateTimePrompt = RemindMeBot.Dialogs.Prompts.DateTimePrompt;

namespace RemindMeBot.Dialogs
{
    public class AddReminderDialog : CancelDialog
    {
        private readonly IStateService _stateService;
        private readonly IDateTimeConverter _dateTimeConverter;
        private readonly IStringLocalizer<BotMessages> _localizer;

        public AddReminderDialog(
            IStateService stateService,
            ITranslationService translationService,
            IDateTimeConverter dateTimeConverter,
            IStringLocalizer<BotMessages> localizer)
            : base(nameof(AddReminderDialog), localizer)
        {
            _stateService = stateService;
            _dateTimeConverter = dateTimeConverter;
            _localizer = localizer;

            AddDialog(new TextPrompt($"{nameof(AddReminderDialog)}.reminderText"));
            AddDialog(new DateTimePrompt($"{nameof(AddReminderDialog)}.reminderDate", translationService));
            AddDialog(new ChoicePrompt($"{nameof(AddReminderDialog)}.whetherToRepeatReminder"));
            AddDialog(new ChoicePrompt($"{nameof(AddReminderDialog)}.repeatInterval"));

            AddDialog(new WaterfallDialog($"{nameof(AddReminderDialog)}.main",
                new WaterfallStep[]
                {
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

            var recognizedDate = RecognizeDate(dateTimeResolutions, userSettings);
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
            var date = (DateTime) stepContext.Values["reminderDate"];

            var shouldRepeat = (bool) stepContext.Values["shouldRepeat"];
            var repeatInterval = ((FoundChoice) stepContext.Result)?.Value;

            var reminder = new Reminder
            {
                Text = text,
                Date = date,
                ShouldRepeat = shouldRepeat,
                RepeatInterval = repeatInterval
            };

            // TODO: Save gathered info to the storage

            var reminderAddedMsg = shouldRepeat
                ? _localizer[ResourceKeys.RepeatedReminderAdded, text, date, repeatInterval!]
                : _localizer[ResourceKeys.ReminderAdded, text, date];

            await stepContext.Context.SendActivityAsync(MessageFactory.Text(reminderAddedMsg, reminderAddedMsg), cancellationToken);

            return await stepContext.EndDialogAsync(reminder, cancellationToken);
        }

        private DateTime? RecognizeDate(List<DateTimeResolution>? dateTimeResolutions, UserSettings userSettings)
        {
            if (dateTimeResolutions is null) return null;

            var currentDate = _dateTimeConverter.ToLocalDateTime(userSettings.TimeZone!);

            foreach (var dateTimeResolution in dateTimeResolutions)
            {
                if (!DateTime.TryParse(dateTimeResolution.Value, CultureInfo.CurrentCulture, DateTimeStyles.NoCurrentDateDefault,
                        out var recognizedDate))
                    continue;

                // Use current date if the only time specified
                if (recognizedDate.Date == default)
                {
                    recognizedDate = new DateTime(currentDate.Year, currentDate.Month, currentDate.Day,
                        recognizedDate.Hour, recognizedDate.Minute, recognizedDate.Second);
                }

                // Ignore past dates and time
                if (DateTime.Compare(currentDate, recognizedDate) > 0)
                    continue;

                return recognizedDate;
            }

            return null;
        }
    }
}
