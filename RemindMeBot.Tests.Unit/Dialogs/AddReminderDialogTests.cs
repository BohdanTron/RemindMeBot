﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Azure.Data.Tables;
using Azure.Storage.Queues;
using FluentAssertions;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Testing;
using Microsoft.Bot.Connector;
using Microsoft.Bot.Schema;
using NSubstitute;
using RemindMeBot.Dialogs;
using RemindMeBot.Models;
using RemindMeBot.Resources;
using RemindMeBot.Services;
using RemindMeBot.Tests.Unit.Common;
using Xunit;
using Xunit.Abstractions;

namespace RemindMeBot.Tests.Unit.Dialogs
{
    public class AddReminderDialogTests : BotTestBase
    {
        private readonly AddReminderDialog _sut;

        private readonly IStateService _stateService = Substitute.For<IStateService>();
        private readonly ITranslationService _translationService = Substitute.For<ITranslationService>();
        private readonly IClock _clock = Substitute.For<IClock>();
        private readonly ReminderTableService _reminderTableService = Substitute.ForPartsOf<ReminderTableService>(Substitute.For<TableServiceClient>());
        private readonly ReminderQueueService _reminderQueueService = Substitute.ForPartsOf<ReminderQueueService>(Substitute.For<QueueServiceClient>());

        public AddReminderDialogTests(ITestOutputHelper output) : base(output)
        {
            _sut = new AddReminderDialog(_stateService, _translationService, _clock, _reminderTableService,
                _reminderQueueService, new RepeatedIntervalMapper(Localizer), Localizer);
        }

