using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Delivery.Navigation.Models; // Assuming GeoData and Google response models are here
using Microsoft.Extensions.Configuration; // For IConfiguration
using Microsoft.Extensions.Logging;    // For ILogger

namespace Delivery.Navigation.Services
{
    class GMapsService // Made public if it's to be used by other assemblies/DI
    {
        private readonly ILogger<GMapsService> _logger;
        private readonly HttpClient _httpClient; // Get from IHttpClientFactory
        private readonly string _apiKey;

        // Constructor for Dependency Injection
        public GMapsService(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<GMapsService> logger)
        {
            _logger = logger;
            _httpClient = httpClientFactory.CreateClient("GoogleMapsClient"); // Use a named client

            // It's good practice to validate critical configuration
            _apiKey = configuration["GoogleMaps:ApiKey"];
            if (string.IsNullOrEmpty(_apiKey))
            {
                _logger.LogCritical("Google Maps API Key is not configured in GoogleMaps:ApiKey.");
                throw new InvalidOperationException("Google Maps API Key is not configured.");
            }
        }

        public async Task<GeoData> GetGeoDataAsync(string address)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                _logger.LogWarning("Address provided to GetGeoDataAsync was null or empty.");
                return null; // Or throw ArgumentNullException
            }

            string encodedAddress = Uri.EscapeDataString(address);
            _logger.LogDebug("Encoded address: {EncodedAddress}", encodedAddress);

            // Consider using a UriBuilder for more complex URLs
            string requestUri = $"https://maps.googleapis.com/maps/api/geocode/json?address={encodedAddress}&key={_apiKey}";

            _logger.LogDebug("Requesting Geocoding data from: {RequestUri}", requestUri);

            HttpResponseMessage response;
            try
            {
                response = await _httpClient.GetAsync(requestUri);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP request to Google Geocoding API failed for address: {Address}", address);
                throw; // Re-throw the original exception or a custom one
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Google Geocoding API request failed with status code {StatusCode} ({ReasonPhrase}) for address: {Address}",
                    response.StatusCode, response.ReasonPhrase, address);

                string errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogDebug("Error content from Google API: {ErrorContent}", errorContent); // Be careful with logging full content

