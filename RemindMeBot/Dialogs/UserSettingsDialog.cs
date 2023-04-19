using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Dialogs.Choices;
using RemindMeBot.Helpers;
using RemindMeBot.Models;
using RemindMeBot.Services;

namespace RemindMeBot.Dialogs
{
    public class UserSettingsDialog : ComponentDialog
    {
        private readonly StateService _stateService;

        public UserSettingsDialog(StateService stateService)
            : base(nameof(UserSettingsDialog))
        {
            _stateService = stateService;

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

        private static Task<DialogTurnResult> AskForLocationStep(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var language = ((FoundChoice) stepContext.Result).Value;
            stepContext.Values["language"] = language;

            var code = GetLanguageCode(language);

            return stepContext.PromptAsync($"{nameof(UserSettingsDialog)}.location",
                new PromptOptions
                {
                    Prompt = MessageFactory.Text(ResourceKeys.AskForLocation.ToLocalized(code))
                }, cancellationToken);
        }

        private async Task<DialogTurnResult> SaveUserSettingsStep(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var language = stepContext.Values["language"].ToString()!;

            var userSettings = new UserSettings
            {
                Language = GetLanguageCode(language),
                Location = stepContext.Result.ToString()
            };

            await _stateService.UserSettingsPropertyAccessor.SetAsync(stepContext.Context, userSettings, cancellationToken);

            var messageTemplate = ResourceKeys.LanguageAndLocationSet.ToLocalized(GetLanguageCode(language));
            var message = string.Format(messageTemplate, language, userSettings.Location);

            await stepContext.Context.SendActivityAsync(MessageFactory.Text(message), cancellationToken);

            return await stepContext.EndDialogAsync(cancellationToken: cancellationToken);
        }

        private static string GetLanguageCode(string language) =>
            language switch
            {
                "English" => "en",
                "Українська" => "uk",
                _ => "en"
            };
    }
}
