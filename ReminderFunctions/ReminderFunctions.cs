using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace ReminderFunctions
{
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
            var reminder = context.GetInput<ReminderActionMessage>();

            // TODO: Schedule timer

            return true;
        }

        [FunctionName(nameof(UpdateReminder))]
        public static async Task<bool> UpdateReminder(
            [OrchestrationTrigger] IDurableOrchestrationContext context,
            ILogger logger)
        {
            throw new NotImplementedException();
        }

        [FunctionName(nameof(DeleteReminder))]
        public static async Task<bool> DeleteReminder(
            [OrchestrationTrigger] IDurableOrchestrationContext context,
            ILogger logger)
        {
            throw new NotImplementedException();
        }

        [FunctionName(nameof(QueueStart))]
        public static async Task QueueStart(
            [QueueTrigger("reminders", Connection = "ConnectionString")] string message,
            [DurableClient] IDurableOrchestrationClient starter,
            ILogger logger)
        {
            logger.LogInformation($"Queue trigger function processed: {message}");

            var reminder = JsonConvert.DeserializeObject<ReminderActionMessage>(message);

            var instanceId = await starter.StartNewAsync(nameof(RunOrchestrator), input: reminder);

            logger.LogInformation("Started orchestration with ID = '{instanceId}'.", instanceId);
        }
    }
}