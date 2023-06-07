using System.Globalization;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RemindMeBot.Models;

namespace RemindMeBot.Services
{
    public class OpenAiService
    {
        private const string SystemPrompt =
            "You're an AI for a Ukrainian bot. Parse tasks (raw text without datetime part), reminder dates (various formats), and reference dates ('yyyy-mm-dd HH:mm:ss'). " +
            "Output JSON with 'text', 'datetime', and 'repeatedInterval' ('daily', 'weekly', 'monthly', 'yearly', null). " +
            "Reminder date and time should be future to reference date. Invalid input yields null.";

        private readonly HttpClient _httpClient;
        private readonly ILogger _logger;

        public OpenAiService(HttpClient httpClient, ILogger<OpenAiService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public virtual async Task<RecognizedReminder?> RecognizeReminder(string input, DateTime refDateTime)
        {
            var body = new JObject
            {
                ["model"] = "gpt-3.5-turbo",
                ["max_tokens"] = 250,
                ["temperature"] = 0,
                ["top_p"] = 0,
                ["messages"] = new JArray(
                    new JObject
                    {
                        ["role"] = "system",
                        ["content"] = SystemPrompt
                    },
                    new JObject
                    {
                        ["role"] = "user",
                        ["content"] = $"{input}, reference date: {refDateTime.ToString(CultureInfo.InvariantCulture)}"
                    })
            };

            var content = new StringContent(body.ToString(), Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("/v1/chat/completions", content);
            var responseBody = await response.Content.ReadAsStringAsync();

            _logger.LogInformation($"OpenAI response body: {responseBody}");

            try
            {
                var openAiResponse = JsonConvert.DeserializeObject<OpenAiResponse?>(responseBody);
                var message = openAiResponse?.Choices[0].Message;

                if (message is null) return null;

                var reminder = JsonConvert.DeserializeObject<RecognizedReminder?>(message.Content);

                return reminder;
            }
            catch
            {
                return null;
            }
        }
    }

    public record OpenAiResponse
    {
        [JsonProperty("id")]
        public string Id { get; init; } = default!;

        [JsonProperty("object")]
        public string Object { get; init; } = default!;

        [JsonProperty("created")]
        public long Created { get; init; }

        [JsonProperty("choices")]
        public List<Choice> Choices { get; init; } = default!;

        [JsonProperty("usage")]
        public Usage Usage { get; init; } = default!;
    }

    public record Choice
    {
        [JsonProperty("index")]
        public int Index { get; init; }

        [JsonProperty("message")]
        public Message Message { get; init; } = default!;

        [JsonProperty("finish_reason")]
        public string FinishReason { get; init; } = default!;
    }

    public record Message
    {
        [JsonProperty("role")]
        public string Role { get; init; } = default!;

        [JsonProperty("content")]
        public string Content { get; init; } = default!;
    }

    public record Usage
    {
        [JsonProperty("prompt_tokens")]
        public int PromptTokens { get; init; }

        [JsonProperty("completion_tokens")]
        public int CompletionTokens { get; init; }

        [JsonProperty("total_tokens")]
        public int TotalTokens { get; init; }
    }
}