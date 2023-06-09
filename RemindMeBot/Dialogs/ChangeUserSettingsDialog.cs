using System.Globalization;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Dialogs.Choices;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Localization;
using RemindMeBot.Helpers;
using RemindMeBot.Models;
using RemindMeBot.Resources;
using RemindMeBot.Services;

namespace RemindMeBot.Dialogs
{
    public class ChangeUserSettingsDialog : CancelDialog
    {
        private readonly IStateService _stateService;
        private readonly ILocationService _locationService;
        private readonly ITranslationService _translationService;
        private readonly IClock _clock;
        private readonly ReminderTableService _reminderTableService;
        private readonly IStringLocalizer<BotMessages> _localizer;

        public ChangeUserSettingsDialog(
            IStateService stateService,
            ILocationService locationService,
            ITranslationService translationService,
            IClock clock,
            ReminderTableService reminderTableService,
            IStringLocalizer<BotMessages> localizer) : base(nameof(ChangeUserSettingsDialog), stateService, localizer)
        {
            _stateService = stateService;
            _locationService = locationService;
            _translationService = translationService;
            _clock = clock;
            _reminderTableService = reminderTableService;
            _localizer = localizer;

            AddDialog(new ChoicePrompt($"{nameof(ChangeUserSettingsDialog)}.settingToChange"));
            AddDialog(new ChoicePrompt($"{nameof(ChangeUserSettingsDialog)}.language"));
            AddDialog(new TextPrompt($"{nameof(ChangeUserSettingsDialog)}.location"));

            AddDialog(new WaterfallDialog($"{nameof(ChangeUserSettingsDialog)}.main",
                new WaterfallStep[]
                {
                    CheckUserSettingsExistStep,
                    ShowCurrentSettingsStep,
                    AskSettingToChangeStep,
                    AskForNewValueStep,
                    SaveChangedSettingsStep
                }));

            AddDialog(new WaterfallDialog($"{nameof(ChangeUserSettingsDialog)}.retryLocation",
                new WaterfallStep[]
                {
                    AskForNewLocationStep,
                    SaveChangedSettingsStep
                }));

            InitialDialogId = $"{nameof(ChangeUserSettingsDialog)}.main";
        }

        private async Task<DialogTurnResult> ShowCurrentSettingsStep(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var userSettings = await _stateService.UserSettingsPropertyAccessor.GetAsync(stepContext.Context,
                () => new UserSettings(), cancellationToken);

            await DisplayCurrentUserSettings(stepContext, userSettings, cancellationToken);

            return await stepContext.NextAsync(null, cancellationToken);
        }

        private Task<DialogTurnResult> AskSettingToChangeStep(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var language = _localizer[ResourceKeys.Language];
            var location = _localizer[ResourceKeys.Location];

            var askSettingToChange = _localizer[ResourceKeys.AskSettingToChange];
            var retryPrompt = _localizer[ResourceKeys.OptionListRetryPrompt];

            return stepContext.PromptAsync($"{nameof(ChangeUserSettingsDialog)}.settingToChange",
                new PromptOptions
                {
                    Prompt = MessageFactory.Text(askSettingToChange, askSettingToChange),
                    Choices = ChoiceFactory.ToChoices(new List<string> { language, location }),
                    RetryPrompt = MessageFactory.Text(retryPrompt, retryPrompt)
                }, cancellationToken);
        }

        private Task<DialogTurnResult> AskForNewValueStep(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var language = _localizer[ResourceKeys.Language];
            var location = _localizer[ResourceKeys.Location];

            var settingToChange = ((FoundChoice) stepContext.Result).Value;
            stepContext.Values["settingToChange"] = settingToChange;

            if (settingToChange == language)
            {
                var askForLanguage = _localizer[ResourceKeys.AskForLanguage];
                var retryPrompt = _localizer[ResourceKeys.OptionListRetryPrompt];

                return stepContext.PromptAsync($"{nameof(ChangeUserSettingsDialog)}.language",
                    new PromptOptions
                    {
                        Prompt = MessageFactory.Text(askForLanguage),
                        Choices = ChoiceFactory.ToChoices(new List<string> { "English", "Українська" }),
                        RetryPrompt = MessageFactory.Text(retryPrompt, retryPrompt)
                    }, cancellationToken);
            }
            if (settingToChange == location)
            {
                var askForLocation = _localizer[ResourceKeys.AskToChangeLocation];
                var retryPrompt = _localizer[ResourceKeys.AskToRetryLocation];

                return stepContext.PromptAsync($"{nameof(ChangeUserSettingsDialog)}.location",
                    new PromptOptions
                    {
                        Prompt = MessageFactory.Text(askForLocation, askForLocation),
                        RetryPrompt = MessageFactory.Text(retryPrompt, retryPrompt)
                    }, cancellationToken);
            }

            return stepContext.EndDialogAsync(null, cancellationToken);
        }

