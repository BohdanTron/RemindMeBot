using System.Globalization;
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
using RemindMeBot.Services;
using Xunit;
using Xunit.Abstractions;

namespace RemindMeBot.Tests.Unit.Dialogs
{
    public class UserSettingsDialogTests : BotTestBase
    {
        private readonly UserSettingsDialog _sut;

        private readonly ILocationService _locationService = Substitute.For<ILocationService>();
        private readonly ITranslationService _translationService = Substitute.For<ITranslationService>();

        public UserSettingsDialogTests(ITestOutputHelper output) : base(output)
        {
            _sut = new UserSettingsDialog(StateService, _translationService, _locationService, Localizer);
        }

        [Theory]
        [InlineData("English", "en-US", "London", "United Kingdom", "Europe/London")]
        [InlineData("Українська", "uk-UA", "Київ", "Україна", "Europe/Kyiv")]
        public async Task ShouldSetUserSettings_WhenHappyPath(string language, string culture, string city, string country, string timeZone)
        {
            // Arrange
            _locationService.GetLocation(Arg.Any<string>())
                .Returns(new Location(city, country, timeZone));

            var location = $"{city}, {country}";
            var userSettings = new UserSettings
            {
                Language = language,
                LanguageCode = culture,
                Location = $"{city}, {country}",
                TimeZoneId = timeZone
            };

            // Set current culture for the test
            CultureInfo.CurrentCulture = new CultureInfo(culture);
            CultureInfo.CurrentUICulture = new CultureInfo(culture);

            var testClient = new DialogTestClient(Channels.Test, _sut, middlewares: Middlewares);

            var inputsAndReplies = new[,]
            {
                { "start", "Welcome to the RemindMe chatbot! Please choose your language: (1) English or (2) Українська"},
                { language, Localizer[ResourceKeys.AskForLocation].Value},
                { location, Localizer[ResourceKeys.UserSettingsWereSet, language, location, timeZone, userSettings.LocalTime!]}
            };

            // Act / Assert
            for (var i = 0; i < inputsAndReplies.GetLength(0); i++)
            {
                var reply = await testClient.SendActivityAsync<IMessageActivity>(inputsAndReplies[i, 0]);
                reply.Text.Should().Be(inputsAndReplies[i, 1]);
            }

            // Check state
            var actualUserSettings = await StateService.UserSettingsPropertyAccessor.GetAsync(testClient.DialogContext.Context);
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

            // Set current culture for the test
            CultureInfo.CurrentCulture = new CultureInfo(culture);
            CultureInfo.CurrentUICulture = new CultureInfo(culture);

            var testClient = new DialogTestClient(Channels.Test, _sut, middlewares: Middlewares);

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
            reply.Text.Should().Be(Localizer[ResourceKeys.AskForLocation].Value);
            testClient.DialogTurnResult.Status.Should().Be(DialogTurnStatus.Waiting);

            // Step 4 (Invalid location)
            _locationService.GetLocation(Arg.Any<string>()).Returns((Location?) null);
            reply = await testClient.SendActivityAsync<IMessageActivity>("Invalid location");
            reply.Text.Should().Be(Localizer[ResourceKeys.AskToRetryLocation].Value);
            testClient.DialogTurnResult.Status.Should().Be(DialogTurnStatus.Waiting);

            // Step 5 (Valid location)
            _locationService.GetLocation(Arg.Any<string>()).Returns(new Location(city, country, timeZone));
            reply = await testClient.SendActivityAsync<IMessageActivity>(location);
            var expected = Localizer[ResourceKeys.UserSettingsWereSet, language, location, timeZone, userSettings.LocalTime!];
            reply.Text.Should().Be(expected);
            testClient.DialogTurnResult.Status.Should().Be(DialogTurnStatus.Complete);

            // Check state
            var actualUserSettings = await StateService.UserSettingsPropertyAccessor.GetAsync(testClient.DialogContext.Context);
            actualUserSettings.Should().Be(userSettings);
        }
    }
}
