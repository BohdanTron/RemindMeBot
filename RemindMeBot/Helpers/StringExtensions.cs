using System.Globalization;
using System.Reflection;
using System.Resources;

namespace RemindMeBot.Helpers
{
    public static class StringExtensions
    {
        private static readonly ResourceManager ResourceManager =
            new($"{Assembly.GetExecutingAssembly().GetName().Name}.Resources.Messages", Assembly.GetExecutingAssembly());

        public static string ToLocalized(this string key, string languageCode)
        {
            var culture = new CultureInfo(languageCode);
            
            return ResourceManager.GetString(key, culture) ??
                   throw new ArgumentException($"{nameof(key)} or {nameof(languageCode)}");
        }
    }
}
