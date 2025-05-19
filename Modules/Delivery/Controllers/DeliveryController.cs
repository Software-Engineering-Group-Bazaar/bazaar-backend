using System;
using System.Collections.Generic;
using System.Linq;
using System.Net; // Required for WebUtility
using System.Net.Http; // Required for HttpClient, IHttpClientFactory
using System.Runtime.InteropServices;
using System.Security.Claims;
using System.Security.Cryptography; // Required for SHA256
using System.Text; // Required for Encoding
using System.Text.Json;
using System.Threading.Tasks;
using Admin.Controllers;
using Delivery.Dtos;      // Za DTO klase
using Delivery.Interfaces; // Za IRoutesService
using Delivery.Models;    // Za DeliveryRouteData (ako servisi vraćaju modele)
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding.Binders;
using Microsoft.Extensions.Configuration; // Required for IConfiguration
using Microsoft.Extensions.Logging;
using MimeKit;
using Order.Interface; // For IOrderService
using Order.Services;
using Store.Interface;
// Assuming IAddressService and Address model are in these namespaces
using Users.Interfaces; // For IAddressService
using Users.Models;    // For Address model (if needed directly, though service is preferred)


namespace Delivery.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class DeliveryController : ControllerBase
    {
        private readonly IRoutesService _routesService;
        private readonly ILogger<DeliveryController> _logger;
        private readonly IOrderService _orderService;

        private readonly IAddressService _addressService; // Added
        private readonly IHttpClientFactory _httpClientFactory; // Added
        private readonly IConfiguration _configuration; // Added
        private readonly IStoreService _storeService;

        public DeliveryController(IRoutesService routesService, ILogger<DeliveryController> logger, IOrderService orderService, IAddressService addressService, IHttpClientFactory httpClientFactory, IConfiguration configuration, IStoreService storeService)
        {
            _routesService = routesService ?? throw new ArgumentNullException(nameof(routesService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _orderService = orderService ?? throw new ArgumentNullException(nameof(orderService));
            _addressService = addressService ?? throw new ArgumentNullException(nameof(addressService)); // Added
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory)); // Added
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration)); // Added
            _storeService = storeService ?? throw new ArgumentNullException(nameof(storeService));

        }

        /// <summary>
        /// Gets all delivery routes.
        /// </summary>
        [HttpGet("routes")]
        [ProducesResponseType(typeof(IEnumerable<DeliveryRouteResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<DeliveryRouteResponseDto>>> GetAllRoutes()
        {
            try
            {
                var routes = await _routesService.GetAllDeliveryRoutes();
                var routeDtos = routes.Select(MapToResponseDto).ToList(); // Ručno mapiranje
                return Ok(routeDtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all delivery routes.");
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while retrieving routes.");
            }
        }

        /// <summary>
        /// Gets a specific delivery route by ID.
        /// </summary>
        /// <param name="id">The ID of the route.</param>
        [HttpGet("routes/{id:int}")]
        [ProducesResponseType(typeof(DeliveryRouteResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<DeliveryRouteResponseDto>> GetRouteById(int id)
        {
            try
            {
                var route = await _routesService.GetRouteByIdAsync(id);
                if (route == null)
                {
                    _logger.LogWarning("Route with ID {RouteId} not found.", id);
                    return NotFound($"Route with ID {id} not found.");
                }
                var routeDto = MapToResponseDto(route); // Ručno mapiranje
                return Ok(routeDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving route with ID {RouteId}.", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while retrieving the route.");
            }
        }

        /// <summary>
        /// Gets delivery routes that contain the exact set of specified order IDs.
        /// </summary>
        /// <param name="orderIds">A list of order IDs.</param>
        [HttpPost("routes/by-orders")]
        [ProducesResponseType(typeof(IEnumerable<DeliveryRouteResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<DeliveryRouteResponseDto>>> GetRoutesByOrderIds([FromBody] List<int> orderIds)
        {
            if (orderIds == null || !orderIds.Any())
            {
                return BadRequest("At least one OrderId must be provided.");
            }
            try
            {
                var route = await _routesService.GetRoutesFromOrders(orderIds);
                var routeDtos = MapToResponseDto(route);
                return Ok(routeDtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving routes by order IDs.");
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while retrieving routes by orders.");
            }
        }

        /// <summary>
        /// Creates a new delivery route using Google Maps API for directions.
        /// </summary>
        /// <param name="requestDto">The request data containing order IDs for the route.</param>
        [HttpPost("routes")]
        [ProducesResponseType(typeof(DeliveryRouteResponseDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [Authorize(Roles = "Admin, Seller")]
        public async Task<ActionResult<DeliveryRouteResponseDto>> CreateRoute([FromBody] CreateDeliveryRouteRequestDto requestDto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("User ID not found in token for CreateRoute.");
                return Unauthorized("User ID not found in token.");
            }
            try
            {
                if (requestDto.OrderIds == null || !requestDto.OrderIds.Any())
                {
                    return BadRequest("At least one OrderId must be provided.");
                }

                var destinationAddressesForGoogleApi = new List<string>();
                var originAddressForGoogleApi = "";
                var uniqueOrderIds = requestDto.OrderIds.Distinct().ToList(); // Ensure unique order IDs

                if (uniqueOrderIds.Count < 1)
                { // Should be caught by MinLength, but defensive
                    return BadRequest("At least one unique OrderId must be provided.");
                }


                foreach (var orderId in uniqueOrderIds)
                {
                    var order = await _orderService.GetOrderByIdAsync(orderId);
                    if (order == null)
                    {
                        _logger.LogWarning("Order with ID {OrderId} not found during route creation.", orderId);
                        return BadRequest($"Order with ID {orderId} not found.");
                    }

                    if (await _routesService.GetRoutesFromOrders(new List<int> { orderId }) != null)
                    {
                        _logger.LogWarning("Order ID {OrderId} already exists in another delivery.", orderId);
                        return BadRequest($"Order with ID {orderId} already exists in another delivery.");
                    }

                    var address = await _addressService.GetAddressByIdAsync(order.AddressId);
                    if (address == null || string.IsNullOrWhiteSpace(address.StreetAddress))
                    {
                        _logger.LogWarning("Address not found or is invalid for Order ID {OrderId} (AddressID: {AddressId}).", orderId, order.AddressId);
                        return BadRequest($"Address not found or is invalid for Order ID {orderId}.");
                    }
                    destinationAddressesForGoogleApi.Add(address.StreetAddress);

                    var store = _storeService.GetStoreById(order.StoreId);
                    if (store == null || string.IsNullOrWhiteSpace(store.address))
                    {
                        _logger.LogWarning("Address not found or is invalid for Order ID {OrderId} (StoreID: {StoreId}).", orderId, order.StoreId);
                        return BadRequest($"Address not found or is invalid for Order ID {orderId}.");
                    }
                    originAddressForGoogleApi = store.address;
                }

                // Construct Google Maps API URL
                string googleMapsApiKey = _configuration["GoogleMaps:ApiKey"];
                if (string.IsNullOrEmpty(googleMapsApiKey))
                {
                    _logger.LogError("Google Maps API Key is not configured.");
                    return StatusCode(StatusCodes.Status500InternalServerError, "Map service configuration error.");
                }

                string origin, destination;
                var waypoints = new List<String>();
                origin = WebUtility.UrlEncode(originAddressForGoogleApi);
                destination = WebUtility.UrlEncode(destinationAddressesForGoogleApi.Last());
                if (destinationAddressesForGoogleApi.Count > 1) // Have waypoints
                {
                    for (int i = 0; i < destinationAddressesForGoogleApi.Count() - 1; i++)
                        waypoints.Add(WebUtility.UrlEncode(destinationAddressesForGoogleApi[i]));
                }


                var googleApiUrlBuilder = new StringBuilder($"https://maps.googleapis.com/maps/api/directions/json?origin={origin}&destination={destination}");
                if (waypoints.Count != 0)
                {
                    foreach (var waypoint in waypoints)
                    {
                        googleApiUrlBuilder.Append($"&waypoints={waypoint}");
                    }
                }

                googleApiUrlBuilder.Append($"&key={googleMapsApiKey}");
                string googleApiUrl = googleApiUrlBuilder.ToString();

                _logger.LogInformation("Requesting route from Google Maps API: {Url}", googleApiUrl);

                // Call Google Maps API
                var httpClient = _httpClientFactory.CreateClient("GoogleMapsClient"); // Use a named client if configured
                HttpResponseMessage googleApiResponse;
                try
                {
                    googleApiResponse = await httpClient.GetAsync(googleApiUrl);
                }
                catch (HttpRequestException ex)
                {
                    _logger.LogError(ex, "Error calling Google Maps API.");
                    return StatusCode(StatusCodes.Status500InternalServerError, "Failed to connect to the map service.");
                }


                if (!googleApiResponse.IsSuccessStatusCode)
                {
                    var errorContent = await googleApiResponse.Content.ReadAsStringAsync();
                    _logger.LogError("Google Maps API request failed with status {StatusCode}. Response: {Response}", googleApiResponse.StatusCode, errorContent);
                    return StatusCode(StatusCodes.Status500InternalServerError, $"Failed to retrieve route data from Google Maps API. Status: {googleApiResponse.StatusCode}");
                }

                string googleMapsJson = await googleApiResponse.Content.ReadAsStringAsync();

                var routeDataModel = new DeliveryRouteData
                {
                    Data = googleMapsJson,
                    Hash = CalculateSHA256Hash(googleMapsJson)
                };

                var createdRoute = await _routesService.CreateRoute(userId, uniqueOrderIds, routeDataModel);
                var responseDto = MapToResponseDto(createdRoute);

                _logger.LogInformation("Delivery route with ID {RouteId} created successfully using Google Maps data.", createdRoute.Id);
                return CreatedAtAction(nameof(GetRouteById), new { id = createdRoute.Id }, responseDto);
            }
            catch (ArgumentException argEx)
            {
                _logger.LogWarning(argEx, "Invalid argument while creating delivery route.");
                return BadRequest(argEx.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating delivery route.");
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while creating the route.");
            }
        }


        /// <summary>
        /// Updates the route data for an existing delivery route.
        /// </summary>
        /// <param name="id">The ID of the route to update.</param>
        /// <param name="requestDto">The new route data.</param>
        [HttpPut("routes/{id:int}/routedata")]
        [ProducesResponseType(typeof(DeliveryRouteResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [Authorize(Roles = "Admin, Seller")]
        public async Task<ActionResult<DeliveryRouteResponseDto>> UpdateRouteData(int id, [FromBody] UpdateRouteDataRequestDto requestDto)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var r = await _routesService.GetRouteByIdAsync(id);
                if (r == null)
                    return BadRequest("This route does not exist");
                if (r.OwnerId != userId)
                    return BadRequest("This route is not owned by you!");
                // Ručno mapiranje RouteDataDto na RouteData model
                var routeDataModel = new DeliveryRouteData
                {
                    Data = requestDto.RouteData?.Data.ToString() ?? string.Empty,
                    Hash = requestDto.RouteData?.Hash ?? string.Empty
                };

                var updatedRoute = await _routesService.UpdateRouteDataAsync(id, routeDataModel);
                if (updatedRoute == null)
                {
                    return NotFound($"Route with ID {id} not found.");
                }
                var responseDto = MapToResponseDto(updatedRoute); // Ručno mapiranje

                _logger.LogInformation("Route data for route ID {RouteId} updated successfully.", updatedRoute.Id);
                return Ok(responseDto);
            }
            catch (InvalidDataException ide)
            {
                _logger.LogWarning(ide, "UpdateRouteData failed: {ErrorMessage}", ide.Message);
                return NotFound(ide.Message);
            }
            catch (ArgumentException argEx)
            {
                _logger.LogWarning(argEx, "Invalid argument while updating route data for route ID {RouteId}.", id);
                return BadRequest(argEx.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating route data for route ID {RouteId}.", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while updating route data.");
            }
        }

        /// <summary>
        /// Updates the order IDs for an existing delivery route.
        /// </summary>
        /// <param name="id">The ID of the route to update.</param>
        /// <param name="requestDto">The new list of order IDs.</param>
        [HttpPut("routes/{id:int}/orders")]
        [ProducesResponseType(typeof(DeliveryRouteResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [Authorize(Roles = "Admin, Seller")]
        public async Task<ActionResult<DeliveryRouteResponseDto>> UpdateRouteOrders(int id, [FromBody] UpdateRouteOrdersRequestDto requestDto)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var r = await _routesService.GetRouteByIdAsync(id);
                if (r == null)
                    return BadRequest("This route does not exist");
                if (r.OwnerId != userId)
                    return BadRequest("This route is not owned by you!");
                var updatedRoute = await _routesService.UpdateRouteOrdersAsync(id, requestDto.OrderIds);
                if (updatedRoute == null)
                {
                    return NotFound($"Route with ID {id} not found.");
                }
                var responseDto = MapToResponseDto(updatedRoute); // Ručno mapiranje

                _logger.LogInformation("Order IDs for route ID {RouteId} updated successfully.", updatedRoute.Id);
                return Ok(responseDto);
            }
            catch (InvalidDataException ide)
            {
                _logger.LogWarning(ide, "UpdateRouteOrders failed: {ErrorMessage}", ide.Message);
                return NotFound(ide.Message);
            }
            catch (ArgumentException argEx)
            {
                _logger.LogWarning(argEx, "Invalid argument while updating orders for route ID {RouteId}.", id);
                return BadRequest(argEx.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating orders for route ID {RouteId}.", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while updating route orders.");
            }
        }


        /// <summary>
        /// Deletes a delivery route by ID.
        /// </summary>
        /// <param name="id">The ID of the route to delete.</param>
        [HttpDelete("routes/{id:int}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [Authorize(Roles = "Admin, Seller")]
        public async Task<IActionResult> DeleteRoute(int id)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var r = await _routesService.GetRouteByIdAsync(id);
                if (r == null)
                    return BadRequest("This route does not exist");
                if (r.OwnerId != userId)
                    return BadRequest("This route is not owned by you!");
                await _routesService.DeleteRouteAsync(id);
                _logger.LogInformation("Delivery route with ID {RouteId} deleted successfully.", id);
                return NoContent();
            }
            catch (InvalidDataException ide)
            {
                _logger.LogWarning(ide, "DeleteRoute failed: {ErrorMessage}", ide.Message);
                return NotFound(ide.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting delivery route with ID {RouteId}.", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while deleting the route.");
            }
        }

        // --- Helper metoda za ručno mapiranje Domenskog modela na Response DTO ---
        // --- Helper metoda za ručno mapiranje Domenskog modela na Response DTO ---
        private DeliveryRouteResponseDto MapToResponseDto(DeliveryRoute route)
        {
            if (route == null)
            {
                return null!;
            }

            JsonElement dataElement;
            try
            {
                // Ensure RouteData and RouteData.Data are not null or empty before parsing
                if (route.RouteData != null && !string.IsNullOrEmpty(route.RouteData.Data))
                {
                    dataElement = JsonDocument.Parse(route.RouteData.Data).RootElement.Clone(); // Clone to avoid issues with JsonDocument disposal
                }
                else
                {
                    // Create an empty JSON object if data is missing or empty
                    dataElement = JsonDocument.Parse("{}").RootElement.Clone();
                }
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to parse RouteData.Data for route ID {RouteId}. Data: {RouteDataString}", route.Id, route.RouteData?.Data);
                // Return a DTO with an indication of error or an empty JSON object for Data
                dataElement = JsonDocument.Parse("{\"error\":\"Invalid route data format\"}").RootElement.Clone();
            }

            return new DeliveryRouteResponseDto
            {
                Id = route.Id,
                OwnerId = route.OwnerId,
                OrderIds = new List<int>(route.OrderIds ?? new List<int>()),
                RouteData = new DeliveryRouteDataDto
                {
                    Data = dataElement,
                    Hash = route.RouteData?.Hash ?? string.Empty
                },
                CreatedAt = route.CreatedAt,
                UpdatedAt = route.UpdatedAt
            };
        }

        // Helper method to calculate SHA256 hash
        private string CalculateSHA256Hash(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;

            using (SHA256 sha256Hash = SHA256.Create())
            {
                byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(input));
                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < bytes.Length; i++)
                {
                    builder.Append(bytes[i].ToString("x2"));
                }
                return builder.ToString();
            }
        }
    }
}