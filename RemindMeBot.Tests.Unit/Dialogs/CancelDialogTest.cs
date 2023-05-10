using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Testing;
using Microsoft.Bot.Connector;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Localization;
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
    public class CancelDialogTest : BotTestBase
    {
        private readonly IStateService _stateService = Substitute.For<IStateService>();

        private readonly TestCancelDialog _sut;

        public CancelDialogTest(ITestOutputHelper output) : base(output)
        {
            _sut = new TestCancelDialog(_stateService, Localizer);
        }

        [Theory]
        [InlineData("en-US")]
        [InlineData("uk-UA")]
        public async Task ShouldBeAbleToCancel(string culture)
        {
            // Arrange
            ConfigureLocalization(culture);

            _stateService.UserSettingsPropertyAccessor
                .GetAsync(Arg.Any<ITurnContext>(), Arg.Any<Func<UserSettings>>(), Arg.Any<CancellationToken>())
                .Returns(new UserSettings
                {
                    Culture = "Test culture",
                    Language = "Test language",
                    TimeZone = "Europe/London",
                    Location = "Test Location"
                });

            var testClient = new DialogTestClient(Channels.Test, _sut, middlewares: Middlewares);

            // Act / Assert
            var reply = await testClient.SendActivityAsync<IMessageActivity>("hi");
            reply.Text.Should().Be("Hi there");
            testClient.DialogTurnResult.Status.Should().Be(DialogTurnStatus.Waiting);

            reply = await testClient.SendActivityAsync<IMessageActivity>("/cancel");
            reply.Text.Should().Be(Localizer[ResourceKeys.OperationCancelled]);
            testClient.DialogTurnResult.Status.Should().Be(DialogTurnStatus.Complete);
        }

        [Fact]
        public async Task ShouldNotCancel_WhenItIsNotMessage()
        {
            // Arrange
            _stateService.UserSettingsPropertyAccessor
                .GetAsync(Arg.Any<ITurnContext>(), Arg.Any<Func<UserSettings>>(), Arg.Any<CancellationToken>())
                .Returns(new UserSettings
                {
                    Culture = "Test culture",
                    Language = "Test language",
                    TimeZone = "Europe/London",
                    Location = "Test Location"
                });

            var testClient = new DialogTestClient(Channels.Test, _sut, middlewares: Middlewares);

            // Act / Assert
            var reply = await testClient.SendActivityAsync<IMessageActivity>(new Activity { Type = ActionTypes.ShowImage, Value = "/cancel" });
            reply.Text.Should().Be("Hi there");
            testClient.DialogTurnResult.Status.Should().Be(DialogTurnStatus.Waiting);

            reply = await testClient.SendActivityAsync<IMessageActivity>(new Activity { Type = ActionTypes.ShowImage, Value = "/cancel" });
            reply.Should().BeNull();
        }

        [Theory]
        [InlineData("en-US")]
        [InlineData("uk-UA")]
        public async Task ShouldEndDialog_WhenNoUserSettingsExist(string culture)
        {
            // Arrange
            ConfigureLocalization(culture);
            _stateService.UserSettingsPropertyAccessor
                .GetAsync(Arg.Any<ITurnContext>(), Arg.Any<Func<UserSettings>>(), Arg.Any<CancellationToken>())
                .Returns(new UserSettings());

            var testClient = new DialogTestClient(Channels.Test, _sut, middlewares: Middlewares);

            // Act 
            var reply = await testClient.SendActivityAsync<IMessageActivity>("hi");

            // Assert
            reply.Text.Should().Be(Localizer[ResourceKeys.NoUserSettings]);
            testClient.DialogTurnResult.Status.Should().Be(DialogTurnStatus.Complete);
        }


        [Theory]
        [InlineData("en-US")]
        [InlineData("uk-UA")]
        public async Task ShouldContinueDialog_WhenUserSettingsExist(string culture)
        {
            // Arrange
            ConfigureLocalization(culture);

            _stateService.UserSettingsPropertyAccessor
                .GetAsync(Arg.Any<ITurnContext>(), Arg.Any<Func<UserSettings>>(), Arg.Any<CancellationToken>())
                .Returns(new UserSettings
                {
                    Culture = culture,
                    Language = "Test language",
                    TimeZone = "Europe/London",
                    Location = "Test Location"
                });

            var testClient = new DialogTestClient(Channels.Test, _sut, middlewares: Middlewares);

            // Act 
            var reply = await testClient.SendActivityAsync<IMessageActivity>("hi");

            // Assert
            reply.Text.Should().Be("Hi there");
            testClient.DialogTurnResult.Status.Should().Be(DialogTurnStatus.Waiting);
        }
    }

    public class TestCancelDialog : CancelDialog
    {
        public TestCancelDialog(IStateService stateService, IStringLocalizer<BotMessages> localizer)
            : base(nameof(TestCancelDialog), stateService, localizer)
        {
            AddDialog(new TextPrompt(nameof(TextPrompt)));
            AddDialog(new WaterfallDialog("testWaterfall",
                new WaterfallStep[]
                {
                    CheckUserSettingsExistStep,
                    PromptStep
                }));

            InitialDialogId = "testWaterfall";
        }

        private static Task<DialogTurnResult> PromptStep(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            return stepContext.PromptAsync(nameof(TextPrompt), new PromptOptions
            {
                Prompt = MessageFactory.Text("Hi there")
            }, cancellationToken);
        }
    }
}
