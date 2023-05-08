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
        public string DateTimeLocal { get; set; } = default!;
        public DateTime CreationDateTimeUtc { get; set; }
        public string TimeZone { get; set; } = default!;
        public bool ShouldRepeat { get; set; }
        public string? RepeatInterval { get; set; }
        public string Culture { get; set; } = default!;
    }
}
