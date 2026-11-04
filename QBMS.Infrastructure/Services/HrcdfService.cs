using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using QBMS.Application.Interfaces;
using QBMS.Application.Models;

namespace QBMS.Infrastructure.Services
{
    /// <summary>
    /// Fetches employee details from the HRCDF API (JSON). The URL, optional API-key
    /// header and the JSON field that holds the name are configured in appsettings.json
    /// under "Hrcdf".
    /// </summary>
    public class HrcdfService : IHrcdfService
    {
        private static readonly string[] DefaultNameFields =
            { "name", "employeeName", "fullName", "empName", "personnelName", "userName","rankName" };

        private readonly HttpClient _httpClient;
        private readonly HrcdfSettings _settings;

        public HrcdfService(HttpClient httpClient, HrcdfSettings settings)
        {
            _httpClient = httpClient;
            _settings = settings;
        }

        public async Task<HrcdfLookupResult> GetEmployeeAsync(string personnelNo)
        {
            if (string.IsNullOrWhiteSpace(personnelNo))
            {
                return HrcdfLookupResult.Fail("Personnel No is required.");
            }

            if (string.IsNullOrWhiteSpace(_settings.UrlTemplate))
            {
                return HrcdfLookupResult.Fail(
                    "HRCDF API is not configured. Set Hrcdf:UrlTemplate in appsettings.json.");
            }

            var url = _settings.UrlTemplate.Replace(
                "{personnelNo}",
                Uri.EscapeDataString(personnelNo.Trim()));

            try
            {
                using (var request = new HttpRequestMessage(HttpMethod.Get, url))
                {
                    request.Headers.TryAddWithoutValidation("Accept", "application/json");

                    if (!string.IsNullOrWhiteSpace(_settings.ApiKeyHeader) &&
                        !string.IsNullOrWhiteSpace(_settings.ApiKey))
                    {
                        request.Headers.TryAddWithoutValidation(
                            _settings.ApiKeyHeader,
                            _settings.ApiKey);
                    }

                    using (var response = await _httpClient.SendAsync(request))
                    {
                        if (response.StatusCode == HttpStatusCode.NotFound)
                        {
                            return HrcdfLookupResult.Fail(
                                "Personnel No was not found in HRCDF.");
                        }

                        if (!response.IsSuccessStatusCode)
                        {
                            return HrcdfLookupResult.Fail(
                                "HRCDF returned an error (HTTP " + (int)response.StatusCode + ").");
                        }

                        var body = await response.Content.ReadAsStringAsync();

                        if (string.IsNullOrWhiteSpace(body))
                        {
                            return HrcdfLookupResult.Fail(
                                "Personnel No was not found in HRCDF.");
                        }

                        var name = ExtractName(JToken.Parse(body));

                        if (string.IsNullOrWhiteSpace(name))
                        {
                            return HrcdfLookupResult.Fail(
                                "Personnel No was not found in HRCDF, or the response did not contain a name.");
                        }

                        return HrcdfLookupResult.Ok(name);
                    }
                }
            }
            catch (TaskCanceledException)
            {
                return HrcdfLookupResult.Fail("HRCDF did not respond in time. Please try again.");
            }
            catch (HttpRequestException)
            {
                return HrcdfLookupResult.Fail("Unable to reach the HRCDF service. Please try again.");
            }
            catch (JsonException)
            {
                return HrcdfLookupResult.Fail("HRCDF returned an invalid response.");
            }
        }

        private string ExtractName(JToken root)
        {
            var fields = (_settings.NameFields != null && _settings.NameFields.Length > 0)
                ? _settings.NameFields
                : DefaultNameFields;

            foreach (var field in fields)
            {
                var value = FindString(root, field);

                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }

            return null;
        }

        // Depth-first search for a string property with the given name (case-insensitive).
        private static string FindString(JToken token, string field)
        {
            if (token is JObject obj)
            {
                foreach (var property in obj.Properties())
                {
                    if (string.Equals(property.Name, field, StringComparison.OrdinalIgnoreCase) &&
                        property.Value.Type == JTokenType.String)
                    {
                        return property.Value.ToString();
                    }
                }

                foreach (var property in obj.Properties())
                {
                    var found = FindString(property.Value, field);

                    if (found != null)
                    {
                        return found;
                    }
                }
            }
            else if (token is JArray array)
            {
                foreach (var item in array)
                {
                    var found = FindString(item, field);

                    if (found != null)
                    {
                        return found;
                    }
                }
            }

            return null;
        }
    }
}