        /// <summary>
        /// User: /add
        /// Bot: Please enter the title of the reminder:
        /// User: Take out the trash
        /// Bot: When should I remind you?
        /// User: tomorrow at 2 PM
        /// Bot: Do you want this reminder to repeat ? (yes / no)
        /// User: yes
        /// Bot: How often should it repeat ? (daily / weekly / monthly / yearly)
        /// User: weekly
        /// Bot: Reminder added: "Take out the trash" on 2023 - 04 - 17 18:00, repeating weekly.
        /// </summary>
        [Theory]
        [InlineData("English", "en-US", "Europe/London", "Take out the trash", "tomorrow at 2 PM", "weekly")]
        [InlineData("Українська", "uk-UA", "Europe/Kyiv", "Винести сміття", "завтра о 14:00", "щотижня")]
        public async Task ShouldSaveRepeatedEvent_WhenBasicFlow(
            string language,
            string culture,
            string timeZone,
            string reminderText,
            string reminderTime,
            string repeatInterval)
        {
            // Arrange
            var userSettings = new UserSettings
            {
                Language = language,
                Culture = culture,
                Location = "Test Location",
                TimeZone = timeZone
            };

            var localDateTime = new DateTime(2023, 5, 27, 0, 0, 0);

            ConfigureLocalization(culture, userSettings.TimeZone, localDateTime);

            _stateService.UserSettingsPropertyAccessor
                .GetAsync(Arg.Any<ITurnContext>(), Arg.Any<Func<UserSettings>>(), Arg.Any<CancellationToken>())
                .Returns(userSettings);

            _translationService
                .Translate(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
                .Returns("tomorrow at 14:00");

            _reminderTableService.Add(Arg.Any<ReminderEntity>(), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);

            var expectedDateTime = localDateTime.AddDays(1).Date.AddHours(14);
            var expectedDate = expectedDateTime.ToString("d", CultureInfo.CurrentCulture);
            var expectedTime = $"{expectedDateTime:t}";

            var testClient = new DialogTestClient(Channels.Test, _sut, middlewares: Middlewares);

            // Act / Assert
            var reply = await testClient.SendActivityAsync<IMessageActivity>("/add");
            reply.Text.Should().Be(Localizer[ResourceKeys.AskForReminderText]);

            reply = await testClient.SendActivityAsync<IMessageActivity>(reminderText);
            reply.Text.Should().Be(Localizer[ResourceKeys.AskForReminderDate]);

            reply = await testClient.SendActivityAsync<IMessageActivity>(reminderTime);
            reply.Text.Should().Be($"{Localizer[ResourceKeys.AskWhetherToRepeatReminder]} (1) {Localizer[ResourceKeys.Yes]} or (2) {Localizer[ResourceKeys.No]}");

            reply = await testClient.SendActivityAsync<IMessageActivity>(Localizer[ResourceKeys.Yes]);
            reply.Text.Should().Be($"{Localizer[ResourceKeys.AskForRepeatInterval]}\n\n   1. {Localizer[ResourceKeys.Daily]}\n   2. {Localizer[ResourceKeys.Weekly]}\n   3. {Localizer[ResourceKeys.Monthly]}\n   4. {Localizer[ResourceKeys.Yearly]}");

            reply = await testClient.SendActivityAsync<IMessageActivity>(Localizer[ResourceKeys.Weekly]);
            reply.Text.Should().Be(Localizer[ResourceKeys.RepeatedReminderAdded, reminderText, expectedDate, expectedTime, repeatInterval]);

            reply = testClient.GetNextReply<IMessageActivity>();
            reply.Text.Should().Be(Localizer[ResourceKeys.ReminderCreationTip]);

            // Check dialog result
            var conversation = testClient.DialogContext.Context.Activity.GetConversationReference();
            var result = (ReminderEntity) testClient.DialogTurnResult.Result;

            result.PartitionKey.Should().Be(conversation.User.Id);
            result.Text.Should().Be(reminderText);
            result.DueDateTimeLocal.Should().Be(expectedDateTime.ToString("G", CultureInfo.InvariantCulture));
            result.TimeZone.Should().Be(userSettings.TimeZone);
        }

        /// <summary>
        /// User: /add
        /// Bot: Please enter the title of the reminder:
        /// User: Take out the trash
        /// Bot: When should I remind you?
        /// User: tomorrow at 2 PM
        /// Bot: Do you want this reminder to repeat ? (yes / no)
        /// User: no
        /// Bot: Reminder added: "Take out the trash" on 2023-04-17 18:00.
        /// </summary>
        [Theory]
        [InlineData("English", "en-US", "Europe/London", "Take out the trash", "tomorrow at 2 PM")]
        [InlineData("Українська", "uk-UA", "Europe/Kyiv", "Винести сміття", "завтра о 14:00")]
        public async Task ShouldSaveNotRepeatedEvent_WhenBasicFlow(
            string language,
            string culture,
            string timeZone,
            string reminderText,
            string reminderTime)
        {
            // Arrange
            var userSettings = new UserSettings
            {
                Language = language,
                Culture = culture,
                Location = "Test Location",
                TimeZone = timeZone
            };

            var localDateTime = new DateTime(2023, 5, 27, 0, 0, 0);

            ConfigureLocalization(culture, userSettings.TimeZone, localDateTime);

            _stateService.UserSettingsPropertyAccessor
                .GetAsync(Arg.Any<ITurnContext>(), Arg.Any<Func<UserSettings>>(), Arg.Any<CancellationToken>())
                .Returns(userSettings);

            _translationService
                .Translate(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
                .Returns("tomorrow at 14:00");

            _reminderTableService.Add(Arg.Any<ReminderEntity>(), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);

            _clock.GetLocalDateTime(timeZone)
                .Returns(localDateTime);

            var expectedDateTime = localDateTime.AddDays(1).Date.AddHours(14);
            var expectedDate = expectedDateTime.ToString("d", CultureInfo.CurrentCulture);
            var expectedTime = $"{expectedDateTime:t}";

            var testClient = new DialogTestClient(Channels.Test, _sut, middlewares: Middlewares);

            // Act / Assert
            var reply = await testClient.SendActivityAsync<IMessageActivity>("/add");
            reply.Text.Should().Be(Localizer[ResourceKeys.AskForReminderText]);

            reply = await testClient.SendActivityAsync<IMessageActivity>(reminderText);
            reply.Text.Should().Be(Localizer[ResourceKeys.AskForReminderDate]);

            reply = await testClient.SendActivityAsync<IMessageActivity>(reminderTime);
            reply.Text.Should().Be($"{Localizer[ResourceKeys.AskWhetherToRepeatReminder]} (1) {Localizer[ResourceKeys.Yes]} or (2) {Localizer[ResourceKeys.No]}");

            reply = await testClient.SendActivityAsync<IMessageActivity>(Localizer[ResourceKeys.No]);
            reply.Text.Should().Be(Localizer[ResourceKeys.ReminderAdded, reminderText, expectedDate, expectedTime]);

            reply = testClient.GetNextReply<IMessageActivity>();
            reply.Text.Should().Be(Localizer[ResourceKeys.ReminderCreationTip]);

            // Check dialog result
            var conversation = testClient.DialogContext.Context.Activity.GetConversationReference();

            var result = (ReminderEntity) testClient.DialogTurnResult.Result;
            result.PartitionKey.Should().Be(conversation.User.Id);
            result.Text.Should().Be(reminderText);
            result.DueDateTimeLocal.Should().Be(expectedDateTime.ToString("G", CultureInfo.InvariantCulture));
        }

        [Theory]
        [MemberData(nameof(DateTimeInputVariations.ValidDates), MemberType = typeof(DateTimeInputVariations))]
        public async Task ShouldRecognizeDifferentDateInputs(string userDateInput, int days, int hours, int minutes = 0)
        {
            // Arrange
            var today = new DateTimeOffset(2023, 5, 6, 0, 0, 0, TimeSpan.FromHours(1)); // 2023-05-06 00:00 +01:00 - Saturday

            ConfigureLocalization("en-US", "Europe/London", today);

            var expectedDateTime = today.Date.AddDays(days).AddHours(hours).AddMinutes(minutes);
            var expectedDate = expectedDateTime.ToString("d", CultureInfo.CurrentCulture);
            var expectedTime = $"{expectedDateTime:t}";

            _stateService.UserSettingsPropertyAccessor
                .GetAsync(Arg.Any<ITurnContext>(), Arg.Any<Func<UserSettings>>(), Arg.Any<CancellationToken>())
                .Returns(new UserSettings
                {
                    Culture = "Test culture",
                    Language = "Test language",
                    TimeZone = "Europe/London",
                    Location = "Test Location"
                });

            _clock.GetLocalDateTime(Arg.Any<string>())
                .Returns(today);

            _reminderTableService.Add(Arg.Any<ReminderEntity>(), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);

            var testClient = new DialogTestClient(Channels.Test, _sut, middlewares: Middlewares);

            // Act / Assert
            var reply = await testClient.SendActivityAsync<IMessageActivity>("/add");
            reply.Text.Should().Be(Localizer[ResourceKeys.AskForReminderText]);

            reply = await testClient.SendActivityAsync<IMessageActivity>("Reminder text");
            reply.Text.Should().Be(Localizer[ResourceKeys.AskForReminderDate]);

            reply = await testClient.SendActivityAsync<IMessageActivity>(userDateInput);
            reply.Text.Should().Be($"{Localizer[ResourceKeys.AskWhetherToRepeatReminder]} (1) {Localizer[ResourceKeys.Yes]} or (2) {Localizer[ResourceKeys.No]}");

            reply = await testClient.SendActivityAsync<IMessageActivity>(Localizer[ResourceKeys.No]);
            reply.Text.Should().Be(Localizer[ResourceKeys.ReminderAdded, "Reminder text", expectedDate, expectedTime]);

            reply = testClient.GetNextReply<IMessageActivity>();
            reply.Text.Should().Be(Localizer[ResourceKeys.ReminderCreationTip]);

            // Check dialog result
            var conversation = testClient.DialogContext.Context.Activity.GetConversationReference();

            var result = (ReminderEntity) testClient.DialogTurnResult.Result;
            result.PartitionKey.Should().Be(conversation.User.Id);
            result.Text.Should().Be("Reminder text");
            result.DueDateTimeLocal.Should().Be(expectedDateTime.ToString("G", CultureInfo.InvariantCulture));
        }

        [Theory]
        [MemberData(nameof(DateTimeInputVariations.InvalidDates), MemberType = typeof(DateTimeInputVariations))]
        public async Task ShouldRejectInvalidInputAndPastDates(string invalidDateInput)
        {
            // Arrange
            var today = new DateTime(2023, 5, 6, 18, 30, 0); // 2023-05-06 18:30 - Saturday

            ConfigureLocalization("en-US", "Europe/London", today);

            _stateService.UserSettingsPropertyAccessor
                .GetAsync(Arg.Any<ITurnContext>(), Arg.Any<Func<UserSettings>>(), Arg.Any<CancellationToken>())
                .Returns(new UserSettings
                {
                    Culture = "Test culture",
                    Language = "Test language",
                    TimeZone = "Europe/London",
                    Location = "Test Location"
                });

            _clock.GetLocalDateTime(Arg.Any<string>())
                .Returns(today);

            var testClient = new DialogTestClient(Channels.Test, _sut, middlewares: Middlewares);

            // Act / Assert
            var reply = await testClient.SendActivityAsync<IMessageActivity>("/add");
            reply.Text.Should().Be(Localizer[ResourceKeys.AskForReminderText]);

            reply = await testClient.SendActivityAsync<IMessageActivity>("Reminder text");
            reply.Text.Should().Be(Localizer[ResourceKeys.AskForReminderDate]);

            reply = await testClient.SendActivityAsync<IMessageActivity>(invalidDateInput);
            reply.Text.Should().Be(Localizer[ResourceKeys.DateNotRecognized]);

            // Check dialog result
            testClient.DialogTurnResult.Status.Should().Be(DialogTurnStatus.Waiting);
        }


        private class DateTimeInputVariations
        {
            public static IEnumerable<object[]> ValidDates()
            {
                // Relative time inputs
                yield return new object[] { "in an hour", 0, 1 };
                yield return new object[] { "in 2 hours", 0, 2 };
                yield return new object[] { "2 hours from now", 0, 2 };
                yield return new object[] { "in 30 minutes", 0, 0, 30 };
                yield return new object[] { "in 90 minutes", 0, 1, 30 };

                // Specific time inputs
                yield return new object[] { "at 2 AM", 0, 2 };
                yield return new object[] { "2pm", 0, 14 };

                // Absolute date and time inputs with different formats
                yield return new object[] { "5/6/2023 10:00 AM", 0, 10 };
                yield return new object[] { "2023-05-08 20:00", 2, 20, 0 };
                yield return new object[] { "2023/05/08 20:00", 2, 20, 0 };

                // Relative day inputs
                yield return new object[] { "tomorrow", 1, 0 };
                yield return new object[] { "in 1 day", 1, 0 };
                yield return new object[] { "in 2 days", 2, 0 };
                yield return new object[] { "in 3 days", 3, 0 };

                // Relative day inputs with specific time
                yield return new object[] { "tomorrow at 2 PM", 1, 14 };
                yield return new object[] { "in 2 days at 3:30 PM", 2, 15, 30 };

                // Days of the week
                yield return new object[] { "on Monday", 2, 0 };
                yield return new object[] { "Monday at 5:30 PM", 2, 17, 30 };

                // Week-related inputs
                yield return new object[] { "in a week", 7, 0 };
                yield return new object[] { "next Saturday", 7, 0 };
                yield return new object[] { "next Saturday at 1:15 PM", 7, 13, 15 };
                yield return new object[] { "next Sunday at 07:00", 8, 7, 0 };
                yield return new object[] { "2 weeks from now", 14, 0 };
                yield return new object[] { "2 weeks from now at 3 PM", 14, 15 };

                // Inputs with day and month
                yield return new object[] { "on May 12th", 6, 0 };
                yield return new object[] { "May 12th at 8:30 PM", 6, 20, 30 };
                yield return new object[] { "on the 12th", 6, 0 };
                yield return new object[] { "on the 12th at 5 PM", 6, 17 };

                // Additional date formats
                yield return new object[] { "05-12-2023", 6, 0 };
                yield return new object[] { "05-12-2023 3:45 PM", 6, 15, 45 };
                yield return new object[] { "5/12", 6, 0 };
                yield return new object[] { "5/12 3:45 PM", 6, 15, 45 };

                // Longer period inputs
                yield return new object[] { "in 10 days at 11:45 AM", 10, 11, 45 };
                yield return new object[] { "in 3 weeks at 18:45", 21, 18, 45 };
                yield return new object[] { "in 1 month", 31, 0 };
                yield return new object[] { "in 1 month at 4 PM", 31, 16 };
                yield return new object[] { "in 2 months at 10:30 AM", 61, 10, 30 };
                yield return new object[] { "in 6 months", 184, 0 };
                yield return new object[] { "in 1 year", 366, 0 };
                yield return new object[] { "in 1 year at 2:15 PM", 366, 14, 15 };
                yield return new object[] { "in 2 years at 9:00 AM", 731, 9, 0 };
            }

            public static IEnumerable<object[]> InvalidDates()
            {
                yield return new object[] { "invalid" };
                yield return new object[] { "31/31" };
                yield return new object[] { "2022-12-31 23:59" }; // Past year
                yield return new object[] { "2023-04-30 12:00" }; // Past month
                yield return new object[] { "2023-05-05 18:00" }; // Past day
                yield return new object[] { "17:00" };           // Past time (same day)
            }

        }
    }
}