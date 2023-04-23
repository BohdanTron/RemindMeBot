﻿using System.Text;
using Newtonsoft.Json;

namespace RemindMeBot.Services
{
    public class TranslationService
    {
        private readonly HttpClient _httpClient;

        public TranslationService(HttpClient httpClient) =>
            _httpClient = httpClient;

        public async Task<string> Translate(string text, string from, string to)
        {
            var route = $"/translate?api-version=3.0&from={from}&to={to}";

            var body = new object[] { new { Text = text } };
            var requestBody = JsonConvert.SerializeObject(body);

            // BaseAddress must be set in Program.cs
            var uri = new Uri(_httpClient.BaseAddress!, route);
            var content = new StringContent(requestBody, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(uri, content);
            var responseBody = await response.Content.ReadAsStringAsync();

            var result = JsonConvert.DeserializeObject<List<Dictionary<string, List<Dictionary<string, string>>>>>(responseBody);
            var translation = result![0]["translations"][0]["text"];

            return translation;
        }
    }
}