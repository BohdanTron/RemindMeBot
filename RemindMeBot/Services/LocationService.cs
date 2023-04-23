using System.Globalization;
using AzureMapsToolkit;
using AzureMapsToolkit.Search;
using AzureMapsToolkit.Timezone;

namespace RemindMeBot.Services
{
    public record Location(SearchResultAddress Address, Timezone Timezone);

    public class LocationService
    {
        private readonly AzureMapsServices _azureMapsClient;

        public LocationService(AzureMapsServices azureMapsClient) =>
            _azureMapsClient = azureMapsClient;

        public async Task<Location?> GetLocation(string location)
        {
            var addressResponse = await _azureMapsClient.GetSearchAddress(new SearchAddressRequest
            {
                Query = location
            });

            if (addressResponse.Error is not null || !addressResponse.Result.Results.Any())
            {
                return null;
            }

            var address = addressResponse.Result.Results.First();
            var position = address.Position;

            var timeZoneResponse = await _azureMapsClient.GetTimezoneByCoordinates(new TimeZoneRequest
            {
                Query = $"{position.Lat.ToString(CultureInfo.InvariantCulture)},{position.Lon.ToString(CultureInfo.InvariantCulture)}"
            });

            if (timeZoneResponse.Error is not null || !timeZoneResponse.Result.TimeZones.Any())
            {
                return null;
            }

            var timezone = timeZoneResponse.Result.TimeZones.First();

            return new Location(address.Address, timezone);
        }
    }
}
