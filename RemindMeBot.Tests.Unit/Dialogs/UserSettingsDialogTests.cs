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
using RemindMeBot.Models;
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
        private readonly StateService _stateService;

        public UserSettingsDialogTests()
        {
            // Initialize state
            var memoryStorage = new MemoryStorage();
            var userState = new UserState(memoryStorage);
            var conversationState = new ConversationState(memoryStorage);

            _stateService = new StateService(userState, conversationState);

            var resourceManager = new ResourceManager(
                $"{typeof(BotMessages).Assembly.GetName().Name}.Resources.BotMessages", typeof(BotMessages).Assembly);

            // Mock localizer
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

            // Initialize sut
            _sut = new UserSettingsDialog(_stateService, _translationService, _locationService, _localizer);
        }

        [Theory]
        [InlineData("English", "en-US", "London", "United Kingdom", "Europe/London")]
        [InlineData("Українська", "uk-UA", "Київ", "Українська", "Europe/Kyiv")]
        public async Task ShouldSetUserSettings_WhenHappyPath(string language, string culture, string city, string country, string timeZone)
        {
            // Arrange
            _locationService.GetLocation(Arg.Any<string>())
                .Returns(new Location(city, country, timeZone));

            var userSettings = new UserSettings
            {
                Language = language,
                LanguageCode = culture,
                Location = $"{city}, {country}",
                TimeZoneId = timeZone
            };

            // Set the current culture for the test
            CultureInfo.CurrentCulture = new CultureInfo(culture);
            CultureInfo.CurrentUICulture = new CultureInfo(culture);

            var testClient = new DialogTestClient(Channels.Test, _sut);

            // Act / Assert

            // Step 1 
            var reply = await testClient.SendActivityAsync<IMessageActivity>("start");
            reply.Text.Should().Be("Welcome to the RemindMe chatbot! Please choose your language: (1) English or (2) Українська");
            testClient.DialogTurnResult.Status.Should().Be(DialogTurnStatus.Waiting);

            // Step 2 (Valid Language)
            reply = await testClient.SendActivityAsync<IMessageActivity>(language);
            reply.Text.Should().Be(_localizer[ResourcesKeys.AskForLocation].Value);
            testClient.DialogTurnResult.Status.Should().Be(DialogTurnStatus.Waiting);

            // Step 3 (Valid Location)
            reply = await testClient.SendActivityAsync<IMessageActivity>($"{city}, {country}");
            var expected = _localizer[ResourcesKeys.UserSettingsWereSet, language, userSettings.Location, timeZone, userSettings.LocalTime!];
            reply.Text.Should().Be(expected);
            testClient.DialogTurnResult.Status.Should().Be(DialogTurnStatus.Complete);

            // Check state
            var actualUserSettings = await _stateService.UserSettingsPropertyAccessor.GetAsync(testClient.DialogContext.Context);
            actualUserSettings.Should().Be(userSettings);
        }

        [Theory]
        [InlineData("English", "en-US", "London", "United Kingdom", "Europe/London")]
        [InlineData("Українська", "uk-UA", "Київ", "Українська", "Europe/Kyiv")]
        public async Task ShouldSetUserSettings_WhenInvalidInputFlow(string language, string culture, string city, string country, string timeZone)
        {
            // Arrange
            var location = $"{city}, {country}";
            var userSettings = new UserSettings
            {
                Language = language,
                LanguageCode = culture,
                Location = location,
                TimeZoneId = timeZone
            };

            // Set the current culture for the test
            CultureInfo.CurrentCulture = new CultureInfo(culture);
            CultureInfo.CurrentUICulture = new CultureInfo(culture);

            var testClient = new DialogTestClient(Channels.Test, _sut);

            // Act / Assert

            // Step 1
            var reply = await testClient.SendActivityAsync<IMessageActivity>("start");
            reply.Text.Should().Be("Welcome to the RemindMe chatbot! Please choose your language: (1) English or (2) Українська");
            testClient.DialogTurnResult.Status.Should().Be(DialogTurnStatus.Waiting);

            // Step 2 (Invalid language)
            reply = await testClient.SendActivityAsync<IMessageActivity>("Invalid language");
            reply.Text.Should().Be("Please choose an option from the list: (1) English or (2) Українська");
            testClient.DialogTurnResult.Status.Should().Be(DialogTurnStatus.Waiting);

            // Step 3 (Valid language)
            reply = await testClient.SendActivityAsync<IMessageActivity>(language);
            reply.Text.Should().Be(_localizer[ResourcesKeys.AskForLocation].Value);
            testClient.DialogTurnResult.Status.Should().Be(DialogTurnStatus.Waiting);

            // Step 4 (Invalid location)
            _locationService.GetLocation(Arg.Any<string>()).Returns((Location?) null);
            reply = await testClient.SendActivityAsync<IMessageActivity>("Invalid location");
            reply.Text.Should().Be(_localizer[ResourcesKeys.AskToRetryLocation].Value);
            testClient.DialogTurnResult.Status.Should().Be(DialogTurnStatus.Waiting);

            // Step 5 (Valid location)
            _locationService.GetLocation(Arg.Any<string>()).Returns(new Location(city, country, timeZone));
            reply = await testClient.SendActivityAsync<IMessageActivity>(location);
            var expected = _localizer[ResourcesKeys.UserSettingsWereSet, language, location, timeZone, userSettings.LocalTime!];
            reply.Text.Should().Be(expected);
            testClient.DialogTurnResult.Status.Should().Be(DialogTurnStatus.Complete);

            // Check state
            var actualUserSettings = await _stateService.UserSettingsPropertyAccessor.GetAsync(testClient.DialogContext.Context);
            actualUserSettings.Should().Be(userSettings);
        }
    }
}
