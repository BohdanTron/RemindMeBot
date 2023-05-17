using Azure.Data.Tables;
using RemindMeBot.Models;

namespace RemindMeBot.Services
{
    public class ReminderTableService
    {
        private readonly TableClient _tableClient;

        public ReminderTableService(TableServiceClient tableServiceClient)
        {
            _tableClient = tableServiceClient.GetTableClient("reminders");
        }

        public virtual async Task AddReminder(ReminderEntity reminder, CancellationToken cancellationToken = new())
        {
            await _tableClient.AddEntityAsync(reminder, cancellationToken);
        }

        public virtual async Task<ReminderEntity?> GetReminder(string partitionKey, string rowKey, CancellationToken cancellationToken = new())
        {
            var reminder =
                await _tableClient.GetEntityIfExistsAsync<ReminderEntity>(partitionKey, rowKey, cancellationToken: cancellationToken);

            return reminder.Value;
        }
    }
}
