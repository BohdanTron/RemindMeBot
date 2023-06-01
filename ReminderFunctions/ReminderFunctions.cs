using System;
using System.Globalization;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Data.Tables;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using ReminderFunctions.Helpers;
using HttpMethod = System.Net.Http.HttpMethod;
using ITableEntity = Azure.Data.Tables.ITableEntity;

namespace ReminderFunctions
{
    public record ReminderCreatedMessage(string PartitionKey, string RowKey);

    public static class ReminderFunctions
    {
        [FunctionName(nameof(QueueStart))]
        public static async Task QueueStart(
            [QueueTrigger("reminders")] string message,
            [DurableClient] IDurableOrchestrationClient starter,
            ILogger logger)
        {
            logger.LogInformation($"Message from queue retrieved: {message}");

            var remindedMsg = JsonConvert.DeserializeObject<ReminderCreatedMessage>(message);

            var instanceId = await starter.StartNewAsync(nameof(CreateReminder), input: remindedMsg);

            logger.LogInformation("Started orchestration with ID = '{instanceId}'.", instanceId);
        }

        [FunctionName(nameof(CreateReminder))]
        public static async Task<bool> CreateReminder([OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            var message = context.GetInput<ReminderCreatedMessage>();
            var reminder = await context.CallActivityAsync<ReminderEntity>(nameof(GetReminder), message);

            var reminderDateTimeLocal = DateTime.ParseExact(reminder.DueDateTimeLocal, "G", CultureInfo.InvariantCulture, DateTimeStyles.None);
            var reminderDateTimeUtc = reminderDateTimeLocal.ToDateTimeUtc(reminder.TimeZone);

            if (reminderDateTimeUtc <= context.CurrentUtcDateTime) return false;

            await context.CreateTimer(reminderDateTimeUtc, CancellationToken.None);

            var succeeded = await context.CallSubOrchestratorAsync<bool>(nameof(SendReminder), message);

            return succeeded;
        }

        [FunctionName(nameof(SendReminder))]
        public static async Task<bool> SendReminder(
            [OrchestrationTrigger] IDurableOrchestrationContext context,
            ILogger logger)
        {
            var message = context.GetInput<ReminderCreatedMessage>();
            var reminder = await context.CallActivityAsync<ReminderEntity?>(nameof(GetReminder), message);

            if (reminder is null) return false;

            var baseAddress = Environment.GetEnvironmentVariable("BotBaseAddress");
            var url = new Uri($"{baseAddress}/api/proactive-message/{reminder.PartitionKey}/{reminder.RowKey}");

            logger.LogInformation($"Calling the proactive message endpoint, the URL is {url}");
            try
            {
                await context.CallHttpAsync(HttpMethod.Get, url,
                    retryOptions: new HttpRetryOptions(TimeSpan.FromMinutes(1), 5));
            }
            catch (HttpRequestException ex)
            {
                logger.LogError(ex, "HTTP request to the proactive message endpoint failed");
            }

            if (reminder.RepeatInterval is null)
            {
                return await context.CallActivityAsync<bool>(nameof(DeleteReminder), reminder);
            }

            await context.CallActivityAsync(nameof(UpdateReminderDate), reminder);
            await context.CallActivityAsync(nameof(PublishMessage), message);

            return true;
        }

        [FunctionName(nameof(GetReminder))]
        public static async Task<ReminderEntity?> GetReminder(
            [ActivityTrigger] ReminderCreatedMessage message,
            [Table("reminders")] TableClient tableClient,
            ILogger logger)
        {
            try
            {
                var reminder = await tableClient.GetEntityIfExistsAsync<ReminderEntity>(message.PartitionKey, message.RowKey);

                return reminder.HasValue ? reminder.Value : null;
            }
            catch (RequestFailedException ex)
            {
                logger.LogError(ex, $"Request to get reminder failed, partitionKey = {message.PartitionKey}, rowKey = {message.RowKey}");
                return null;
            }
        }

        [FunctionName(nameof(DeleteReminder))]
        public static async Task<bool> DeleteReminder(
            [ActivityTrigger] ReminderEntity reminder,
            [Table("reminders")] TableClient tableClient,
            ILogger logger)
        {
            try
            {
                await tableClient.DeleteEntityAsync(reminder.PartitionKey, reminder.RowKey);
                return true;
            }
            catch (RequestFailedException ex)
            {
                logger.LogError(ex, $"Deleting reminder with partitionKey = {reminder.PartitionKey} and rowKey = {reminder.RowKey} failed");
                return false;
            }
        }

        [FunctionName(nameof(UpdateReminderDate))]
        public static async Task UpdateReminderDate(
            [ActivityTrigger] ReminderEntity reminder,
            [Table("reminders")] TableClient tableClient,
            ILogger logger)
        {
            var currentDateTime = DateTime.ParseExact(reminder.DueDateTimeLocal, "G", CultureInfo.InvariantCulture, DateTimeStyles.None);

            var nextDateTime = reminder.RepeatInterval!.ToLowerInvariant() switch
            {
                "daily" => currentDateTime.AddDays(1),
                "weekly" => currentDateTime.AddDays(7),
                "monthly" => currentDateTime.AddMonths(1),
                "yearly" => currentDateTime.AddYears(1),
                _ => throw new InvalidOperationException($"Unsupported repeat interval: {reminder.RepeatInterval}")
            };
            reminder.DueDateTimeLocal = nextDateTime.ToString("G", CultureInfo.InvariantCulture);

            logger.LogInformation($"Updating reminders with PartitionKey = {reminder.PartitionKey}, Row Key = {reminder.RowKey}, the new date = {reminder.DueDateTimeLocal}");

            await tableClient.UpdateEntityAsync(reminder, ETag.All);
        }

        [FunctionName(nameof(PublishMessage))]
        [return: Queue("reminders")]
        public static string PublishMessage([ActivityTrigger] ReminderCreatedMessage message)
        {
            return JsonConvert.SerializeObject(message);
        }
    }

    public record ReminderEntity : ITableEntity
    {
        public string PartitionKey { get; set; } = default!;
        public string RowKey { get; set; } = default!;
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }

        public string DueDateTimeLocal { get; set; } = default!;
        public string TimeZone { get; set; } = default!;
        public string? RepeatInterval { get; set; }
    }
}