namespace RemindMeBot.Models
{
    public record RecognizedReminder(string Text, DateTime DateTime, RepeatedInterval RepeatedInterval);
}