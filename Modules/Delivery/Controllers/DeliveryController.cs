using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Delivery.Dtos;      // Za DTO klase
using Delivery.Interfaces; // Za IRoutesService
using Delivery.Models;    // Za DeliveryRouteData (ako servisi vraćaju modele)
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Order.Interface;
using Order.Services;

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

        public DeliveryController(IRoutesService routesService, ILogger<DeliveryController> logger, IOrderService orderService)
        {
            _routesService = routesService ?? throw new ArgumentNullException(nameof(routesService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _orderService = orderService ?? throw new ArgumentNullException(nameof(orderService));
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
        /// Creates a new delivery route.
        /// </summary>
        /// <param name="requestDto">The request data for creating a new route.</param>
        [HttpPost("routes")]
        [ProducesResponseType(typeof(DeliveryRouteResponseDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<DeliveryRouteResponseDto>> CreateRoute([FromBody] CreateDeliveryRouteRequestDto requestDto)
        {
            try
            {
                foreach (var item in requestDto.OrderIds)
                {
                    if (await _orderService.GetOrderByIdAsync(item) == null)
                    {
                        throw new ArgumentException("No order with that ID.");
                    }

                    if (await _routesService.GetRoutesFromOrders(new List<int> { item }) != null)
                    {
                        throw new ArgumentException("Order already exists in another delivery.");
                    }
                }

                // Ručno mapiranje RouteDataDto na RouteData model
                var routeDataModel = new DeliveryRouteData
                {
                    Data = requestDto.RouteData?.Data.ToString() ?? string.Empty, // Handle null requestDto.RouteData
                    Hash = requestDto.RouteData?.Hash ?? string.Empty
                };

                var createdRoute = await _routesService.CreateRoute(requestDto.OwnerId, requestDto.OrderIds, routeDataModel);
                var responseDto = MapToResponseDto(createdRoute); // Ručno mapiranje

                _logger.LogInformation("Delivery route with ID {RouteId} created successfully.", createdRoute.Id);
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
        public async Task<ActionResult<DeliveryRouteResponseDto>> UpdateRouteData(int id, [FromBody] UpdateRouteDataRequestDto requestDto)
        {
            try
            {
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
        public async Task<ActionResult<DeliveryRouteResponseDto>> UpdateRouteOrders(int id, [FromBody] UpdateRouteOrdersRequestDto requestDto)
        {
            try
            {
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
        public async Task<IActionResult> DeleteRoute(int id)
        {
            try
            {
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
        private DeliveryRouteResponseDto MapToResponseDto(DeliveryRoute route)
        {
            if (route == null)
            {
                // Možeš odlučiti da li ćeš baciti ArgumentNullException ili vratiti null/prazan DTO
                // Za ovaj primer, vraćamo null, ali kontroler će proveriti i vratiti 404 ako je potrebno.
                // Ako bi ova metoda bila pozvana interno gde route *ne sme* biti null, bacanje izuzetka bi bilo bolje.
                return null!; // Ili new DeliveryRouteResponseDto(); ako želiš prazan objekat umesto null reference
            }

            return new DeliveryRouteResponseDto
            {
                Id = route.Id,
                OwnerId = route.OwnerId,
                OrderIds = new List<int>(route.OrderIds), // Kreiraj novu listu da izbegneš modifikaciju originala
                RouteData = new DeliveryRouteDataDto
                {
                    Data = JsonDocument.Parse(route.RouteData.Data).RootElement, // Proveri da li je RouteData null
                    Hash = route.RouteData?.Hash ?? string.Empty
                },
                CreatedAt = route.CreatedAt,
                UpdatedAt = route.UpdatedAt
            };
        }
    }
}