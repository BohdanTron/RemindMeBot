using Azure.Data.Tables;
using Azure.Storage.Queues;
using Newtonsoft.Json;

namespace RemindMeBot.Services
{
    public record ReminderCreatedMessage(string PartitionKey, string RowKey);

    public class ReminderQueueService
    {
        private readonly QueueClient _queueClient;

        public ReminderQueueService(QueueServiceClient queueServiceClient) =>
            _queueClient = queueServiceClient.GetQueueClient("reminders");

        public virtual async Task PublishCreatedMessage(ITableEntity entity, CancellationToken cancellationToken = new())
        {
            var message = new ReminderCreatedMessage(entity.PartitionKey, entity.RowKey);

            await _queueClient.SendMessageAsync(JsonConvert.SerializeObject(message), cancellationToken);
        }
    }
}
