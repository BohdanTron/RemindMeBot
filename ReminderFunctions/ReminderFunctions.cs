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

            var reminderMsg = JsonConvert.DeserializeObject<ReminderCreatedMessage>(message);

            var instanceId = await starter.StartNewAsync(nameof(CreateReminder), input: reminderMsg);

            logger.LogInformation("Started orchestration with ID = '{instanceId}'.", instanceId);
        }

        [FunctionName(nameof(CreateReminder))]
        public static async Task CreateReminder(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            var message = context.GetInput<ReminderCreatedMessage>();
            var reminder = await context.CallActivityAsync<ReminderEntity?>(nameof(GetReminder), message);

            if (reminder is null) return;

            var reminderDateTimeLocal = DateTime.ParseExact(reminder.DueDateTimeLocal, "G", CultureInfo.InvariantCulture, DateTimeStyles.None);
            var reminderDateTimeUtc = reminderDateTimeLocal.ToDateTimeUtc(reminder.TimeZone);

            if (reminderDateTimeUtc <= context.CurrentUtcDateTime) return;

            await context.CreateTimer(reminderDateTimeUtc, CancellationToken.None);

            await context.CallSubOrchestratorAsync<bool>(nameof(SendReminder), message);
        }

        [FunctionName(nameof(SendReminder))]
        public static async Task SendReminder(
            [OrchestrationTrigger] IDurableOrchestrationContext context,
            ILogger logger)
        {
            var message = context.GetInput<ReminderCreatedMessage>();
            var reminder = await context.CallActivityAsync<ReminderEntity?>(nameof(GetReminder), message);

            if (reminder is null) return;

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

            if (reminder.RepeatedInterval == RepeatedInterval.None)
            {
                await context.CallActivityAsync<bool>(nameof(DeleteReminder), reminder);
                return;
            }

            await context.CallActivityAsync(nameof(UpdateReminderDate), reminder);
            await context.CallActivityAsync(nameof(PublishMessage), message);
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
                if (reminder.HasValue)
                {
                    return reminder.Value;
                }

                logger.LogInformation(
                    $"Reminder with partitionKey = {message.PartitionKey} and rowKey = {message.RowKey} wasn't found, it could be deleted.");

                return null;
            }
            catch (RequestFailedException ex)
            {
                logger.LogError(ex, $"Request to get reminder failed, partitionKey = {message.PartitionKey}, rowKey = {message.RowKey}");
                return null;
            }
        }

        [FunctionName(nameof(DeleteReminder))]
        public static async Task DeleteReminder(
            [ActivityTrigger] ReminderEntity reminder,
            [Table("reminders")] TableClient tableClient,
            ILogger logger)
        {
            try
            {
                await tableClient.DeleteEntityAsync(reminder.PartitionKey, reminder.RowKey);
            }
            catch (RequestFailedException ex)
            {
                logger.LogError(ex, $"Deleting of reminder failed, partitionKey = {reminder.PartitionKey} and rowKey = {reminder.RowKey}");
                throw;
            }
        }

        [FunctionName(nameof(UpdateReminderDate))]
        public static async Task UpdateReminderDate(
            [ActivityTrigger] ReminderEntity reminder,
            [Table("reminders")] TableClient tableClient,
            ILogger logger)
        {
            var currentDateTime = DateTime.ParseExact(reminder.DueDateTimeLocal, "G", CultureInfo.InvariantCulture, DateTimeStyles.None);

            var nextDateTime = reminder.RepeatedInterval switch
            {
                RepeatedInterval.Daily => currentDateTime.AddDays(1),
                RepeatedInterval.Weekly => currentDateTime.AddDays(7),
                RepeatedInterval.Monthly => currentDateTime.AddMonths(1),
                RepeatedInterval.Yearly => currentDateTime.AddYears(1),
                _ => throw new InvalidOperationException($"Unsupported repeat interval: {reminder.RepeatedInterval}")
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
        public RepeatedInterval RepeatedInterval { get; set; }
    }

    public enum RepeatedInterval
    {
        None = 0,
        Daily = 1,
        Weekly = 2,
        Monthly = 3,
        Yearly = 4
    }
}