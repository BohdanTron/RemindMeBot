using System;
using System.Globalization;
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
        private readonly UserSettingsDialog _userSettingsDialog = Substitute.For<UserSettingsDialog>(null, null, null, null);

        public MainDialogTests(ITestOutputHelper output) : base(output)
        {
            _sut = new MainDialog(_userSettingsDialog, _stateService, Localizer);
        }

        [Fact]
        public async Task ShouldStartUserSettingsDialog_WhenStartCommandAndNoUserSettingsSet()
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

        [Fact]
        public async Task ShouldNotStartUserSettingsDialog_WhenStartCommandAndUserSettingsAlreadySet()
        {
            // Arrange
            _stateService.UserSettingsPropertyAccessor
                .GetAsync(Arg.Any<ITurnContext>(), Arg.Any<Func<UserSettings>>(), Arg.Any<CancellationToken>())
                .Returns(new UserSettings { TimeZoneId = "Europe/Kyiv" });

            var testClient = new DialogTestClient(Channels.Test, _sut, middlewares: Middlewares);

            // Act 
            var reply = await testClient.SendActivityAsync<IMessageActivity>("/start");

            // Assert
            reply.Text.Should().Be("What to remind you about?");
        }

        [Theory]
        [InlineData("en-US")]
        [InlineData("uk-UA")]
        public async Task ShouldSendUnknownCommandMessage_WhenCommandDifferentFromStart(string culture)
        {
            // Arrange
            // Set culture for the test
            SetCurrentCulture(culture);

            // Set culture for the dialog
            Middlewares.Add(new TestCultureMiddleware(new CultureInfo(culture)));

            var testClient = new DialogTestClient(Channels.Test, _sut, middlewares: Middlewares);

            // Act
            var reply = await testClient.SendActivityAsync<IMessageActivity>("/unknown-command");

            // Assert
            reply.Text.Should().Be(Localizer[ResourceKeys.UnknownCommand].Value);
        }
    }
}
