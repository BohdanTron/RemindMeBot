using System.Globalization;

namespace RemindMeBot.Helpers
{
    public static class CultureHelper
    {
        public static void SetCurrentCulture(string culture)
        {
            CultureInfo.CurrentCulture = new CultureInfo(culture);
            CultureInfo.CurrentUICulture = new CultureInfo(culture);
        }

        public static string GetCulture(string language) =>
            language switch
            {
                "English" => "en-US",
                "Українська" => "uk-UA",
                _ => "en-US"
            };
    }
}
