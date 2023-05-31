using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Azure.Data.Tables;
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
    public class ReminderListDialogTests : BotTestBase
    {
        private readonly RemindersListDialog _sut;

        private readonly IStateService _stateService = Substitute.For<IStateService>();
        private readonly ReminderTableService _reminderTableService = Substitute.ForPartsOf<ReminderTableService>(Substitute.For<TableServiceClient>());

        public ReminderListDialogTests(ITestOutputHelper output) : base(output)
        {
            _sut = new RemindersListDialog(_stateService, _reminderTableService, Localizer);
        }

        [Theory]
        [InlineData("en-US")]
        [InlineData("uk-UA")]
        public async Task ShouldDisplayRemindersList_WhenRemindersExist(string culture)
        {
            // Arrange
            ConfigureLocalization(culture);

            _stateService.UserSettingsPropertyAccessor
                .GetAsync(Arg.Any<ITurnContext>(), Arg.Any<Func<UserSettings>>(), Arg.Any<CancellationToken>())
                .Returns(new UserSettings { TimeZone = "Europe/Kyiv" });

            var firstReminderTime = "05/27/2023 19:11:18";
            var secondReminderTime = "11/11/2023 00:00:00";

            var reminders = new List<ReminderEntity>
            {
                new()
                {
                    Text = "Call mom",
                    DueDateTimeLocal = firstReminderTime,
                    TimeZone = "Europe/Kyiv",
                    RowKey = Guid.NewGuid().ToString(),
                },
                new()
                {
                    Text = "Mom's birthday",
                    DueDateTimeLocal = secondReminderTime,
                    TimeZone = "Europe/Kyiv",
                    RepeatInterval = "yearly",
                    RowKey = Guid.NewGuid().ToString(),
                }
            };

            _reminderTableService.GetList(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(reminders);

            var firstDisplayTime = DateTime.Parse(firstReminderTime, CultureInfo.InvariantCulture).ToString("g", CultureInfo.CurrentCulture);
            var secondDisplayTime = DateTime.Parse(secondReminderTime, CultureInfo.InvariantCulture).ToString("g", CultureInfo.CurrentCulture);

            var displayList = $"1) *Call mom* \n📅  {firstDisplayTime}\n\n2) *Mom's birthday* \n📅  {secondDisplayTime}\n";
            var replyText = $"{Localizer[ResourceKeys.RemindersList]}\n\n{displayList}";

            var suggestedActions = new SuggestedActions
            {
                Actions = new List<CardAction>
                {
                    new() { Title = "🗑️ 1", Type = ActionTypes.ImBack, Value = reminders[0].RowKey },
                    new() { Title = "🗑️ 2", Type = ActionTypes.ImBack, Value = reminders[1].RowKey }
                }
            };

            var testClient = new DialogTestClient(Channels.Test, _sut, middlewares: Middlewares);

            // Act
            var reply = await testClient.SendActivityAsync<IMessageActivity>("/list");

            // Assert
            reply.Text.Should().Be(replyText);
            reply.SuggestedActions.Should().BeEquivalentTo(suggestedActions);

            testClient.DialogTurnResult.Status.Should().Be(DialogTurnStatus.Waiting);
        }

        [Theory]
        [InlineData("en-US")]
        [InlineData("uk-UA")]
        public async Task ShouldDeleteReminderFromList_WhenSelected(string culture)
        {
            // Arrange
            ConfigureLocalization(culture);

            _stateService.UserSettingsPropertyAccessor
                .GetAsync(Arg.Any<ITurnContext>(), Arg.Any<Func<UserSettings>>(), Arg.Any<CancellationToken>())
                .Returns(new UserSettings { TimeZone = "Europe/Kyiv" });

            var reminders = new List<ReminderEntity>
            {
                new()
                {
                    Text = "Call mom",
                    DueDateTimeLocal = "05/27/2023 19:11:18",
                    TimeZone = "Europe/Kyiv",
                    RowKey = Guid.NewGuid().ToString(),
                },
                new()
                {
                    Text = "Mom's birthday",
                    DueDateTimeLocal = "11/11/2023 00:00:00",
                    TimeZone = "Europe/Kyiv",
                    RepeatInterval = "yearly",
                    RowKey = Guid.NewGuid().ToString(),
                }
            };

            _reminderTableService.GetList(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(reminders);

            _reminderTableService.Delete(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);

            _reminderTableService.Get(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(reminders[0]);

            var testClient = new DialogTestClient(Channels.Test, _sut, middlewares: Middlewares);

            // Act / Assert
            await testClient.SendActivityAsync<IMessageActivity>("/list");
            testClient.DialogTurnResult.Status.Should().Be(DialogTurnStatus.Waiting);

            var reply = await testClient.SendActivityAsync<IMessageActivity>(reminders[0].RowKey);
            reply.Text.Should().Be(Localizer[ResourceKeys.ReminderDeleted]);
        }

        [Fact]
        public async Task ShouldNotDeleteReminder_WhenNotPresentInList()
        {
            // Arrange
            _stateService.UserSettingsPropertyAccessor
                .GetAsync(Arg.Any<ITurnContext>(), Arg.Any<Func<UserSettings>>(), Arg.Any<CancellationToken>())
                .Returns(new UserSettings { TimeZone = "Europe/Kyiv" });

            var reminders = new List<ReminderEntity>
            {
                new()
                {
                    Text = "Call mom",
                    DueDateTimeLocal = "05/27/2023 19:11:18",
                    TimeZone = "Europe/Kyiv",
                    RowKey = Guid.NewGuid().ToString(),
                },
                new()
                {
                    Text = "Mom's birthday",
                    DueDateTimeLocal = "11/11/2023 00:00:00",
                    TimeZone = "Europe/Kyiv",
                    RepeatInterval = "yearly",
                    RowKey = Guid.NewGuid().ToString(),
                }
            };

            _reminderTableService.GetList(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(reminders);

            _reminderTableService.Get(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns((ReminderEntity?) null);

            var testClient = new DialogTestClient(Channels.Test, _sut, middlewares: Middlewares);

            // Act / Assert
            await testClient.SendActivityAsync<IMessageActivity>("/list");
            testClient.DialogTurnResult.Status.Should().Be(DialogTurnStatus.Waiting);

            await testClient.SendActivityAsync<IMessageActivity>(Guid.NewGuid().ToString());
            testClient.DialogTurnResult.Status.Should().Be(DialogTurnStatus.Complete);

            var result = (RemindersListDialogResult) testClient.DialogTurnResult.Result;
            result.ItemDeleted.Should().BeFalse();
        }

        [Fact]
        public async Task ShouldNotDeleteReminder_WhenInvalidSelection()
        {
            // Arrange
            _stateService.UserSettingsPropertyAccessor
                .GetAsync(Arg.Any<ITurnContext>(), Arg.Any<Func<UserSettings>>(), Arg.Any<CancellationToken>())
                .Returns(new UserSettings { TimeZone = "Europe/Kyiv" });

            var reminders = new List<ReminderEntity>
            {
                new()
                {
                    Text = "Call mom",
                    DueDateTimeLocal = "05/27/2023 19:11:18",
                    TimeZone = "Europe/Kyiv",
                    RowKey = Guid.NewGuid().ToString(),
                },
                new()
                {
                    Text = "Mom's birthday",
                    DueDateTimeLocal = "11/11/2023 00:00:00",
                    TimeZone = "Europe/Kyiv",
                    RepeatInterval = "yearly",
                    RowKey = Guid.NewGuid().ToString(),
                }
            };

            _reminderTableService.GetList(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(reminders);

            _reminderTableService.Get(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns((ReminderEntity?) null);

            var testClient = new DialogTestClient(Channels.Test, _sut, middlewares: Middlewares);

            // Act / Assert
            await testClient.SendActivityAsync<IMessageActivity>("/list");
            testClient.DialogTurnResult.Status.Should().Be(DialogTurnStatus.Waiting);

            await testClient.SendActivityAsync<IMessageActivity>("invalid user input");
            testClient.DialogTurnResult.Status.Should().Be(DialogTurnStatus.Complete);

            var result = (RemindersListDialogResult) testClient.DialogTurnResult.Result;
            result.ItemDeleted.Should().BeFalse();
        }

        [Theory]
        [InlineData("en-US")]
        [InlineData("uk-UA")]
        public async Task ShouldEndDialog_WhenNoRemindersExist(string culture)
        {
            // Arrange
            ConfigureLocalization(culture);

            _stateService.UserSettingsPropertyAccessor
                .GetAsync(Arg.Any<ITurnContext>(), Arg.Any<Func<UserSettings>>(), Arg.Any<CancellationToken>())
                .Returns(new UserSettings { TimeZone = "Europe/Kyiv" });

            _reminderTableService.GetList(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new List<ReminderEntity>());

            var testClient = new DialogTestClient(Channels.Test, _sut, middlewares: Middlewares);

            // Act
            var reply = await testClient.SendActivityAsync<IMessageActivity>("/list");

            // Assert
            reply.Text.Should().Be(Localizer[ResourceKeys.NoRemindersCreated]);
            testClient.DialogTurnResult.Status.Should().Be(DialogTurnStatus.Complete);
        }
    }
}
