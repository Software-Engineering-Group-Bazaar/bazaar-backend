using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Chat.Dtos;
using Chat.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Chat.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ChatController : ControllerBase
    {
        private readonly IChatService _chatService;
        private readonly ILogger<ChatController> _logger;

        public ChatController(IChatService chatService, ILogger<ChatController> logger)
        {
            _chatService = chatService;
            _logger = logger;
        }

        // GET /api/Chat/conversations
        [HttpGet("conversations")]
        [ProducesResponseType(typeof(IEnumerable<ConversationDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<ConversationDto>>> GetMyConversations()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized("User ID not found.");

            _logger.LogInformation("User {UserId} fetching their conversations.", userId);
            try
            {
                var conversations = await _chatService.GetConversationsForUserAsync(userId);
                return Ok(conversations);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching conversations for User {UserId}", userId);
                return StatusCode(500, "An error occurred while fetching conversations.");
            }
        }

        // GET /api/Chat/conversations/{conversationId}/messages
        [HttpGet("conversations/{conversationId:int}/messages")]
        [ProducesResponseType(typeof(IEnumerable<MessageDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<IEnumerable<MessageDto>>> GetConversationMessages(
            int conversationId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 30)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized("User ID not found.");
            bool isAdmin = User.IsInRole("Admin"); // Za filtriranje privatnih poruka

            _logger.LogInformation("User {UserId} (IsAdmin: {IsAdmin}) fetching messages for Conversation {ConversationId}, Page: {Page}, Size: {PageSize}",
                userId, isAdmin, conversationId, page, pageSize);
            try
            {
                // Servis radi autorizaciju (CanUserAccessConversationAsync) i filtriranje privatnih
                var messages = await _chatService.GetConversationMessagesAsync(conversationId, userId, isAdmin, page, pageSize);
                return Ok(messages);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "User {UserId} unauthorized access to messages for Conversation {ConversationId}", userId, conversationId);
                return Forbid("You do not have access to this conversation.");
            }
            catch (KeyNotFoundException ex) // Ako konverzacija ne postoji
            {
                _logger.LogWarning(ex, "Conversation {ConversationId} not found when fetching messages for User {UserId}", conversationId, userId);
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching messages for Conversation {ConversationId}, User {UserId}", conversationId, userId);
                return StatusCode(500, "An error occurred while fetching messages.");
            }
        }


        // POST /api/Chat/conversations/{conversationId}/markasread
        [HttpPost("conversations/{conversationId:int}/markasread")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> MarkConversationAsRead(int conversationId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized("User ID not found.");

            _logger.LogInformation("User {UserId} marking messages as read in Conversation {ConversationId}", userId, conversationId);
            try
            {
                bool success = await _chatService.MarkMessagesAsReadAsync(conversationId, userId);
                if (!success && !(await _chatService.CanUserAccessConversationAsync(userId, conversationId))) // Provjeri da li je NotFound
                {
                    return NotFound($"Conversation {conversationId} not found or no messages to mark as read.");
                }
                return NoContent();
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "User {UserId} unauthorized to mark messages as read for Conversation {ConversationId}", userId, conversationId);
                return Forbid("You do not have access to this conversation.");
            }
            catch (KeyNotFoundException ex) // Ako konverzacija ne postoji
            {
                _logger.LogWarning(ex, "Conversation {ConversationId} not found when marking as read for User {UserId}", conversationId, userId);
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking messages as read for Conversation {ConversationId}, User {UserId}", conversationId, userId);
                return StatusCode(500, "An error occurred.");
            }
        }

        // POST /api/Chat/conversations/find-or-create
        [HttpPost("conversations/find-or-create")]
        [ProducesResponseType(typeof(ConversationDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ConversationDto), StatusCodes.Status201Created)]
        // ... (ostali ProducesResponseType) ...
        public async Task<ActionResult<ConversationDto>> FindOrCreateConversation([FromBody] FindOrCreateConversationDto findDto) // DTO sada ima ProductId
        {
            var requestingUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(requestingUserId)) return Unauthorized("User ID not found.");
            if (!ModelState.IsValid) return BadRequest(ModelState);

            _logger.LogInformation("User {RU} finding or creating conversation with TargetUser {TU}, Store {S}, Order {O}, Product {P}",
                requestingUserId, findDto.TargetUserId, findDto.StoreId, findDto.OrderId?.ToString() ?? "N/A", findDto.ProductId?.ToString() ?? "N/A");
            try
            {
                var conversationDto = await _chatService.GetOrCreateConversationAsync(
                    requestingUserId,
                    findDto.TargetUserId,
                    findDto.StoreId,
                    findDto.OrderId, // Proslijedi OrderId
                    findDto.ProductId  // <<< Proslijedi ProductId
                );

                if (conversationDto == null)
                {
                    return StatusCode(500, "Could not find or create conversation.");
                }

                // Približna provjera da li je nova (za CreatedAtAction)
                bool wasJustCreated = conversationDto.CreatedAt >= DateTime.UtcNow.AddSeconds(-5);
                string actionName = wasJustCreated ? nameof(GetConversationMessages) : nameof(GetConversationMessages); // Linkuj na messages
                object routeValues = new { conversationId = conversationDto.Id };

                if (wasJustCreated)
                {
                    return CreatedAtAction(actionName, routeValues, conversationDto);
                }
                return Ok(conversationDto);
            }
            // ... (catch blokovi ostaju isti) ...
            catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error in FindOrCreateConversation."); return StatusCode(500, "An error occurred."); }
        }
    }
}