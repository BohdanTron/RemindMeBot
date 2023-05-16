using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Data.Tables;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using ReminderFunctions.Helpers;
using ITableEntity = Azure.Data.Tables.ITableEntity;

namespace ReminderFunctions
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
    }

    public record ReminderActionMessage(ActionType Action, string PartitionKey, string RowKey);

    public enum ActionType : byte
    {
        Created = 1,
        Updated = 2,
        Deleted = 3
    }

    public static class ReminderFunctions
    {
        private static readonly Dictionary<ActionType, string> ActionToHandlerMap = new()
        {
            { ActionType.Created, nameof(CreateReminder) },
            { ActionType.Updated, nameof(UpdateReminder) },
            { ActionType.Deleted, nameof(DeleteReminder) }
        };

        [FunctionName(nameof(RunOrchestrator))]
        public static async Task<bool> RunOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            var reminder = context.GetInput<ReminderActionMessage>();

            var handler = ActionToHandlerMap[reminder.Action];

            var result = await context.CallSubOrchestratorAsync<bool>(handler, reminder);

            return result;
        }

        [FunctionName(nameof(CreateReminder))]
        public static async Task<bool> CreateReminder(
            [OrchestrationTrigger] IDurableOrchestrationContext context,
            ILogger logger)
        {
            var message = context.GetInput<ReminderActionMessage>();
            var reminder = await context.CallActivityAsync<ReminderEntity>(nameof(GetReminder), message);

            var reminderDateTimeLocal = DateTimeOffset.Parse(reminder.DueDateTimeLocal);
            var reminderDateTimeUtc = reminderDateTimeLocal.ToDateTimeUtc(reminder.TimeZone);

            await context.CreateTimer(reminderDateTimeUtc, CancellationToken.None);

            // TODO: Send a proactive message

            // Handle repeated event
            if (reminder.RepeatInterval is null)
            {
                return true;
            }

            await context.CallActivityAsync(nameof(UpdateReminderDate), reminder);

            await context.CallActivityAsync(nameof(PublishMessage),
                new ReminderActionMessage(ActionType.Created, reminder.PartitionKey, reminder.RowKey));

            // TODO: Add logging

            return true;
        }

        [FunctionName(nameof(UpdateReminder))]
        public static Task<bool> UpdateReminder(
            [OrchestrationTrigger] IDurableOrchestrationContext context,
            ILogger logger)
        {
            throw new NotImplementedException();
        }

        [FunctionName(nameof(DeleteReminder))]
        public static Task<bool> DeleteReminder(
            [OrchestrationTrigger] IDurableOrchestrationContext context,
            ILogger logger)
        {
            throw new NotImplementedException();
        }

        [FunctionName(nameof(QueueStart))]
        public static async Task QueueStart(
            [QueueTrigger("reminders")] string message,
            [DurableClient] IDurableOrchestrationClient starter,
            ILogger logger)
        {
            logger.LogInformation($"Queue trigger function processed: {message}");

            var reminder = JsonConvert.DeserializeObject<ReminderActionMessage>(message);

            var instanceId = await starter.StartNewAsync(nameof(RunOrchestrator), input: reminder);

            logger.LogInformation("Started orchestration with ID = '{instanceId}'.", instanceId);
        }

        [FunctionName(nameof(GetReminder))]
        public static async Task<ReminderEntity> GetReminder(
            [ActivityTrigger] ReminderActionMessage message,
            [Table("reminders")] TableClient tableClient,
            ILogger logger)
        {
            var reminder = await tableClient.GetEntityAsync<ReminderEntity>(message.PartitionKey, message.RowKey);

            return reminder;
        }

        [FunctionName(nameof(UpdateReminderDate))]
        public static async Task UpdateReminderDate(
            [ActivityTrigger] ReminderEntity reminder,
            [Table("reminders")] TableClient tableClient,
            ILogger logger)
        {
            var currentDateTime = DateTimeOffset.Parse(reminder.DueDateTimeLocal);

            var nextDateTime = reminder.RepeatInterval!.ToLowerInvariant() switch
            {
                "daily" => currentDateTime.AddDays(1),
                "weekly" => currentDateTime.AddDays(7),
                "monthly" => currentDateTime.AddMonths(1),
                "yearly" => currentDateTime.AddYears(1),
                _ => throw new InvalidOperationException($"Unsupported repeat interval: {reminder.RepeatInterval}")
            };
            reminder.DueDateTimeLocal = nextDateTime.ToString(CultureInfo.CurrentCulture);

            await tableClient.UpdateEntityAsync(reminder, ETag.All, TableUpdateMode.Replace);
        }

        [FunctionName(nameof(PublishMessage))]
        [return: Queue("reminders")]
        public static string PublishMessage([ActivityTrigger] ReminderActionMessage message, ILogger logger)
        {
            return JsonConvert.SerializeObject(message);
        }
    }
}