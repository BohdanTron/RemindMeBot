using System.Threading.Tasks;
using AzureMapsToolkit.Search;
using AzureMapsToolkit.Timezone;
using FluentAssertions;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Testing;
using Microsoft.Bot.Connector;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Localization;
using NSubstitute;
using RemindMeBot.Dialogs;
using RemindMeBot.Resources;
using RemindMeBot.Services;
using Xunit;

namespace RemindMeBot.Tests.Unit.Dialogs
{
    public class UserSettingsDialogTests
    {
        private readonly UserSettingsDialog _sut;

        private readonly ILocationService _locationService = Substitute.For<ILocationService>();
        private readonly ITranslationService _translationService = Substitute.For<ITranslationService>();
        private readonly IStringLocalizer<BotMessages> _localizer = Substitute.For<IStringLocalizer<BotMessages>>();

        public UserSettingsDialogTests()
        {
            var memoryStorage = new MemoryStorage();
            var userState = new UserState(memoryStorage);
            var conversationState = new ConversationState(memoryStorage);

            var stateService = new StateService(userState, conversationState);

            _sut = new UserSettingsDialog(stateService, _translationService, _locationService, _localizer);
        }

        [Fact]
        public async Task ShouldSetUserSettings_WhenValid()
        {
            // Arrange
            var location = new Location(new SearchResultAddress
            {
                Country = "United Kingdom",
                FreeformAddress = "London",
            }, new Timezone
            {
                Id = "Europe/London"
            });
            _locationService.GetLocation(Arg.Any<string>()).Returns(location);

            _localizer[Arg.Is(ResourcesKeys.AskForLocation)].Returns(_ =>
                new LocalizedString(
                    ResourcesKeys.AskForLocation, "Please enter your city and country (e.g London, United Kingdom)"));

            _localizer[Arg.Is(ResourcesKeys.AskToRetryLocation)].Returns(_ =>
                new LocalizedString(ResourcesKeys.AskToRetryLocation, "I couldn't find your location. For better accuracy, please provide both your city and country names"));

            _localizer[Arg.Is(ResourcesKeys.UserSettingsWereSet), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()]
                .Returns(_ => new LocalizedString(ResourcesKeys.UserSettingsWereSet, "Your current settings:"));

            var testClient = new DialogTestClient(Channels.Test, _sut);

            // Act / Assert

            // Step 1
            var reply = await testClient.SendActivityAsync<IMessageActivity>("start");
            reply.Text.Should().Be("Welcome to the RemindMe chatbot! Please choose your language: (1) English or (2) Українська");
            testClient.DialogTurnResult.Status.Should().Be(DialogTurnStatus.Waiting);

            // Step 2
            reply = await testClient.SendActivityAsync<IMessageActivity>("English");
            reply.Text.Should().Be("Please enter your city and country (e.g London, United Kingdom)");
            testClient.DialogTurnResult.Status.Should().Be(DialogTurnStatus.Waiting);

            // Step 3
            reply = await testClient.SendActivityAsync<IMessageActivity>("London, United Kingdom");
            reply.Text.Should().Contain("Your current settings:");
            testClient.DialogTurnResult.Status.Should().Be(DialogTurnStatus.Complete);
        }
    }
}
