using Azure;
using Azure.Data.Tables;

namespace RemindMeBot.Models
{
    public record ReminderEntity : ITableEntity
    {
        public string PartitionKey { get; set; } = default!;
        public string RowKey { get; set; } = default!;
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }
        
        public string Text { get; set; } = default!;
        public string DueDateTimeLocal { get; set; } = default!;
        public string TimeZone { get; set; } = default!;
        public string? RepeatInterval { get; set; }
        public string ConversationReference { get; set; } = default!;
    }
}
