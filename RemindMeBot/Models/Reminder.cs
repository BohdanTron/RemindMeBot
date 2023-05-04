namespace RemindMeBot.Models
{
    public record Reminder
    {
        public string Text { get; init; } = default!;
        public DateTime Date { get; init; }
        public bool ShouldRepeat { get; init; }
        public string? RepeatInterval { get; init; }
    }
}
