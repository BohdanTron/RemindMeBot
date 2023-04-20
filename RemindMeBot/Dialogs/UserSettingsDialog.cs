using System.Globalization;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Dialogs.Choices;
using Microsoft.Extensions.Localization;
using RemindMeBot.Models;
using RemindMeBot.Resources;
using RemindMeBot.Services;

namespace RemindMeBot.Dialogs
{
    public class UserSettingsDialog : ComponentDialog
    {
        private readonly StateService _stateService;
        private readonly IStringLocalizer<BotMessages> _localizer;

        public UserSettingsDialog(StateService stateService, IStringLocalizer<BotMessages> localizer)
            : base(nameof(UserSettingsDialog))
        {
            _stateService = stateService;
            _localizer = localizer;

            AddDialog(new ChoicePrompt($"{nameof(UserSettingsDialog)}.language"));
            AddDialog(new TextPrompt($"{nameof(UserSettingsDialog)}.location"));

            AddDialog(new WaterfallDialog($"{nameof(UserSettings)}.{nameof(WaterfallDialog)}",
                new WaterfallStep[]
                {
                    AskForLanguageStep,
                    AskForLocationStep,
                    SaveUserSettingsStep
                }));

            InitialDialogId = $"{nameof(UserSettings)}.{nameof(WaterfallDialog)}";
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
            var language = ((FoundChoice) stepContext.Result).Value;
            stepContext.Values["language"] = language;

            var languageCode = GetLanguageCode(language);
            var userSettings = new UserSettings
            {
                Language = languageCode
            };

            var culture = new CultureInfo(languageCode);
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;

            await _stateService.UserSettingsPropertyAccessor.SetAsync(stepContext.Context, userSettings, cancellationToken);

            return await stepContext.PromptAsync($"{nameof(UserSettingsDialog)}.location",
                new PromptOptions
                {
                    Prompt = MessageFactory.Text(_localizer[ResourcesKeys.AskForLocation].Value)
                }, cancellationToken);
        }

        private async Task<DialogTurnResult> SaveUserSettingsStep(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var language = (string) stepContext.Values["language"];

            var userSettings = new UserSettings
            {
                Language = GetLanguageCode(language),
                Location = (string) stepContext.Result
            };

            await _stateService.UserSettingsPropertyAccessor.SetAsync(stepContext.Context, userSettings, cancellationToken);

            var message = _localizer[ResourcesKeys.LanguageAndLocationSet, language, userSettings.Location];

            await stepContext.Context.SendActivityAsync(MessageFactory.Text(message), cancellationToken);

            return await stepContext.EndDialogAsync(cancellationToken: cancellationToken);
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
