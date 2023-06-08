using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Recognizers.Text;
using Microsoft.Recognizers.Text.DateTime;
using RemindMeBot.Models;
using static Microsoft.Recognizers.Text.Culture;

namespace RemindMeBot.Services.Recognizers
{
    public class MicrosoftRecognizer : IReminderRecognizer
    {
        private readonly RepeatedIntervalMapper _repeatedIntervalMapper;
        private const string PrepositionsRegex = @"\s+(at|on|in|by|for|after|before|around|until)$";

        public string[] SupportedCultures { get; } = { "en-US" };

        public MicrosoftRecognizer(RepeatedIntervalMapper repeatedIntervalMapper) =>
            _repeatedIntervalMapper = repeatedIntervalMapper;

        public Task<RecognizedReminder?> RecognizeReminder(string input, DateTime refDateTime)
        {
            var culture = CultureInfo.CurrentCulture.Name == "en-US" ? English : EnglishOthers;

            var results = DateTimeRecognizer.RecognizeDateTime(input, culture, DateTimeOptions.TasksMode, refTime: refDateTime);
            var result = results.FirstOrDefault();

            var values = (List<Dictionary<string, string>>?) result?.Resolution?["values"];
            if (values is null)
            {
                return Task.FromResult<RecognizedReminder?>(null);
            }

            var text = ExtractTextOnly(input, result!);
            if (string.IsNullOrEmpty(text))
            {
                return Task.FromResult<RecognizedReminder?>(null);
            }

            foreach (var value in values)
            {
                var resolution = ReadResolution(value);

                var dateTime = ExtractDateTime(refDateTime, resolution);
                if (dateTime is null)
                {
                    continue;
                }

                if (resolution.IntervalType is null)
                {
                    return Task.FromResult<RecognizedReminder?>(new RecognizedReminder(text, dateTime.Value, RepeatedInterval.None));
                }
                if (resolution.IntervalSize != 1)
                {
                    return Task.FromResult<RecognizedReminder?>(null);
                }

                var interval = _repeatedIntervalMapper.MapToEnum(resolution.IntervalType);

                return Task.FromResult<RecognizedReminder?>(new RecognizedReminder(text, dateTime.Value, interval));
            }

            return Task.FromResult<RecognizedReminder?>(null);
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
            if (resolution.TryGetValue("intervalSize", out var intervalSize))
            {
                result.IntervalSize = int.Parse(intervalSize);
            }
            if (resolution.TryGetValue("intervalType", out var intervalType))
            {
                result.IntervalType = intervalType;
            }

            return result;
        }

        private static string ExtractTextOnly(string input, ModelResult result)
        {
            var textWithPreposition = input[..result.Start].Trim();
            var cleanText = Regex.Replace(textWithPreposition, PrepositionsRegex, string.Empty, RegexOptions.Compiled);

            return cleanText;
        }

        private static DateTime? ExtractDateTime(DateTime refDateTime, DateTimeResolution resolution)
        {
            if (!DateTime.TryParse(resolution.Value, CultureInfo.CurrentCulture, DateTimeStyles.NoCurrentDateDefault,
                    out var dateTime))
            {
                return null;
            }

            // Use current date if only the time is specified
            if (dateTime.Date == default)
            {
                dateTime = new DateTime(refDateTime.Year, refDateTime.Month, refDateTime.Day,
                    dateTime.Hour, dateTime.Minute, dateTime.Second);
            }

            // Use next day if the date is recognized but the time is passed
            if (dateTime.Date == refDateTime.Date && dateTime.TimeOfDay < refDateTime.TimeOfDay)
            {
                dateTime = dateTime.AddDays(1);
            }

            // Ignore other past dates and time
            if (refDateTime > dateTime)
            {
                return null;
            }

            return dateTime;
        }
    }

    public record DateTimeResolution
    {
        public string? Value { get; set; }
        public string? Start { get; set; }
        public string? End { get; set; }
        public string? Timex { get; set; }
        public int? IntervalSize { get; set; }
        public string? IntervalType { get; set; }
    }
}
