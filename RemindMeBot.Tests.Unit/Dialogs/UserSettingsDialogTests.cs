using System.Globalization;
using System.Resources;
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

            var resourceManager = new ResourceManager(
                $"{typeof(BotMessages).Assembly.GetName().Name}.Resources.BotMessages", typeof(BotMessages).Assembly);

            _localizer[Arg.Any<string>()].Returns(info =>
            {
                var key = (string) info[0];
                var value = resourceManager.GetString(key, CultureInfo.CurrentCulture);

                return value is null ? null : new LocalizedString(key, value);
            });

            _localizer[Arg.Any<string>(), Arg.Any<object[]>()].Returns(info =>
            {
                var key = (string) info[0];
                var value = resourceManager.GetString(key, CultureInfo.CurrentCulture);

                return value is null ? null : new LocalizedString(key, value);
            });

            _sut = new UserSettingsDialog(stateService, _translationService, _locationService, _localizer);
        }

        [Fact]
        public async Task ShouldSetUserSettings_WhenValid_English()
        {
            // Arrange
            _locationService.GetLocation(Arg.Any<string>())
                .Returns(new Location("London", "United Kingdom", "Europe/London"));

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

        [Fact]
        public async Task ShouldSetUserSettings_WhenValid_Ukrainian()
        {
            // Arrange
            _locationService.GetLocation(Arg.Any<string>())
                .Returns(new Location("Київ", "Україна", "Europe/Kyiv"));

            _translationService.Translate(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
                .Returns("Kyiv, Ukraine");

            var testClient = new DialogTestClient(Channels.Test, _sut);

            // Act / Assert

            // Step 1
            var reply = await testClient.SendActivityAsync<IMessageActivity>("start");
            reply.Text.Should().Be("Welcome to the RemindMe chatbot! Please choose your language: (1) English or (2) Українська");
            testClient.DialogTurnResult.Status.Should().Be(DialogTurnStatus.Waiting);

            // Step 2
            reply = await testClient.SendActivityAsync<IMessageActivity>("Українська");
            reply.Text.Should().Be("Будь ласка вкажіть ваше місцезнаходження (наприклад Київ, Україна)");
            testClient.DialogTurnResult.Status.Should().Be(DialogTurnStatus.Waiting);

            // Step 3
            reply = await testClient.SendActivityAsync<IMessageActivity>("Київ, Україна");
            reply.Text.Should().Contain("Ваші поточні налаштування:");
            testClient.DialogTurnResult.Status.Should().Be(DialogTurnStatus.Complete);
        }
    }
}
