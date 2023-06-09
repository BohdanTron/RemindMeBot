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
    public class MainDialogTests : BotTestBase
    {
        private readonly MainDialog _sut;

        private readonly IStateService _stateService = Substitute.For<IStateService>();
        private readonly UserSettingsDialog _userSettingsDialog = Substitute.For<UserSettingsDialog>(null, null, null, null, null);
        private readonly ChangeUserSettingsDialog _changeUserSettingsDialog = Substitute.For<ChangeUserSettingsDialog>(null, null, null, null, null, null);
        private readonly AddReminderDialog _addReminderDialog = Substitute.For<AddReminderDialog>(null, null, null, null, null, null, null);
        private readonly RemindersListDialog _remindersListDialog = Substitute.For<RemindersListDialog>(null, null, null, null);
        private readonly CreateQuickReminderDialog _createQuickReminderDialog = Substitute.For<CreateQuickReminderDialog>(null, null, null, null, null, null, null, null);

        public MainDialogTests(ITestOutputHelper output) : base(output)
        {
            _sut = new MainDialog(
                _stateService,
                _userSettingsDialog,
                _changeUserSettingsDialog,
                _addReminderDialog,
                _remindersListDialog,
                _createQuickReminderDialog,
                Localizer);
        }

        [Fact]
        public async Task ShouldBeginUserSettingsDialog_WhenStartCommandAndNoUserSettingsSet()
        {
            // Arrange
            _userSettingsDialog
                .BeginDialogAsync(Arg.Any<DialogContext>(), Arg.Any<object>(), Arg.Any<CancellationToken>())
                .Returns(async callInfo =>
                {
                    var dialogContext = callInfo.Arg<DialogContext>();
                    var cancellationToken = callInfo.Arg<CancellationToken>();

                    await dialogContext.Context.SendActivityAsync("Welcome to the RemindMe chatbot! Please choose your language: (1) English or (2) Українська", cancellationToken: cancellationToken);

                    return await dialogContext.EndDialogAsync(cancellationToken: cancellationToken);
                });

            var testClient = new DialogTestClient(Channels.Test, _sut, middlewares: Middlewares);

            // Act 
            var reply = await testClient.SendActivityAsync<IMessageActivity>("/start");

            // Assert
            reply.Text.Should().Be("Welcome to the RemindMe chatbot! Please choose your language: (1) English or (2) Українська");
        }

        [Theory]
        [InlineData("en-US")]
        [InlineData("uk-UA")]
        public async Task ShouldNotBeginUserSettingsDialog_WhenStartCommandAndUserSettingsAlreadySet(string culture)
        {
            // Arrange
            ConfigureLocalization(culture);

            _stateService.UserSettingsPropertyAccessor
                .GetAsync(Arg.Any<ITurnContext>(), Arg.Any<Func<UserSettings>>(), Arg.Any<CancellationToken>())
                .Returns(new UserSettings { TimeZone = "Europe/Kyiv" });
            var testClient = new DialogTestClient(Channels.Test, _sut, middlewares: Middlewares);

            // Act 
            var reply = await testClient.SendActivityAsync<IMessageActivity>("/start");

            // Assert
            reply.Text.Should().Be(Localizer[ResourceKeys.WhatToRemindYouAbout]);
        }

        [Fact]
        public async Task ShouldBeginQuickReminderDialog_WhenUnknownCommand()
        {
            // Arrange
            _createQuickReminderDialog
                .BeginDialogAsync(Arg.Any<DialogContext>(), Arg.Any<object>(), Arg.Any<CancellationToken>())
                .Returns(async callInfo =>
                {
                    var dialogContext = callInfo.Arg<DialogContext>();
                    var cancellationToken = callInfo.Arg<CancellationToken>();

                    await dialogContext.Context.SendActivityAsync("Reminder was added", cancellationToken: cancellationToken);

                    return await dialogContext.EndDialogAsync(cancellationToken: cancellationToken);
                });

            var testClient = new DialogTestClient(Channels.Test, _sut, middlewares: Middlewares);

            // Act
            var reply = await testClient.SendActivityAsync<IMessageActivity>("/unknown-command");

            // Assert
            reply.Text.Should().Be("Reminder was added");
        }

        [Theory]
        [InlineData("en-US")]
        [InlineData("uk-UA")]
        public async Task ShouldReplyWithNoActiveOperationsMessage_WhenCancelCommand(string culture)
        {
            // Arrange
            ConfigureLocalization(culture);
            var testClient = new DialogTestClient(Channels.Test, _sut, middlewares: Middlewares);

            // Act
            var reply = await testClient.SendActivityAsync<IMessageActivity>("/cancel");

            // Assert
            reply.Text.Should().Be(Localizer[ResourceKeys.NoActiveOperations]);
        }

        [Fact]
        public async Task ShouldBeginChangeUserSettingsDialog_WhenMySettingsCommand()
        {
            // Arrange
            _changeUserSettingsDialog
                .BeginDialogAsync(Arg.Any<DialogContext>(), Arg.Any<object>(), Arg.Any<CancellationToken>())
                .Returns(async callInfo =>
                {
                    var dialogContext = callInfo.Arg<DialogContext>();
                    var cancellationToken = callInfo.Arg<CancellationToken>();

                    await dialogContext.Context.SendActivityAsync("Your current settings:", cancellationToken: cancellationToken);

                    return await dialogContext.EndDialogAsync(cancellationToken: cancellationToken);
                });


            var testClient = new DialogTestClient(Channels.Test, _sut, middlewares: Middlewares);

            // Act
            var reply = await testClient.SendActivityAsync<IMessageActivity>("/settings");

            // Assert
            reply.Text.Should().Be("Your current settings:");
        }

        [Fact]
        public async Task ShouldBeginAddReminderDialog_WhenAddReminderCommand()
        {
            // Arrange
            _addReminderDialog
                .BeginDialogAsync(Arg.Any<DialogContext>(), Arg.Any<object>(), Arg.Any<CancellationToken>())
                .Returns(async callInfo =>
                {
                    var dialogContext = callInfo.Arg<DialogContext>();
                    var cancellationToken = callInfo.Arg<CancellationToken>();

                    await dialogContext.Context.SendActivityAsync("What to remind you about?", cancellationToken: cancellationToken);

                    return await dialogContext.EndDialogAsync(cancellationToken: cancellationToken);
                });

            var testClient = new DialogTestClient(Channels.Test, _sut, middlewares: Middlewares);

            // Act
            var reply = await testClient.SendActivityAsync<IMessageActivity>("/add");

            // Assert
            reply.Text.Should().Be("What to remind you about?");
        }

        [Fact]
        public async Task ShouldBeginRemindersListDialog_WhenListCommand()
        {
            // Arrange
            _remindersListDialog
                .BeginDialogAsync(Arg.Any<DialogContext>(), Arg.Any<object>(), Arg.Any<CancellationToken>())
                .Returns(async callInfo =>
                {
                    var dialogContext = callInfo.Arg<DialogContext>();
                    var cancellationToken = callInfo.Arg<CancellationToken>();

                    await dialogContext.Context.SendActivityAsync("Here's the list of your reminders:", cancellationToken: cancellationToken);

                    return await dialogContext.EndDialogAsync(cancellationToken: cancellationToken);
                });

            var testClient = new DialogTestClient(Channels.Test, _sut, middlewares: Middlewares);

            // Act
            var reply = await testClient.SendActivityAsync<IMessageActivity>("/list");

            // Assert
            reply.Text.Should().Be("Here's the list of your reminders:");
        }

        [Fact]
        public async Task ShouldEndDialog_WhenReminderWasDeletedFromList()
        {
            // Arrange
            _remindersListDialog
                .BeginDialogAsync(Arg.Any<DialogContext>(), Arg.Any<object>(), Arg.Any<CancellationToken>())
                .Returns(async callInfo =>
                {
                    var dialogContext = callInfo.Arg<DialogContext>();
                    var cancellationToken = callInfo.Arg<CancellationToken>();

                    await dialogContext.Context.SendActivityAsync("Here's the list of your reminders:", cancellationToken: cancellationToken);

                    return await dialogContext.EndDialogAsync(new RemindersListDialogResult { ItemDeleted = true },
                        cancellationToken: cancellationToken);
                });

            // Act
            var testClient = new DialogTestClient(Channels.Test, _sut, middlewares: Middlewares);
            await testClient.SendActivityAsync<IMessageActivity>("/list");

            // Assert
            testClient.DialogTurnResult.Status.Should().Be(DialogTurnStatus.Complete);
        }
    }
}
