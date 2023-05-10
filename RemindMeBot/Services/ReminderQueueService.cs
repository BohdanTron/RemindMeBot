using Azure.Storage.Queues;
using Newtonsoft.Json;

namespace RemindMeBot.Services
{
    public record ReminderActionMessage(ActionType Action, string PartitionKey, string RowKey);

    public enum ActionType : byte
    {
        Created = 1,
        Updated = 2,
        Deleted = 3
    }

    public class ReminderQueueService
    {
        private readonly QueueClient _queueClient;

        public ReminderQueueService(QueueClient queueClient) =>
            _queueClient = queueClient;

        public virtual async Task SendReminderCreatedMessage(string partitionKey, string rowKey, CancellationToken cancellationToken = new())
        {
            var message = new ReminderActionMessage(ActionType.Created, partitionKey, rowKey);

            await _queueClient.SendMessageAsync(JsonConvert.SerializeObject(message), cancellationToken);
        }

        public virtual async Task SendReminderUpdatedMessage(string partitionKey, string rowKey, CancellationToken cancellationToken = new())
        {
            var message = new ReminderActionMessage(ActionType.Updated, partitionKey, rowKey);

            await _queueClient.SendMessageAsync(JsonConvert.SerializeObject(message), cancellationToken);
        }

        public virtual async Task SendReminderDeletedMessage(string partitionKey, string rowKey, CancellationToken cancellationToken = new())
        {
            var message = new ReminderActionMessage(ActionType.Deleted, partitionKey, rowKey);

            await _queueClient.SendMessageAsync(JsonConvert.SerializeObject(message), cancellationToken);
        }
    }
}
