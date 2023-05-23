using Azure.Storage.Queues;
using Newtonsoft.Json;

namespace RemindMeBot.Services
{
    public record ReminderActionMessage(ActionType Action, string PartitionKey, string RowKey);

    public enum ActionType : byte
    {
        Created = 1,
        Deleted = 2
    }

    public class ReminderQueueService
    {
        private readonly QueueClient _queueClient;

        public ReminderQueueService(QueueServiceClient queueServiceClient) => 
            _queueClient = queueServiceClient.GetQueueClient("reminders");

        public virtual async Task PublishCreatedMessage(string partitionKey, string rowKey, CancellationToken cancellationToken = new())
        {
            var message = new ReminderActionMessage(ActionType.Created, partitionKey, rowKey);

            await _queueClient.SendMessageAsync(JsonConvert.SerializeObject(message), cancellationToken);
        }

        public virtual async Task PublishDeletedMessage(string partitionKey, string rowKey, CancellationToken cancellationToken = new())
        {
            var message = new ReminderActionMessage(ActionType.Deleted, partitionKey, rowKey);

            await _queueClient.SendMessageAsync(JsonConvert.SerializeObject(message), cancellationToken);
        }
    }
}
