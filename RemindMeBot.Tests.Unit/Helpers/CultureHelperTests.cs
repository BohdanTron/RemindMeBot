using System.Globalization;
using FluentAssertions;
using RemindMeBot.Helpers;
using Xunit;

namespace RemindMeBot.Tests.Unit.Helpers
{
    public class CultureHelperTests
    {
        [Fact]
        public void SetCurrentCulture_ShouldSetGivenCulture()
        {
            // Arrange
            const string culture = "uk-UA";

            // Act
            CultureHelper.SetCurrentCulture(culture);

            // Assert
            CultureInfo.CurrentCulture.Name.Should().Be(culture);
        }

        [Theory]
        [InlineData("English", "en-US")]
        [InlineData("english", "en-US")]
        [InlineData("Українська", "uk-UA")]
        [InlineData("українська", "uk-UA")]
        public void GetCulture_ShouldReturnValidCulture_WhenKnownLanguage(string language, string expectedCulture)
        {
            // Act
            var result = CultureHelper.GetCulture(language);

            // Assert
            result.Should().Be(expectedCulture);
        }

        [Fact]
        public void GetCulture_ShouldReturnDefaultCulture_WhenUnknownLanguage()
        {
            // Arrange
            const string language = "Spanish";
            const string defaultCulture = "en-US";

            // Act
            var result = CultureHelper.GetCulture(language);

            // Assert
            result.Should().Be(defaultCulture);
        }
    }
}
