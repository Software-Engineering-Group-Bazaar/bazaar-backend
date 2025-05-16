using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Ticketing.Dtos;
using Ticketing.Interfaces;
using Ticketing.Models;

namespace Ticketing.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class TicketsController : ControllerBase
    {
        private readonly ITicketService _ticketService;
        private readonly ILogger<TicketsController> _logger;

        public TicketsController(ITicketService ticketService, ILogger<TicketsController> logger)
        {
            _ticketService = ticketService ?? throw new ArgumentNullException(nameof(ticketService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        // POST /api/Tickets/create
        [HttpPost("create")]
        [Authorize(Roles = "Buyer, Seller")]
        [ProducesResponseType(typeof(TicketDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)] // Ako OrderId ne postoji
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<TicketDto>> CreateTicket([FromBody] CreateTicketDto createDto)
        {
            var requestingUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(requestingUserId)) return Unauthorized("User ID claim not found.");

            _logger.LogInformation("User {UserId} attempting to create a new ticket: {@CreateTicketDto}", requestingUserId, createDto);

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("CreateTicket failed model validation for User {UserId}. Errors: {@ModelState}",
                    requestingUserId, ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage)));
                return BadRequest(ModelState);
            }

            try
            {
                var createdTicketDto = await _ticketService.CreateTicketAsync(requestingUserId, createDto);
                if (createdTicketDto == null)
                {
                    _logger.LogWarning("Ticket creation failed for User {UserId} (service returned null). DTO: {@CreateTicketDto}", requestingUserId, createDto);
                    return BadRequest("Failed to create ticket. Associated order might not exist or another validation failed.");
                }
                _logger.LogInformation("Ticket {TicketId} created successfully by User {UserId}.", createdTicketDto.Id, requestingUserId);
                return CreatedAtAction(nameof(GetTicketById), new { id = createdTicketDto.Id }, createdTicketDto);
            }
            catch (KeyNotFoundException ex)
            {
                _logger.LogWarning(ex, "KeyNotFoundException during ticket creation by User {UserId}.", requestingUserId);
                return NotFound(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized ticket creation attempt by User {UserId}.", requestingUserId);
                return Forbid(ex.Message);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "ArgumentException during ticket creation by User {UserId}.", requestingUserId);
                return BadRequest(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "InvalidOperationException during ticket creation by User {UserId}.", requestingUserId);
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during ticket creation by User {UserId}.", requestingUserId);
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred while creating the ticket.");
            }
        }

        // GET /api/Tickets 
        [HttpGet]
        [Authorize(Roles = "Buyer, Seller, Admin")]
        [ProducesResponseType(typeof(IEnumerable<TicketDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<IEnumerable<TicketDto>>> GetMyTickets(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized("User ID claim not found.");

            _logger.LogInformation("User {UserId} fetching their tickets. Page: {Page}, Size: {PageSize}", userId, pageNumber, pageSize);
            try
            {
                var tickets = await _ticketService.GetTicketsForUserAsync(userId, pageNumber, pageSize);
                return Ok(tickets);
            }
            catch (KeyNotFoundException ex)
            {
                _logger.LogWarning(ex, "User {UserId} not found when fetching their tickets.", userId);
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching tickets for User {UserId}.", userId);
                return StatusCode(500, "An error occurred while fetching tickets.");
            }
        }

        // GET /api/Tickets/all (za Admina)
        [HttpGet("all")]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(typeof(IEnumerable<TicketDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<IEnumerable<TicketDto>>> GetAllTickets(
            [FromQuery] string? status = null,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20)
        {
            var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            _logger.LogInformation("Admin {AdminId} fetching all tickets. Status filter: {Status}, Page: {Page}, Size: {PageSize}",
                adminId, status ?? "All", pageNumber, pageSize);
            try
            {
                var tickets = await _ticketService.GetAllTicketsAsync(status, pageNumber, pageSize);
                return Ok(tickets);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching all tickets for Admin {AdminId}.", adminId);
                return StatusCode(500, "An error occurred while fetching all tickets.");
            }
        }


        // GET /api/Tickets/{id} 
        [HttpGet("{id:int}")]
        [Authorize(Roles = "Admin, Buyer, Seller")]
        [ProducesResponseType(typeof(TicketDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<TicketDto>> GetTicketById(int id)
        {
            if (id <= 0) return BadRequest("Invalid Ticket ID.");

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized("User ID claim not found.");
            bool isAdmin = User.IsInRole("Admin");

            _logger.LogInformation("User {UserId} (IsAdmin: {IsAdmin}) fetching details for Ticket {TicketId}", userId, isAdmin, id);
            try
            {
                var ticketDto = await _ticketService.GetTicketByIdAsync(id, userId, isAdmin);
                if (ticketDto == null)
                {
                    _logger.LogWarning("Ticket {TicketId} not found or access denied for User {UserId}.", id, userId);
                    return NotFound($"Ticket with ID {id} not found or access denied.");
                }
                return Ok(ticketDto);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "User {UserId} unauthorized access to Ticket {TicketId}.", userId, id);
                return Forbid(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching Ticket {TicketId} for User {UserId}.", id, userId);
                return StatusCode(500, "An error occurred while fetching the ticket.");
            }
        }

        // PUT /api/Tickets/{id}/status 
        [HttpPut("{id:int}/status")]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(typeof(TicketDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<TicketDto>> UpdateTicketStatus(int id, [FromBody] UpdateTicketStatusDto updateDto)
        {
            if (id <= 0) return BadRequest("Invalid Ticket ID.");
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(adminId)) return Unauthorized("Admin User ID claim not found.");

            _logger.LogInformation("Admin {AdminId} attempting to update status for Ticket {TicketId} to {NewStatus}",
                adminId, id, updateDto.NewStatus);

            if (!Enum.TryParse<TicketStatus>(updateDto.NewStatus, true, out var statusEnum))
            {
                return BadRequest($"Invalid status value: {updateDto.NewStatus}. Valid statuses are: {string.Join(", ", Enum.GetNames(typeof(TicketStatus)))}");
            }

            try
            {
                var updatedTicketDto = await _ticketService.UpdateTicketStatusAsync(id, statusEnum, adminId);
                if (updatedTicketDto == null)
                {
                    _logger.LogWarning("Ticket {TicketId} not found or status update failed by Admin {AdminId}.", id, adminId);
                    return NotFound($"Ticket with ID {id} not found or update failed.");
                }
                _logger.LogInformation("Ticket {TicketId} status successfully updated to {NewStatus} by Admin {AdminId}.", id, statusEnum, adminId);
                return Ok(updatedTicketDto);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized status update attempt for Ticket {TicketId} by Admin {AdminId}.", id, adminId);
                return Forbid(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Invalid operation during status update for Ticket {TicketId} by Admin {AdminId}.", id, adminId);
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating status for Ticket {TicketId} by Admin {AdminId}.", id, adminId);
                return StatusCode(500, "An error occurred while updating ticket status.");
            }
        }

        // DELETE /api/Tickets/{id} (SAMO Admin)
        [HttpDelete("{id:int}")]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteTicket(int id)
        {
            if (id <= 0) return BadRequest("Invalid Ticket ID.");

            var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(adminId)) return Unauthorized("Admin User ID claim not found.");

            _logger.LogInformation("Admin {AdminId} attempting to delete Ticket {TicketId}", adminId, id);
            try
            {
                var success = await _ticketService.DeleteTicketAsync(id, adminId);
                if (!success)
                {
                    _logger.LogWarning("Ticket {TicketId} not found for deletion by Admin {AdminId}.", id, adminId);
                    return NotFound($"Ticket with ID {id} not found.");
                }
                _logger.LogInformation("Ticket {TicketId} successfully deleted by Admin {AdminId}.", id, adminId);
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting Ticket {TicketId} by Admin {AdminId}.", id, adminId);
                return StatusCode(500, "An error occurred while deleting the ticket.");
            }
        }
    }
}