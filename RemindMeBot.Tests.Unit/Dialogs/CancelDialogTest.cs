using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Testing;
using Microsoft.Bot.Connector;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Localization;
using RemindMeBot.Dialogs;
using RemindMeBot.Resources;
using RemindMeBot.Tests.Unit.Common;
using Xunit;
using Xunit.Abstractions;

namespace RemindMeBot.Tests.Unit.Dialogs
{
    public class CancelDialogTest : BotTestBase
    {
        private readonly TestCancelDialog _sut;

        public CancelDialogTest(ITestOutputHelper output) : base(output)
        {
            _sut = new TestCancelDialog(Localizer);
        }

        [Theory]
        [InlineData("en-US")]
        [InlineData("uk-UA")]
        public async Task ShouldBeAbleToCancel(string culture)
        {
            // Arrange
            ConfigureLocalization(culture);
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
            var testClient = new DialogTestClient(Channels.Test, _sut, middlewares: Middlewares);

            // Act / Assert
            var reply = await testClient.SendActivityAsync<IMessageActivity>(new Activity { Type = ActionTypes.ShowImage, Value = "/cancel"});
            reply.Text.Should().Be("Hi there");
            testClient.DialogTurnResult.Status.Should().Be(DialogTurnStatus.Waiting);

            reply = await testClient.SendActivityAsync<IMessageActivity>(new Activity { Type = ActionTypes.ShowImage, Value = "/cancel" });
            reply.Should().BeNull();
        }
    }

    public class TestCancelDialog : CancelDialog
    {
        public TestCancelDialog(IStringLocalizer<BotMessages> localizer)
            : base(nameof(TestCancelDialog), localizer)
        {
            AddDialog(new TextPrompt(nameof(TextPrompt)));

            AddDialog(new WaterfallDialog("testWaterfall",
                new WaterfallStep[]
                {
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
