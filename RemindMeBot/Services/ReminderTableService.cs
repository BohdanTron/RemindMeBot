﻿using Azure.Data.Tables;
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

        public virtual async Task<ReminderEntity?> Get(string partitionKey, string rowKey, CancellationToken cancellationToken = new())
        {
            var reminder =
                await _tableClient.GetEntityIfExistsAsync<ReminderEntity>(partitionKey, rowKey, cancellationToken: cancellationToken);

            return reminder.HasValue ? reminder.Value : null;
        }

        public virtual async Task<List<ReminderEntity>> GetList(string partitionKey, CancellationToken cancellationToken = new())
        {
            var queryResults = _tableClient
                .QueryAsync<ReminderEntity>(e => e.PartitionKey == partitionKey, cancellationToken: cancellationToken);

            var reminders = new List<ReminderEntity>();
            await foreach (var result in queryResults)
            {
                reminders.Add(result);
            }

            return reminders;
        }

        public virtual async Task Add(ReminderEntity reminder, CancellationToken cancellationToken = new())
        {
            // TODO: Add error handling
            await _tableClient.AddEntityAsync(reminder, cancellationToken);
        }

        public virtual async Task Delete(string partitionKey, string rowKey, CancellationToken cancellationToken = new())
        {
            // TODO: Add error handling
            await _tableClient.DeleteEntityAsync(partitionKey, rowKey, cancellationToken: cancellationToken);
        }
    }
}
