using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Recognizers.Text;
using Microsoft.Recognizers.Text.DateTime;
using static Microsoft.Recognizers.Text.Culture;

namespace RemindMeBot.Helpers
{
    public record RecognizedReminder(string Text, DateTime DateTime, string? Interval);

    public static class ReminderRecognizer
    {
        private const string PrepositionsRegex = @"\s+(at|on|in|by|for|after|before|around|until)$";

        public static RecognizedReminder? Recognize(string input, DateTime refDateTime)
        {
            var results = DateTimeRecognizer.RecognizeDateTime(input, English, DateTimeOptions.TasksMode, refTime: refDateTime);
            var result = results.FirstOrDefault();

            if (result is null) return null;

            var values = (List<Dictionary<string, string>>) result.Resolution["values"];
            var value = values.First();

            var resolution = ReadResolution(value);

            if (resolution.Value is null) return null;

            var text = ExtractTextOnly(input, result);
            var dateTime = ExtractDateTime(refDateTime, resolution);

            if (dateTime is null) return null;
            
            if (resolution.IntervalType is null) return new(text, dateTime.Value, null);

            if (resolution.IntervalSize != 1) return null;

            var interval = resolution.IntervalType switch
            {
                "D" => "daily",
                "W" => "weekly",
                "M" => "monthly",
                "Y" => "yearly",
                _ => throw new ArgumentOutOfRangeException(nameof(resolution.IntervalType))
            };
            
            return new(text, dateTime.Value, interval);
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