                try
                {
                    // Attempt to parse Google's specific error message
                    var errorResponse = JsonSerializer.Deserialize<GoogleGeocodingResponse>(errorContent,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); // PropertyNameCaseInsensitive for robustness
                    if (errorResponse != null && !string.IsNullOrEmpty(errorResponse.ErrorMessage))
                    {
                        _logger.LogError("Google API Error: {ErrorMessage} (Status: {ApiStatus})",
                            errorResponse.ErrorMessage, errorResponse.Status);
                    }
                }
                catch (JsonException jsonEx)
                {
                    _logger.LogWarning(jsonEx, "Failed to deserialize Google API error response content.");
                }
                // This will throw an HttpRequestException, which is appropriate here.
                response.EnsureSuccessStatusCode();
            }

            string jsonResponse = await response.Content.ReadAsStringAsync();
            GoogleGeocodingResponse? geocodingResponse;

            try
            {
                geocodingResponse = JsonSerializer.Deserialize<GoogleGeocodingResponse>(jsonResponse);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to deserialize the successful response from Google Geocoding API for address: {Address}. Response: {JsonResponse}", address, jsonResponse.Substring(0, Math.Min(500, jsonResponse.Length))); // Log part of response
                throw new ApplicationException("Failed to parse geocoding data from Google Maps API.", ex);
            }


            if (geocodingResponse == null)
            {
                _logger.LogWarning("Deserialized Google Geocoding API response was null for address: {Address}. Raw response (truncated): {JsonResponse}", address, jsonResponse.Substring(0, Math.Min(500, jsonResponse.Length)));
                // This case should ideally be caught by JsonException above if parsing fails,
                // but if Deserialize can return null for valid JSON (e.g. "null" string), this is a safeguard.
                return null;
            }

            if (geocodingResponse.Status != "OK" || geocodingResponse.Results == null || geocodingResponse.Results.Count == 0)
            {
                _logger.LogWarning("Google Geocoding API did not return 'OK' or had no results for address: {Address}. Status: {ApiStatus}, Error: {ErrorMessage}",
                    address, geocodingResponse.Status, geocodingResponse.ErrorMessage ?? "N/A");
                return null; // No results found or API error reported in the JSON
            }

            var firstResult = geocodingResponse.Results[0];
            _logger.LogInformation("Successfully geocoded address '{Address}'. Formatted: '{FormattedAddress}', Lat: {Latitude}, Lng: {Longitude}",
                address, firstResult.FormattedAddress, firstResult.Geometry.Location.Lat, firstResult.Geometry.Location.Lng);

            return new GeoData
            {
                Latitude = (decimal)firstResult.Geometry.Location.Lat, // Ensure GeoData.Latitude is decimal
                Longitude = (decimal)firstResult.Geometry.Location.Lng, // Ensure GeoData.Longitude is decimal
                StreetAddress = firstResult.FormattedAddress,
                // PlaceId = firstResult.PlaceId // Consider adding PlaceId if useful
            };
        }

        public async Task<DirectionsResponse?> GetDirectionsAsync(
            string origin,
            string destination,
            IEnumerable<string>? waypoints = null,
            string travelMode = "driving",
            bool alternatives = false,
            bool optimizeWaypoints = false) // Added optimizeWaypoints
        {
            if (string.IsNullOrWhiteSpace(origin))
            {
                _logger.LogWarning("Origin provided to GetDirectionsAsync was null or empty.");
                // Consider throwing ArgumentNullException or returning null based on desired behavior
                throw new ArgumentNullException(nameof(origin));
            }
            if (string.IsNullOrWhiteSpace(destination))
            {
                _logger.LogWarning("Destination provided to GetDirectionsAsync was null or empty.");
                throw new ArgumentNullException(nameof(destination));
            }

            string encodedOrigin = Uri.EscapeDataString(origin);
            string encodedDestination = Uri.EscapeDataString(destination);

            var queryParams = new System.Text.StringBuilder();
            queryParams.Append($"origin={encodedOrigin}");
            queryParams.Append($"&destination={encodedDestination}");
            queryParams.Append($"&mode={travelMode.ToLower()}");
            queryParams.Append($"&alternatives={(alternatives ? "true" : "false")}");
            // Add other common parameters if needed:
            // queryParams.Append("&units=metric");
            // queryParams.Append("&language=en");

            if (waypoints != null && waypoints.Any())
            {
                string waypointsPrefix = optimizeWaypoints ? "optimize:true|" : "";
                string encodedWaypoints = waypointsPrefix +
                                          string.Join("|", waypoints.Select(wp => Uri.EscapeDataString(wp)));
                queryParams.Append($"&waypoints={encodedWaypoints}");
            }

            queryParams.Append($"&key={_apiKey}"); // API Key should always be last or part of the main query string

            string requestUri = $"https://maps.googleapis.com/maps/api/directions/json?{queryParams.ToString()}";

            _logger.LogDebug("Requesting Directions data from (first 200 chars): {RequestUriStart}", requestUri.Substring(0, Math.Min(requestUri.Length, 200)));

            HttpResponseMessage response;
            try
            {
                response = await _httpClient.GetAsync(requestUri);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP request to Google Directions API failed. Origin: {Origin}, Dest: {Destination}", origin, destination);
                throw;
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Google Directions API request failed with status code {StatusCode} ({ReasonPhrase}). Origin: {Origin}, Dest: {Destination}",
                    response.StatusCode, response.ReasonPhrase, origin, destination);

                string errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogDebug("Error content from Google Directions API: {ErrorContent}", errorContent);

                try
                {
                    var errorResponse = JsonSerializer.Deserialize<DirectionsResponse>(errorContent,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (errorResponse != null && !string.IsNullOrEmpty(errorResponse.ErrorMessage))
                    {
                        _logger.LogError("Google Directions API Error: {ErrorMessage} (Status: {ApiStatus})",
                            errorResponse.ErrorMessage, errorResponse.Status);
                    }
                }
                catch (JsonException jsonEx)
                {
                    _logger.LogWarning(jsonEx, "Failed to deserialize Google Directions API error response content.");
                }
                response.EnsureSuccessStatusCode();
            }

            string jsonResponse = await response.Content.ReadAsStringAsync();
            DirectionsResponse? directionsResponse;

            try
            {
                directionsResponse = JsonSerializer.Deserialize<DirectionsResponse>(jsonResponse);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to deserialize the successful response from Google Directions API. Origin: {Origin}, Dest: {Destination}. Response (truncated): {JsonResponse}",
                                 origin, destination, jsonResponse.Substring(0, Math.Min(500, jsonResponse.Length)));
                throw new ApplicationException("Failed to parse directions data from Google Maps API.", ex);
            }

            if (directionsResponse == null)
            {
                _logger.LogWarning("Deserialized Google Directions API response was null. Origin: {Origin}, Dest: {Destination}. Raw response (truncated): {JsonResponse}",
                                 origin, destination, jsonResponse.Substring(0, Math.Min(500, jsonResponse.Length)));
                return null;
            }

            if (directionsResponse.Status != "OK") // Note: Directions API can return OK with no routes
            {
                _logger.LogWarning("Google Directions API did not return 'OK'. Status: {ApiStatus}, Error: {ErrorMessage}. Origin: {Origin}, Dest: {Destination}",
                    directionsResponse.Status, directionsResponse.ErrorMessage ?? "N/A", origin, destination);
                // You might still want to return the response if the status is e.g. "ZERO_RESULTS"
                // if the caller wants to inspect it. Or return null for any non-OK status.
                // For simplicity here, we'll return the object so caller can check status.
            }
            else if (directionsResponse.Routes == null || !directionsResponse.Routes.Any())
            {
                _logger.LogInformation("Google Directions API returned 'OK' but no routes found. Origin: {Origin}, Dest: {Destination}", origin, destination);
                // This is a valid API response (Status: OK), but no routes.
            }
            else
            {
                _logger.LogInformation("Successfully retrieved directions. Origin: {Origin}, Dest: {Destination}, Routes found: {RouteCount}",
                                     origin, destination, directionsResponse.Routes.Count);
            }

            return directionsResponse;
        }

        // You would also add your GetDirectionsAsync method here, adapted similarly.
    }
}