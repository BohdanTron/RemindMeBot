using Microsoft.Extensions.Caching.Distributed;
using Newtonsoft.Json;

namespace RemindMeBot.Helpers
{
    public static class DistributedCacheExtensions
    {
        public static async Task SetRecord<T>(
            this IDistributedCache cache, 
            string recordId, 
            T data,
            TimeSpan? absoluteExpiredTime = null, 
            TimeSpan? unusedExpiredTime = null)
        {
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = absoluteExpiredTime ?? TimeSpan.FromSeconds(60),
                SlidingExpiration = unusedExpiredTime
            };
            var jsonData = JsonConvert.SerializeObject(data);

            await cache.SetStringAsync(recordId, jsonData, options);
        }


        public static async Task<T?> GetRecord<T>(this IDistributedCache cache, string recordId)
        {
            var jsonData = await cache.GetStringAsync(recordId);
            
            return jsonData is null 
                ? default 
                : JsonConvert.DeserializeObject<T>(jsonData);
        }
    }
}
