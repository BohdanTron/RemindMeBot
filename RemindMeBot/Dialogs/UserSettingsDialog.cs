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
    public class UserSettingsDialog : CancelDialog
    {
        private readonly IStateService _stateService;
        private readonly ITranslationService _translationService;
        private readonly ILocationService _locationService;
        private readonly IClock _clock;

        private readonly IStringLocalizer<BotMessages> _localizer;

        public UserSettingsDialog(
            IStateService stateService,
            ITranslationService translationService,
            ILocationService locationService,
            IClock clock,
            IStringLocalizer<BotMessages> localizer) : base(nameof(UserSettingsDialog), stateService, localizer)
        {
            _stateService = stateService;
            _translationService = translationService;
            _clock = clock;
            _locationService = locationService;
            _localizer = localizer;

            AddDialog(new ChoicePrompt($"{nameof(UserSettingsDialog)}.language"));
            AddDialog(new TextPrompt($"{nameof(UserSettingsDialog)}.location"));

            AddDialog(new WaterfallDialog($"{nameof(UserSettingsDialog)}.main",
                new WaterfallStep[]
                {
                    AskForLanguageStep,
                    AskForLocationStep,
                    SaveUserSettingsStep
                }));

            AddDialog(new WaterfallDialog($"{nameof(UserSettingsDialog)}.retryLocation",
                new WaterfallStep[]
                {
                    AskForLocationStep,
                    SaveUserSettingsStep
                }));

            InitialDialogId = $"{nameof(UserSettingsDialog)}.main";
        }

        private static Task<DialogTurnResult> AskForLanguageStep(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            const string welcomeMsg = "Welcome to the RecallMe chatbot! Please choose your language:";
            const string retryPrompt = "Please choose an option from the list:";

            return stepContext.PromptAsync($"{nameof(UserSettingsDialog)}.language",
                new PromptOptions
                {
                    Prompt = MessageFactory.Text(welcomeMsg, welcomeMsg),
                    Choices = ChoiceFactory.ToChoices(new List<string> { "English", "Українська" }),
                    RetryPrompt = MessageFactory.Text(retryPrompt, retryPrompt)
                }, cancellationToken);
        }

        private async Task<DialogTurnResult> AskForLocationStep(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var options = stepContext.Options as IDictionary<string, object>;

            var language = options is not null && options.TryGetValue("language", out var lang)
                ? (string) lang
                : ((FoundChoice) stepContext.Result).Value;

            var culture = CultureHelper.GetCulture(language);
            CultureHelper.SetCurrentCulture(culture);

            stepContext.Values["language"] = language;
            stepContext.Values["culture"] = culture;

            var askForLocationMsg = _localizer[ResourceKeys.AskForLocation];
            var prompt = options is not null && options.TryGetValue("retryMessage", out var message)
                ? (Activity) message
                : MessageFactory.Text(askForLocationMsg, askForLocationMsg);

            var retryPrompt = _localizer[ResourceKeys.AskToRetryLocation];
            return await stepContext.PromptAsync($"{nameof(UserSettingsDialog)}.location",
                new PromptOptions
                {
                    Prompt = prompt,
                    RetryPrompt = MessageFactory.Text(retryPrompt, retryPrompt)
                }, cancellationToken);
        }

        private async Task<DialogTurnResult> SaveUserSettingsStep(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var location = (string) stepContext.Result;
            var language = (string) stepContext.Values["language"];
            var culture = (string) stepContext.Values["culture"];

            CultureHelper.SetCurrentCulture(culture);

            var locationTranslate = culture == "uk-UA"
                ? await _translationService.Translate(location, "uk-UA", "en-US")
                : location;
            
            var preciseLocationTask = _locationService.GetLocation(locationTranslate);

            await stepContext.Context.SendActivityAsync(new Activity { Type = ActivityTypes.Typing }, cancellationToken);

            var preciseLocation = await preciseLocationTask;
            if (preciseLocation is null)
            {
                var retryMessage = _localizer[ResourceKeys.AskToRetryLocation];
                var options = new Dictionary<string, object>
                {
                    { "language", language },
                    { "retryMessage", MessageFactory.Text(retryMessage, retryMessage) }
                };

                return await stepContext.ReplaceDialogAsync($"{nameof(UserSettingsDialog)}.retryLocation", options, cancellationToken);
            }

            var userSettings = new UserSettings
            {
                Language = language,
                Culture = culture,
                Location = $"{preciseLocation.City}, {preciseLocation.Country}",
                TimeZone = preciseLocation.TimeZoneId
            };
            await _stateService.UserSettingsPropertyAccessor.SetAsync(stepContext.Context, userSettings, cancellationToken);

            var localTime = _clock.GetLocalDateTime(userSettings.TimeZone)
                .ToString("t", CultureInfo.CurrentCulture);

            var message = _localizer[ResourceKeys.UserCurrentSettings, language, userSettings.Location, preciseLocation.TimeZoneId, localTime];

            await stepContext.Context.SendActivityAsync(MessageFactory.Text(message, message), cancellationToken);

            return await stepContext.EndDialogAsync(userSettings, cancellationToken);
        }
    }
}
