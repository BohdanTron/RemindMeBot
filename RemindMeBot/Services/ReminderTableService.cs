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

        public virtual async Task AddReminder(ReminderEntity reminder, CancellationToken cancellationToken)
        {
            await _tableClient.AddEntityAsync(reminder, cancellationToken);
        }
    }
}
