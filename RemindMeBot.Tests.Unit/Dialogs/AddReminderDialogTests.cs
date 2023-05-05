﻿using System;
using System.Threading;
using System.Threading.Tasks;
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

        public AddReminderDialogTests(ITestOutputHelper output) : base(output)
        {
            _sut = new AddReminderDialog(_stateService, _translationService, _clock, Localizer);
        }

        /// <summary>
        /// User: /add-reminder
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
        [InlineData("Українська", "uk-UA", "Europe/Kiev", "Винести сміття", "завтра о 14:00", "щотижня")]
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

            ConfigureCulture(culture, userSettings.TimeZone);

            _stateService.UserSettingsPropertyAccessor
                .GetAsync(Arg.Any<ITurnContext>(), Arg.Any<Func<UserSettings>>(), Arg.Any<CancellationToken>())
                .Returns(userSettings);

            _translationService
                .Translate(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
                .Returns("tomorrow at 14:00");

            var testClient = new DialogTestClient(Channels.Test, _sut, middlewares: Middlewares);

            // Act / Assert
            var reply = await testClient.SendActivityAsync<IMessageActivity>("/add-reminder");
            reply.Text.Should().Be(Localizer[ResourceKeys.AskForReminderText]);

            reply = await testClient.SendActivityAsync<IMessageActivity>(reminderText);
            reply.Text.Should().Be(Localizer[ResourceKeys.AskForReminderDate]);

            var reminderDateTime = DateTime.Now.AddDays(1).Date.AddHours(14);
            reply = await testClient.SendActivityAsync<IMessageActivity>(reminderTime);
            reply.Text.Should().Be($"{Localizer[ResourceKeys.AskWhetherToRepeatReminder]} (1) {Localizer[ResourceKeys.Yes]} or (2) {Localizer[ResourceKeys.No]}");

            reply = await testClient.SendActivityAsync<IMessageActivity>(Localizer[ResourceKeys.Yes]);
            reply.Text.Should().Be($"{Localizer[ResourceKeys.AskForRepeatInterval]}\n\n   1. {Localizer[ResourceKeys.Daily]}\n   2. {Localizer[ResourceKeys.Weekly]}\n   3. {Localizer[ResourceKeys.Monthly]}\n   4. {Localizer[ResourceKeys.Yearly]}");

            reply = await testClient.SendActivityAsync<IMessageActivity>(Localizer[ResourceKeys.Weekly]);
            reply.Text.Should().Be(Localizer[ResourceKeys.RepeatedReminderAdded, reminderText, reminderDateTime, repeatInterval]);

            // Check dialog result
            testClient.DialogTurnResult.Result.Should().Be(new Reminder
            {
                Text = reminderText,
                Date = reminderDateTime,
                ShouldRepeat = true,
                RepeatInterval = repeatInterval
            });
        }

        /// <summary>
        /// User: /add-reminder
        /// Bot: Please enter the title of the reminder:
        /// User: Take out the trash
        /// Bot: When should I remind you?
        /// User: tomorrow at 2 PM
        /// Bot: Do you want this reminder to repeat ? (yes / no)
        /// User: no
        /// Bot: Reminder added: "Take out the trash" on 2023 - 04 - 17 18:00.
        /// </summary>
        [Theory]
        [InlineData("English", "en-US", "Europe/London", "Take out the trash", "tomorrow at 2 PM")]
        [InlineData("Українська", "uk-UA", "Europe/Kiev", "Винести сміття", "завтра о 14:00")]
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

            ConfigureCulture(culture, userSettings.TimeZone);

            _stateService.UserSettingsPropertyAccessor
                .GetAsync(Arg.Any<ITurnContext>(), Arg.Any<Func<UserSettings>>(), Arg.Any<CancellationToken>())
                .Returns(userSettings);

            _translationService
                .Translate(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
                .Returns("tomorrow at 14:00");

            var testClient = new DialogTestClient(Channels.Test, _sut, middlewares: Middlewares);

            // Act / Assert
            var reply = await testClient.SendActivityAsync<IMessageActivity>("/add-reminder");
            reply.Text.Should().Be(Localizer[ResourceKeys.AskForReminderText]);

            reply = await testClient.SendActivityAsync<IMessageActivity>(reminderText);
            reply.Text.Should().Be(Localizer[ResourceKeys.AskForReminderDate]);

            var reminderDateTime = DateTime.Now.AddDays(1).Date.AddHours(14);
            reply = await testClient.SendActivityAsync<IMessageActivity>(reminderTime);
            reply.Text.Should().Be($"{Localizer[ResourceKeys.AskWhetherToRepeatReminder]} (1) {Localizer[ResourceKeys.Yes]} or (2) {Localizer[ResourceKeys.No]}");

            reply = await testClient.SendActivityAsync<IMessageActivity>(Localizer[ResourceKeys.No]);
            reply.Text.Should().Be(Localizer[ResourceKeys.ReminderAdded, reminderText, reminderDateTime]);

            // Check dialog result
            testClient.DialogTurnResult.Result.Should().Be(new Reminder
            {
                Text = reminderText,
                Date = reminderDateTime,
                ShouldRepeat = false,
                RepeatInterval = null
            });
        }

        [Theory]

        // Relative time inputs
        [InlineData("in an hour", 0, 1)]
        [InlineData("in 2 hours", 0, 2)]
        [InlineData("2 hours from now", 0, 2)]
        [InlineData("in 30 minutes", 0, 0, 30)]
        [InlineData("in 90 minutes", 0, 1, 30)]

        // Specific time inputs
        [InlineData("at 2 AM", 0, 2)]
        [InlineData("2pm", 0, 14)]

        // Absolute date and time inputs with different formats
        [InlineData("5/6/2023 10:00 AM", 0, 10)]
        [InlineData("2023-05-08 20:00", 2, 20, 0)]
        [InlineData("2023/05/08 20:00", 2, 20, 0)]

        // Relative day inputs
        [InlineData("tomorrow", 1, 0)]
        [InlineData("in 1 day", 1, 0)]
        [InlineData("in 2 days", 2, 0)]
        [InlineData("in 3 days", 3, 0)]

        // Relative day inputs with specific time
        [InlineData("tomorrow at 2 PM", 1, 14)]
        [InlineData("in 2 days at 3:30 PM", 2, 15, 30)]

        // Days of the week
        [InlineData("on Monday", 2, 0)]
        [InlineData("Monday at 5:30 PM", 2, 17, 30)]

        // Week-related inputs
        [InlineData("in a week", 7, 0)]
        [InlineData("next Saturday", 7, 0)]
        [InlineData("next Saturday at 1:15 PM", 7, 13, 15)]
        [InlineData("next Sunday at 07:00", 8, 7, 0)]
        [InlineData("2 weeks from now", 14, 0)]
        [InlineData("2 weeks from now at 3 PM", 14, 15)]

        // Inputs with day and month
        [InlineData("on May 12th", 6, 0)]
        [InlineData("May 12th at 8:30 PM", 6, 20, 30)]
        [InlineData("on the 12th", 6, 0)]
        [InlineData("on the 12th at 5 PM", 6, 17)]

        // Additional date formats
        [InlineData("05/12/2023", 6, 0)]
        [InlineData("05/12/2023 3:45 PM", 6, 15, 45)]
        [InlineData("05-12-2023", 6, 0)]
        [InlineData("05-12-2023 3:45 PM", 6, 15, 45)]
        [InlineData("5/12", 6, 0)]
        [InlineData("5/12 3:45 PM", 6, 15, 45)]

        // Longer period inputs
        [InlineData("in 10 days at 11:45 AM", 10, 11, 45)]
        [InlineData("in 3 weeks at 18:45", 21, 18, 45)]
        [InlineData("in 1 month", 31, 0)]
        [InlineData("in 1 month at 4 PM", 31, 16)]
        [InlineData("in 2 months at 10:30 AM", 61, 10, 30)]
        [InlineData("in 6 months", 184, 0)]
        [InlineData("in 1 year", 366, 0)]
        [InlineData("in 1 year at 2:15 PM", 366, 14, 15)]
        [InlineData("in 2 years at 9:00 AM", 731, 9, 0)]
        public async Task ShouldRecognizeDifferentDateInputs(string userDateInput, int days, int hours, int minutes = 0)
        {
            // Arrange
            var today = new DateTime(2023, 5, 6, 0, 0, 0); // 2023-05-06 00:00 - Saturday

            ConfigureCulture("en-US", "Europe/London", today);

            var expectedReminderDay = today.Date.AddDays(days).AddHours(hours).AddMinutes(minutes);

            _stateService.UserSettingsPropertyAccessor
                .GetAsync(Arg.Any<ITurnContext>(), Arg.Any<Func<UserSettings>>(), Arg.Any<CancellationToken>())
                .Returns(new UserSettings());

            _clock.GetLocalDateTime(Arg.Any<string>())
                .Returns(today);

            var testClient = new DialogTestClient(Channels.Test, _sut, middlewares: Middlewares);

            // Act / Assert
            var reply = await testClient.SendActivityAsync<IMessageActivity>("/add-reminder");
            reply.Text.Should().Be(Localizer[ResourceKeys.AskForReminderText]);

            reply = await testClient.SendActivityAsync<IMessageActivity>("Reminder text");
            reply.Text.Should().Be(Localizer[ResourceKeys.AskForReminderDate]);

            reply = await testClient.SendActivityAsync<IMessageActivity>(userDateInput);
            reply.Text.Should().Be($"{Localizer[ResourceKeys.AskWhetherToRepeatReminder]} (1) {Localizer[ResourceKeys.Yes]} or (2) {Localizer[ResourceKeys.No]}");

            reply = await testClient.SendActivityAsync<IMessageActivity>(Localizer[ResourceKeys.No]);
            reply.Text.Should().Be(Localizer[ResourceKeys.ReminderAdded, "Reminder text", expectedReminderDay]);

            // Check dialog result
            testClient.DialogTurnResult.Result.Should().Be(new Reminder
            {
                Text = "Reminder text",
                Date = expectedReminderDay,
                ShouldRepeat = false,
                RepeatInterval = null
            });
        }

        [Theory]
        [InlineData("invalid")]
        [InlineData("2022-12-31 23:59")] // Past year
        [InlineData("2023-04-30 12:00")] // Past month
        [InlineData("2023-05-05 18:00")] // Past day
        [InlineData("17:00")]            // Past time (same day)
        public async Task ShouldRejectInvalidDateInputs(string invalidDateInput)
        {
            // Arrange
            var today = new DateTime(2023, 5, 6, 18, 30, 0); // 2023-05-06 18:30 - Saturday

            ConfigureCulture("en-US", "Europe/London", today);

            _stateService.UserSettingsPropertyAccessor
                .GetAsync(Arg.Any<ITurnContext>(), Arg.Any<Func<UserSettings>>(), Arg.Any<CancellationToken>())
                .Returns(new UserSettings());

            _clock.GetLocalDateTime(Arg.Any<string>())
                .Returns(today);

            var testClient = new DialogTestClient(Channels.Test, _sut, middlewares: Middlewares);

            // Act / Assert
            var reply = await testClient.SendActivityAsync<IMessageActivity>("/add-reminder");
            reply.Text.Should().Be(Localizer[ResourceKeys.AskForReminderText]);

            reply = await testClient.SendActivityAsync<IMessageActivity>("Reminder text");
            reply.Text.Should().Be(Localizer[ResourceKeys.AskForReminderDate]);

            reply = await testClient.SendActivityAsync<IMessageActivity>(invalidDateInput);
            reply.Text.Should().Be(Localizer[ResourceKeys.DateNotRecognized]);

            // Check dialog result
            testClient.DialogTurnResult.Status.Should().Be(DialogTurnStatus.Waiting);
        }

    }
}