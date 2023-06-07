namespace RemindMeBot.Models
{
    public record RecognizedReminder(string Text, DateTime DateTime, string? RepeatedInterval);
}