using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;
using Microsoft.Recognizers.Text.DateTime;
using RemindMeBot.Services;
using static Microsoft.Recognizers.Text.Culture;

namespace RemindMeBot.Dialogs.Prompts
{
    public class DateTimePrompt : Prompt<IList<DateTimeResolution>>
    {
        private readonly ITranslationService _translationService;

        public DateTimePrompt(
            string dialogId,
            ITranslationService translationService,
            PromptValidator<IList<DateTimeResolution>>? validator = null) : base(dialogId, validator)
        {
            _translationService = translationService;
        }

        protected override async Task OnPromptAsync(
            ITurnContext turnContext,
            IDictionary<string, object> state,
            PromptOptions options,
            bool isRetry,
            CancellationToken cancellationToken = new())
        {
            if (turnContext is null)
            {
                throw new ArgumentNullException(nameof(turnContext));
            }

            if (options is null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            if (isRetry && options.RetryPrompt is not null)
            {
                await turnContext.SendActivityAsync(options.RetryPrompt, cancellationToken);
            }
            else if (options.Prompt is not null)
            {
                await turnContext.SendActivityAsync(options.Prompt, cancellationToken);
            }
        }

        protected override async Task<PromptRecognizerResult<IList<DateTimeResolution>>> OnRecognizeAsync(
            ITurnContext turnContext,
            IDictionary<string, object> state,
            PromptOptions options,
            CancellationToken cancellationToken = new())
        {
            if (turnContext is null)
            {
                throw new ArgumentNullException(nameof(turnContext));
            }

            if (state.TryGetValue("result", out var existingResult) && ((PromptRecognizerResult<IList<DateTimeResolution>>)existingResult).Succeeded)
            {
                return (PromptRecognizerResult<IList<DateTimeResolution>>) existingResult;
            }

            var result = new PromptRecognizerResult<IList<DateTimeResolution>>();
            if (turnContext.Activity.Type == ActivityTypes.Message)
            {
                var utterance = turnContext.Activity.AsMessageActivity().Text;
                if (string.IsNullOrEmpty(utterance))
                {
                    state.TryAdd("result", result);
                    return result;
                }

                if (turnContext.Activity.Locale == "uk-UA" && !IsNumeric(utterance))
                {
                    utterance = await _translationService.Translate(utterance, from: "uk-UA", to: "en-US");
                }

                var refTime = turnContext.Activity.LocalTimestamp?.DateTime;
                var results = DateTimeRecognizer.RecognizeDateTime(utterance, English, refTime: refTime);

                if (results.Any())
                {
                    // Return list of resolutions from first match
                    result.Succeeded = true;
                    result.Value = new List<DateTimeResolution>();
                    var values = (List<Dictionary<string, string>>) results[0].Resolution["values"];
                    foreach (var value in values)
                    {
                        result.Value.Add(ReadResolution(value));
                    }
                }
            }
            
            state.TryAdd("result", result);
            return result;
        }

        private static DateTimeResolution ReadResolution(IDictionary<string, string> resolution)
        {
            var result = new DateTimeResolution();

            if (resolution.TryGetValue("timex", out var timex))
            {
                result.Timex = timex;
            }

            if (resolution.TryGetValue("value", out var value))
            {
                result.Value = value;
            }

            if (resolution.TryGetValue("start", out var start))
            {
                result.Start = start;
            }

            if (resolution.TryGetValue("end", out var end))
            {
                result.End = end;
            }

            return result;
        }

        private static bool IsNumeric(string utterance) =>
            utterance.All(char.IsDigit);
    }
}