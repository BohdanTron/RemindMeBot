using System.Globalization;
using AzureMapsToolkit;
using AzureMapsToolkit.Search;
using AzureMapsToolkit.Timezone;

namespace RemindMeBot.Services
{
    public class LocationService
    {
        private readonly AzureMapsServices _azureMapsClient;

        public LocationService(AzureMapsServices azureMapsClient) =>
            _azureMapsClient = azureMapsClient;

        public async Task<TimezoneResult?> GetUserTimezone(string location)
        {
            var addressResponse = await _azureMapsClient.GetSearchAddress(new SearchAddressRequest
            {
                Query = location
            });

            if (addressResponse.Error is not null || !addressResponse.Result.Results.Any())
            {
                return null;
            }

            var addressResult = addressResponse.Result.Results.First().Position;

            var timeZoneResponse = await _azureMapsClient.GetTimezoneByCoordinates(new TimeZoneRequest
            {
                Query = $"{addressResult.Lat.ToString(CultureInfo.InvariantCulture)},{addressResult.Lon.ToString(CultureInfo.InvariantCulture)}"
            });

            if (timeZoneResponse.Error is not null || !timeZoneResponse.Result.TimeZones.Any())
            {
                return null;
            }

            return timeZoneResponse.Result;
        }
    }
}
