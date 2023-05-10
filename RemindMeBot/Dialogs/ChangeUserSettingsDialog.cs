using System.Globalization;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Dialogs.Choices;
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
        private readonly IStringLocalizer<BotMessages> _localizer;

        public ChangeUserSettingsDialog(
            IStateService stateService,
            ILocationService locationService,
            ITranslationService translationService,
            IClock clock,
            IStringLocalizer<BotMessages> localizer) : base(nameof(ChangeUserSettingsDialog), stateService, localizer)
        {
            _stateService = stateService;
            _locationService = locationService;
            _translationService = translationService;
            _clock = clock;
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
                var askForLocation = _localizer[ResourceKeys.AskForLocation];
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

            var userSettings = await _stateService.UserSettingsPropertyAccessor.GetAsync(stepContext.Context,
                () => new UserSettings(), cancellationToken);

            if (settingToChange == language)
            {
                var languageChoice = ((FoundChoice) stepContext.Result).Value;

                var culture = CultureHelper.GetCulture(languageChoice);
                CultureHelper.SetCurrentCulture(culture);

                userSettings.Language = languageChoice;
                userSettings.Culture = culture;
            }
            if (settingToChange == location)
            {
                var locationInput = (string) stepContext.Result;
                var translatedLocation = userSettings.Culture == "uk-UA"
                    ? await _translationService.Translate(locationInput, from: "uk-UA", to: "en-US")
                    : locationInput;

                var preciseLocation = await _locationService.GetLocation(translatedLocation);

                if (preciseLocation is null)
                {
                    var askToRetryLocation = _localizer[ResourceKeys.AskToRetryLocation];
                    return await stepContext.ReplaceDialogAsync($"{nameof(ChangeUserSettingsDialog)}.retryLocation",
                        new PromptOptions
                        {
                            Prompt = MessageFactory.Text(askToRetryLocation, askToRetryLocation),
                            RetryPrompt = MessageFactory.Text(askToRetryLocation, askToRetryLocation)
                        }, cancellationToken);
                }

                userSettings.Location = $"{preciseLocation.City}, {preciseLocation.Country}";
                userSettings.TimeZone = preciseLocation.TimeZoneId;
            }

            await _stateService.UserSettingsPropertyAccessor.SetAsync(stepContext.Context, userSettings, cancellationToken);

            var settingsChanged = _localizer[ResourceKeys.UserSettingsHaveBeenChanged];
            await stepContext.Context.SendActivityAsync(MessageFactory.Text(settingsChanged, settingsChanged), cancellationToken);

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