        private async Task<DialogTurnResult> SaveChangedSettingsStep(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var language = _localizer[ResourceKeys.Language];
            var location = _localizer[ResourceKeys.Location];

            var settingToChange = (string) stepContext.Values["settingToChange"];

            if (settingToChange == language)
            {
                await ChangeUserLanguage(stepContext, cancellationToken);
            }
            if (settingToChange == location)
            {
                var locationChanged = await ChangeUserLocation(stepContext, cancellationToken);
                if (!locationChanged)
                {
                    var askToRetryLocation = _localizer[ResourceKeys.AskToRetryLocation];
                    return await stepContext.ReplaceDialogAsync($"{nameof(ChangeUserSettingsDialog)}.retryLocation",
                        new PromptOptions
                        {
                            Prompt = MessageFactory.Text(askToRetryLocation, askToRetryLocation),
                            RetryPrompt = MessageFactory.Text(askToRetryLocation, askToRetryLocation)
                        }, cancellationToken);
                }
            }

            var userSettings = await _stateService.UserSettingsPropertyAccessor.GetAsync(stepContext.Context,
                () => new UserSettings(), cancellationToken);

            CultureHelper.SetCurrentCulture(userSettings.Culture!);

            var settingsChangedMsg = _localizer[ResourceKeys.UserSettingsHaveBeenChanged];
            await stepContext.Context.SendActivityAsync(MessageFactory.Text(settingsChangedMsg, settingsChangedMsg), cancellationToken);

            await DisplayCurrentUserSettings(stepContext, userSettings, cancellationToken);

            return await stepContext.EndDialogAsync(userSettings, cancellationToken);
        }

        private async Task<DialogTurnResult> AskForNewLocationStep(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            stepContext.Values["settingToChange"] = _localizer[ResourceKeys.Location].Value;

            var retryMessage = _localizer[ResourceKeys.AskToRetryLocation];
            return await stepContext.PromptAsync($"{nameof(ChangeUserSettingsDialog)}.location",
                new PromptOptions
                {
                    Prompt = MessageFactory.Text(retryMessage, retryMessage),
                    RetryPrompt = MessageFactory.Text(retryMessage, retryMessage)
                }, cancellationToken);
        }

        private async Task ChangeUserLanguage(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var userSettings = await _stateService.UserSettingsPropertyAccessor.GetAsync(stepContext.Context,
                () => new UserSettings(), cancellationToken);

            var languageChoice = ((FoundChoice) stepContext.Result).Value;

            userSettings.Language = languageChoice;
            userSettings.Culture = CultureHelper.GetCulture(languageChoice);

            await _stateService.UserSettingsPropertyAccessor.SetAsync(stepContext.Context, userSettings, cancellationToken);
        }

        private async Task<bool> ChangeUserLocation(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var userSettings = await _stateService.UserSettingsPropertyAccessor.GetAsync(stepContext.Context,
                () => new UserSettings(), cancellationToken);

            var locationChoice = (string) stepContext.Result;
            var translatedLocation = userSettings.Culture == "uk-UA"
                ? await _translationService.Translate(locationChoice, from: "uk-UA", to: "en-US")
                : locationChoice;

            var preciseLocationTask = _locationService.GetLocation(translatedLocation);

            await stepContext.Context.SendActivityAsync(new Activity { Type = ActivityTypes.Typing }, cancellationToken);

            var preciseLocation = await preciseLocationTask;
            if (preciseLocation is null)
            {
                return false;
            }

            var oldTimeZone = userSettings.TimeZone!;
            var newTimeZone = preciseLocation.TimeZoneId;

            if (oldTimeZone != newTimeZone)
            {
                var conversation = stepContext.Context.Activity.GetConversationReference();
                var reminders = await _reminderTableService.GetList(conversation.User.Id, cancellationToken);

                foreach (var reminder in reminders)
                {
                    var oldDateTime = DateTime.ParseExact(reminder.DueDateTimeLocal, "G", CultureInfo.InvariantCulture, DateTimeStyles.None);
                    var newDateTime = _clock.ToAnotherTimeZone(oldDateTime, oldTimeZone, newTimeZone);

                    reminder.TimeZone = newTimeZone;
                    reminder.DueDateTimeLocal = newDateTime.ToString("G", CultureInfo.InvariantCulture);
                }

                await _reminderTableService.BulkUpdate(conversation.User.Id, reminders, cancellationToken);
            }

            userSettings.Location = $"{preciseLocation.City}, {preciseLocation.Country}";
            userSettings.TimeZone = newTimeZone;

            await _stateService.UserSettingsPropertyAccessor.SetAsync(stepContext.Context, userSettings, cancellationToken);

            return true;
        }

        private async Task DisplayCurrentUserSettings(WaterfallStepContext stepContext, UserSettings userSettings, CancellationToken cancellationToken)
        {
            var (location, language, timeZone) = userSettings;

            if (location is null || language is null || timeZone is null)
            {
                throw new ArgumentNullException(nameof(userSettings));
            }

            var localTime = _clock.GetLocalDateTime(timeZone).ToString("t", CultureInfo.CurrentCulture);

            var message = _localizer[ResourceKeys.UserCurrentSettings, language, location, timeZone, localTime];

            await stepContext.Context.SendActivityAsync(MessageFactory.Text(message, message), cancellationToken);
        }
    }
}
