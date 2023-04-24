using System.Globalization;
using AzureMapsToolkit;
using AzureMapsToolkit.Search;
using AzureMapsToolkit.Timezone;

namespace RemindMeBot.Services
{
    public record Location(string City, string Country, string TimeZoneId);

    public interface ILocationService
    {
        Task<Location?> GetLocation(string location);
    }

    public class AzureLocationService : ILocationService
    {
        private readonly AzureMapsServices _azureMapsClient;

        public AzureLocationService(AzureMapsServices azureMapsClient) =>
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

            return new Location(address.Address.FreeformAddress, address.Address.Country, timezone.Id);
        }
    }
}
