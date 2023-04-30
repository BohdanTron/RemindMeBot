using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Bot.Builder;
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
    public class ChangeUserSettingsDialogTests : BotTestBase
    {
        private readonly ChangeUserSettingsDialog _sut;

        private readonly IStateService _stateService = Substitute.For<IStateService>();
        private readonly ILocationService _locationService = Substitute.For<ILocationService>();
        private readonly ITranslationService _translationService = Substitute.For<ITranslationService>();

        public ChangeUserSettingsDialogTests(ITestOutputHelper output) : base(output)
        {
            _sut = new ChangeUserSettingsDialog(_stateService, _locationService, _translationService, Localizer);
        }

        [Theory]
        [InlineData("en-US")]
        [InlineData("uk-UA")]
        public async Task ShouldEndDialog_WhenNoUserSettingsExist(string culture)
        {
            // Arrange
            ConfigureCulture(culture);

            _stateService.UserSettingsPropertyAccessor
                .GetAsync(Arg.Any<ITurnContext>(), Arg.Any<Func<UserSettings>>(), Arg.Any<CancellationToken>())
                .Returns(new UserSettings());

            var testClient = new DialogTestClient(Channels.Test, _sut, middlewares: Middlewares);

            // Act
            var reply = await testClient.SendActivityAsync<IMessageActivity>("/my-settings");

            // Assert
            reply.Text.Should().Be(Localizer[ResourceKeys.NoUserSettings]);
        }

        [Fact]
        public async Task ShouldChangeUserLanguage_WhenUserSettingsExist()
        {
            // Arrange
            const string currentUserCulture = "en-US";
            const string currentUserLanguage = "English";

            const string newUserCulture = "uk-UA";
            const string newUserLanguage = "Українська";

            ConfigureCulture(currentUserCulture);

            var userSettings = new UserSettings
            {
                Language = currentUserLanguage,
                Location = "London",
                TimeZone = "Europe/London"
            };

            var languageChoice = Localizer[ResourceKeys.Language];
            var locationChoice = Localizer[ResourceKeys.Location];

            _stateService.UserSettingsPropertyAccessor
                .GetAsync(Arg.Any<ITurnContext>(), Arg.Any<Func<UserSettings>>(), Arg.Any<CancellationToken>())
                .Returns(userSettings);

            var testClient = new DialogTestClient(Channels.Test, _sut, middlewares: Middlewares);

            // Act / Assert

            // Step 1 - Display user's current settings
            var reply = await testClient.SendActivityAsync<IMessageActivity>("/my-settings");
            var currentUserSettingsMsg = Localizer[ResourceKeys.UserCurrentSettings, currentUserLanguage, userSettings.Location, userSettings.TimeZone, userSettings.LocalTime!];
            reply.Text.Should().Be(currentUserSettingsMsg);

            // Ask the setting to change (Language or Location)
            reply = testClient.GetNextReply<IMessageActivity>();
            reply.Text.Should().Be($"{Localizer[ResourceKeys.AskSettingToChange]} (1) {languageChoice} or (2) {locationChoice}");

            // Step 2 - Ask the language to change
            reply = await testClient.SendActivityAsync<IMessageActivity>(languageChoice);
            reply.Text.Should().Be($"{Localizer[ResourceKeys.AskForLanguage]} (1) English or (2) Українська");

            // Step 3 - Change the language
            reply = await testClient.SendActivityAsync<IMessageActivity>(newUserLanguage);
            SetCurrentCulture(newUserCulture);
            reply.Text.Should().Be(Localizer[ResourceKeys.UserSettingsHaveBeenChanged]);

            // Display new user's settings
            reply = testClient.GetNextReply<IMessageActivity>();
            var newUserSettingsMsg = Localizer[ResourceKeys.UserCurrentSettings, newUserLanguage, userSettings.Location, userSettings.TimeZone, userSettings.LocalTime!];
            reply.Text.Should().Be(newUserSettingsMsg);

            // Check dialog result
            testClient.DialogTurnResult.Result.Should().Be(new UserSettings
            {
                Language = newUserLanguage,
                Culture = newUserCulture,
                Location = "London",
                TimeZone = "Europe/London"
            });
        }

        [Theory]
        [InlineData("en-US", "English")]
        [InlineData("uk-UA", "Українська")]
        public async Task ShouldChangeUserLocation_WithoutChangingCulture_WhenUserSettingsExist(string culture, string language)
        {
            // Arrange
            ConfigureCulture(culture);
            var userSettings = new UserSettings
            {
                Language = language,
                Culture = culture,
                Location = "London",
                TimeZone = "Europe/London"
            };

            var languageChoice = Localizer[ResourceKeys.Language];
            var locationChoice = Localizer[ResourceKeys.Location];

            _stateService.UserSettingsPropertyAccessor
                .GetAsync(Arg.Any<ITurnContext>(), Arg.Any<Func<UserSettings>>(), Arg.Any<CancellationToken>())
                .Returns(userSettings);

            var newCity = "New York";
            var newCountry = "United States";
            var newTimeZone = "America/New_York";
            var newLocation = $"{newCity}, United States";
            _locationService.GetLocation(Arg.Any<string>()).Returns(new Location(newCity, newCountry, newTimeZone));

            var testClient = new DialogTestClient(Channels.Test, _sut, middlewares: Middlewares);

            // Act / Assert

            // Step 1 - Display user's current settings
            var reply = await testClient.SendActivityAsync<IMessageActivity>("/my-settings");
            var currentUserSettingsMsg = Localizer[ResourceKeys.UserCurrentSettings, language, userSettings.Location, userSettings.TimeZone, userSettings.LocalTime!];
            reply.Text.Should().Be(currentUserSettingsMsg);

            // Ask the setting to change (Language or Location)
            reply = testClient.GetNextReply<IMessageActivity>();
            reply.Text.Should().Be($"{Localizer[ResourceKeys.AskSettingToChange]} (1) {languageChoice} or (2) {locationChoice}");

            // Step 2 - Ask the location to change
            reply = await testClient.SendActivityAsync<IMessageActivity>(locationChoice);
            reply.Text.Should().Be(Localizer[ResourceKeys.AskForLocation]);

            // Step 3 - Change the location
            reply = await testClient.SendActivityAsync<IMessageActivity>(newCity);
            reply.Text.Should().Be(Localizer[ResourceKeys.UserSettingsHaveBeenChanged]);

            // Display new user's settings
            reply = testClient.GetNextReply<IMessageActivity>();
            var newUserSettingsMsg = Localizer[ResourceKeys.UserCurrentSettings, language, newLocation, newTimeZone, userSettings.LocalTime!];
            reply.Text.Should().Be(newUserSettingsMsg);

            // Check dialog result
            testClient.DialogTurnResult.Result.Should().Be(new UserSettings
            {
                Language = language,
                Culture = culture,
                Location = newLocation,
                TimeZone = newTimeZone
            });
        }

        [Theory]
        [InlineData("en-US", "English")]
        [InlineData("uk-UA", "Українська")]
        public async Task ShouldHandleInvalidLocationAndAskAgain_WhenUserSettingsExist(string culture, string language)
        {
            // Arrange
            ConfigureCulture(culture);

            var userSettings = new UserSettings
            {
                Culture = culture,
                Language = language,
                Location = "London",
                TimeZone = "Europe/London"
            };

            var languageChoice = Localizer[ResourceKeys.Language];
            var locationChoice = Localizer[ResourceKeys.Location];

            _stateService.UserSettingsPropertyAccessor
                .GetAsync(Arg.Any<ITurnContext>(), Arg.Any<Func<UserSettings>>(), Arg.Any<CancellationToken>())
                .Returns(userSettings);

            var testClient = new DialogTestClient(Channels.Test, _sut, middlewares: Middlewares);

            // Act / Assert

            // Step 1 - Display user's current settings
            var reply = await testClient.SendActivityAsync<IMessageActivity>("/my-settings");
            var currentUserSettingsMsg = Localizer[ResourceKeys.UserCurrentSettings, language, userSettings.Location, userSettings.TimeZone, userSettings.LocalTime!];
            reply.Text.Should().Be(currentUserSettingsMsg);

            // Ask the setting to change (Language or Location)
            reply = testClient.GetNextReply<IMessageActivity>();
            reply.Text.Should().Be($"{Localizer[ResourceKeys.AskSettingToChange]} (1) {languageChoice} or (2) {locationChoice}");

            // Step 2 - Ask the location to change
            reply = await testClient.SendActivityAsync<IMessageActivity>(locationChoice);
            reply.Text.Should().Be(Localizer[ResourceKeys.AskForLocation]);

            // Step 3 - Enter invalid location
            var invalidLocation = "InvalidLocation";
            _locationService.GetLocation(Arg.Any<string>()).Returns((Location?) null);

            reply = await testClient.SendActivityAsync<IMessageActivity>(invalidLocation);
            reply.Text.Should().Be(Localizer[ResourceKeys.AskToRetryLocation]);

            // Step 4 - Enter valid location
            var newCity = "New York";
            var newCountry = "United States";
            var newTimeZone = "America/New_York";
            var newLocation = $"{newCity}, United States";
            _locationService.GetLocation(Arg.Any<string>()).Returns(new Location(newCity, newCountry, newTimeZone));

            reply = await testClient.SendActivityAsync<IMessageActivity>(newLocation);
            reply.Text.Should().Be(Localizer[ResourceKeys.UserSettingsHaveBeenChanged]);

            // Display new user's settings
            reply = testClient.GetNextReply<IMessageActivity>();
            var newUserSettingsMsg = Localizer[ResourceKeys.UserCurrentSettings, language, newLocation, newTimeZone, userSettings.LocalTime!];
            reply.Text.Should().Be(newUserSettingsMsg);

            // Check dialog result
            testClient.DialogTurnResult.Result.Should().Be(new UserSettings
            {
                Language = language,
                Culture = culture,
                Location = newLocation,
                TimeZone = newTimeZone
            });
        }
    }
}
