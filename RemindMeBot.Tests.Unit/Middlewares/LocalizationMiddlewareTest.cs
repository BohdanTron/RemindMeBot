using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Adapters;
using NSubstitute;
using RemindMeBot.Middlewares;
using RemindMeBot.Models;
using RemindMeBot.Services;
using Xunit;

namespace RemindMeBot.Tests.Unit.Middlewares
{
    public class LocalizationMiddlewareTest
    {
        private readonly IStateService _stateService = Substitute.For<IStateService>();
        private readonly IClock _clock = Substitute.For<IClock>();

        private readonly TestAdapter _testAdapter;

        public LocalizationMiddlewareTest()
        {
            _testAdapter = new TestAdapter()
                .Use(new LocalizationMiddleware(_stateService, _clock));
        }

        [Fact]
        public async Task ShouldSetUserCulture_WhenUserSettingsExist()
        {
            // Arrange
            var userSettings = new UserSettings { Culture = "uk-UA" };

            _stateService.UserSettingsPropertyAccessor
                .GetAsync(Arg.Any<ITurnContext>(), Arg.Any<Func<UserSettings>>(), Arg.Any<CancellationToken>())
                .Returns(userSettings);

            // Act / Assert
            await new TestFlow(_testAdapter, (_, _) =>
                {
                    CultureInfo.CurrentCulture.Name.Should().Be(userSettings.Culture);
                    CultureInfo.CurrentUICulture.Name.Should().Be(userSettings.Culture);

                    return Task.CompletedTask;
                })
                .Send("hi")
                .StartTestAsync();
        }

        [Fact]
        public async Task ShouldSetDefaultCulture_WhenNoUserSettingsExist()
        {
            // Arrange
            const string defaultCulture = "en-US";

            _stateService.UserSettingsPropertyAccessor
                .GetAsync(Arg.Any<ITurnContext>(), Arg.Any<Func<UserSettings>>(), Arg.Any<CancellationToken>())
                .Returns(new UserSettings());

            // Act / Assert
            await new TestFlow(_testAdapter, (_, _) =>
                {
                    CultureInfo.CurrentCulture.Name.Should().Be(defaultCulture);
                    CultureInfo.CurrentUICulture.Name.Should().Be(defaultCulture);

                    return Task.CompletedTask;
                })
                .Send("hi")
                .StartTestAsync();
        }
    }
}
