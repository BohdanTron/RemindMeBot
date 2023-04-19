using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Dialogs.Choices;
using RemindMeBot.Models;

namespace RemindMeBot.Dialogs
{
    public class UserSettingsDialog : ComponentDialog
    {
        public UserSettingsDialog() : base(nameof(UserSettingsDialog))
        {
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
                    Prompt = MessageFactory.Text("Please choose your language"),
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

        private static async Task<DialogTurnResult> SaveUserSettingsStep(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var (location, language) = new UserSettings
            {
                Language = stepContext.Values["language"].ToString()!,
                Location = stepContext.Result.ToString()!
            };

            // TODO: Save user settings to state

            var message = $"Your language is set to {language} and your language is set to {location}.";
            await stepContext.Context.SendActivityAsync(MessageFactory.Text(message), cancellationToken);

            return await stepContext.EndDialogAsync(cancellationToken: cancellationToken);
        }
    }
}
