using System.Globalization;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Dialogs.Choices;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Localization;
using RemindMeBot.Models;
using RemindMeBot.Resources;
using RemindMeBot.Services;

namespace RemindMeBot.Dialogs
{
    public class UserSettingsDialog : ComponentDialog
    {
        private readonly StateService _stateService;
        private readonly ILocationService _locationService;
        private readonly ITranslationService _translationService;

        private readonly IStringLocalizer<BotMessages> _localizer;

        public UserSettingsDialog(
            StateService stateService,
            ITranslationService translationService,
            ILocationService locationService,
            IStringLocalizer<BotMessages> localizer) : base(nameof(UserSettingsDialog))
        {
            _stateService = stateService;
            _translationService = translationService;
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
            return stepContext.PromptAsync($"{nameof(UserSettingsDialog)}.language",
                new PromptOptions
                {
                    Prompt = MessageFactory.Text("Welcome to the RemindMe chatbot! Please choose your language:"),
                    Choices = ChoiceFactory.ToChoices(new List<string> { "English", "Українська" }),
                    RetryPrompt = MessageFactory.Text("Please choose an option from the list:")
                }, cancellationToken);
        }

        private async Task<DialogTurnResult> AskForLocationStep(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var options = stepContext.Options as IDictionary<string, object>;

            var language = options is not null && options.TryGetValue("language", out var lang)
                ? (string) lang
                : ((FoundChoice) stepContext.Result).Value;

            var languageCode = GetLanguageCode(language);
            SetCurrentCulture(languageCode);

            var prompt = options is not null && options.TryGetValue("retryMessage", out var message)
                ? (Activity) message
                : MessageFactory.Text(_localizer[ResourcesKeys.AskForLocation].Value);

            stepContext.Values["language"] = language;
            stepContext.Values["languageCode"] = languageCode;

            return await stepContext.PromptAsync($"{nameof(UserSettingsDialog)}.location",
                new PromptOptions
                {
                    Prompt = prompt,
                    RetryPrompt = MessageFactory.Text(_localizer[ResourcesKeys.AskToRetryLocation].Value)
                }, cancellationToken);
        }

        private async Task<DialogTurnResult> SaveUserSettingsStep(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var location = (string) stepContext.Result;
            var language = (string) stepContext.Values["language"];
            var languageCode = (string) stepContext.Values["languageCode"];

            SetCurrentCulture(languageCode);

            var locationTranslate = languageCode == "uk-UA"
                ? await _translationService.Translate(location, "uk-UA", "en-US")
                : location;

            var preciseLocation = await _locationService.GetLocation(locationTranslate);
            if (preciseLocation is null)
            {
                var retryMessage = MessageFactory.Text(_localizer[ResourcesKeys.AskToRetryLocation].Value);
                var options = new Dictionary<string, object> { { "language", language }, { "retryMessage", retryMessage } };

                return await stepContext.ReplaceDialogAsync($"{nameof(UserSettingsDialog)}.retryLocation", options, cancellationToken);
            }
            
            var userSettings = new UserSettings
            {
                Language = language,
                LanguageCode = languageCode,
                Location = $"{preciseLocation.City}, {preciseLocation.Country}",
                TimeZoneId = preciseLocation.TimeZoneId
            };
            await _stateService.UserSettingsPropertyAccessor.SetAsync(stepContext.Context, userSettings, cancellationToken);

            var userLocalTime = userSettings.LocalTime!;
            var message = _localizer[ResourcesKeys.UserSettingsWereSet, language, userSettings.Location, preciseLocation.TimeZoneId, userLocalTime];

            await stepContext.Context.SendActivityAsync(MessageFactory.Text(message), cancellationToken);

            return await stepContext.EndDialogAsync(cancellationToken: cancellationToken);
        }

        private static void SetCurrentCulture(string languageCode)
        {
            var culture = new CultureInfo(languageCode);
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
        }

        private static string GetLanguageCode(string language) =>
            language switch
            {
                "English" => "en-US",
                "Українська" => "uk-UA",
                _ => "en-US"
            };
    }
}
