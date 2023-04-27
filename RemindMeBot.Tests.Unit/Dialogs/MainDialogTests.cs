using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Testing;
using Microsoft.Bot.Connector;
using Microsoft.Bot.Schema;
using NSubstitute;
using RemindMeBot.Dialogs;
using RemindMeBot.Models;
using RemindMeBot.Resources;
using RemindMeBot.Tests.Unit.Common;
using Xunit;
using Xunit.Abstractions;

namespace RemindMeBot.Tests.Unit.Dialogs
{
    public class MainDialogTests : BotTestBase
    {
        private readonly MainDialog _sut;

        private readonly UserSettingsDialog _userSettingsDialog = Substitute.For<UserSettingsDialog>(null, null, null, null);

        public MainDialogTests(ITestOutputHelper output) : base(output)
        {
            _userSettingsDialog
                .BeginDialogAsync(Arg.Any<DialogContext>(), Arg.Any<object>(), Arg.Any<CancellationToken>())
                .Returns(async callInfo =>
                {
                    var dialogContext = callInfo.Arg<DialogContext>();
                    var cancellationToken = callInfo.Arg<CancellationToken>();

                    // Send a generic activity so we can assert that the dialog was invoked
                    await dialogContext.Context.SendActivityAsync("Welcome to the RemindMe chatbot! Please choose your language: (1) English or (2) Українська", cancellationToken: cancellationToken);

                    var expectedUserSettings = new UserSettings
                    {
                        Language = "English",
                        Culture = "en-US",
                        Location = "London, United Kingdom",
                        TimeZoneId = "Europe/London"
                    };

                    // Return the UserSettings we need without executing the dialog logic
                    return await dialogContext.EndDialogAsync(expectedUserSettings, cancellationToken);
                });

            _sut = new MainDialog(_userSettingsDialog, StateService, Localizer);
        }

        [Theory]
        [InlineData("en-US")]
        [InlineData("uk-UA")]
        public async Task ShouldHandleMainFlow(string culture)
        {
            // Arrange
            
            // Set current culture for the test
            SetCurrentCulture(culture);

            // Set current culture for the dialog
            Middlewares.Add(new TestCultureMiddleware(new CultureInfo(culture)));

            var testClient = new DialogTestClient(Channels.Test, _sut, middlewares: Middlewares);

            // Act / Assert

            // Step 1 - "/start" command with no settings set
            var reply = await testClient.SendActivityAsync<IMessageActivity>("/start");
            reply.Text.Should().Be("Welcome to the RemindMe chatbot! Please choose your language: (1) English or (2) Українська");

            // Set up the state to return a user setting with a local time
            var userSettings = new UserSettings { TimeZoneId = "Europe/Kyiv" };
            await StateService.UserSettingsPropertyAccessor.SetAsync(testClient.DialogContext.Context, userSettings);
            await StateService.UserState.SaveChangesAsync(testClient.DialogContext.Context);

            // Step 2 - "/start" command with settings set
            reply = await testClient.SendActivityAsync<IMessageActivity>("/start");
            reply.Text.Should().Be("What to remind you about?");

            // Step 3 - unknown command
            reply = await testClient.SendActivityAsync<IMessageActivity>("/unknown-command");
            reply.Text.Should().Be(Localizer[ResourceKeys.UnknownCommand].Value);
        }
    }
}
