using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Dialogs.Choices;
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
                    Prompt = MessageFactory.Text("Welcome to the RemindMe Chatbot! Please choose your language:"),
                    Choices = ChoiceFactory.ToChoices(new List<string> {"English", "Українська" }),
                    RetryPrompt = MessageFactory.Text("Please choose an option from the list")
                }, cancellationToken);
        }

        private static Task<DialogTurnResult> AskForLocationStep(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            stepContext.Values["language"] = ((FoundChoice)stepContext.Result).Value;

            return stepContext.PromptAsync($"{nameof(UserSettingsDialog)}.location",
                new PromptOptions
                {
                    Prompt = MessageFactory.Text("Please enter your city and country (e.g Kyiv, Ukraine)")
                }, cancellationToken);
        }

        private async Task<DialogTurnResult> SaveUserSettingsStep(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var userSettings = new UserSettings
            {
                Language = stepContext.Values["language"].ToString()!,
                Location = stepContext.Result.ToString()!
            };

            await _stateService.UserSettingsPropertyAccessor.SetAsync(stepContext.Context, userSettings, cancellationToken);

            var (location, language) = await _stateService.UserSettingsPropertyAccessor.GetAsync(stepContext.Context,
                () => new UserSettings(), cancellationToken);

            var message = $"Your language is set to {language} and your location is set to {location}.";
            await stepContext.Context.SendActivityAsync(MessageFactory.Text(message), cancellationToken);

            return await stepContext.EndDialogAsync(cancellationToken: cancellationToken);
        }
    }
}
