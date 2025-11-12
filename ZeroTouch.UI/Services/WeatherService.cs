using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using DotNetEnv;

namespace ZeroTouch.UI.Services
{
    public class WeatherService
    {
        private readonly HttpClient _httpClient = new();

        public async Task<(string condition, string temperature, string pop, string comfort)> GetWeatherAsync(string location)
        {
            string url = string.Empty;

            try
            {
                DotNetEnv.Env.Load();
                var apiKey = Environment.GetEnvironmentVariable("CWB_API_KEY");

                if (string.IsNullOrWhiteSpace(apiKey))
                    throw new InvalidOperationException("Missing CWB_API_KEY in .env file.");

                url = $"https://opendata.cwa.gov.tw/api/v1/rest/datastore/F-C0032-001" + 
                      $"?Authorization={apiKey}&format=JSON&locationName={location}";

                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                string json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);

                var locationData = doc.RootElement
                    .GetProperty("records")
                    .GetProperty("location")[0];

                string wx = GetParameter(locationData, "Wx");
                string minT = GetParameter(locationData, "MinT");
                string maxT = GetParameter(locationData, "MaxT");
                string pop = GetParameter(locationData, "PoP");
                string ci = GetParameter(locationData, "CI");

                string tempRange = $"{minT}–{maxT}°C";

                return (wx, tempRange, $"{pop}%", ci);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WeatherService] Error: {ex.Message}");
                Console.WriteLine($"[WeatherService] Fetching weather for {location}...");
                Console.WriteLine($"[WeatherService] URL = {url}");
                Console.WriteLine($"[WeatherService] Exception: {ex}");

                return ("Unavailable", "--°C", "N/A", "N/A");
            }
        }

        private static string GetParameter(JsonElement locationData, string elementName)
        {
            foreach (var el in locationData.GetProperty("weatherElement").EnumerateArray())
            {
                if (el.GetProperty("elementName").GetString() == elementName)
                {
                    return el.GetProperty("time")[0]
                             .GetProperty("parameter")
                             .GetProperty("parameterName")
                             .GetString() ?? "N/A";
                }
            }
            return "N/A";
        }
    }
}
