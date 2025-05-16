using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Chat.Dtos;
using Chat.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Notifications.Interfaces;
using Order.Interface;
using Order.Models;
using Ticketing.Data;
using Ticketing.Dtos;
using Ticketing.Interfaces;
using Ticketing.Models;
using Users.Models;

namespace Ticketing.Services
{
    public class TicketService : ITicketService
    {
        private readonly TicketingDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly IOrderService _orderService;
        private readonly IChatService _chatService;
        private readonly INotificationService _dbNotificationService;
        private readonly IPushNotificationService _pushNotificationService;
        private readonly ILogger<TicketService> _logger;
        private readonly IConfiguration _configuration;

        public TicketService(
            TicketingDbContext context,
            UserManager<User> userManager,
            IOrderService orderService,
            IChatService chatService,
            INotificationService dbNotificationService,
            IPushNotificationService pushNotificationService,
            ILogger<TicketService> logger,
            IConfiguration configuration)
        {
            _context = context;
            _userManager = userManager;
            _orderService = orderService;
            _chatService = chatService;
            _dbNotificationService = dbNotificationService;
            _pushNotificationService = pushNotificationService;
            _logger = logger;
            _configuration = configuration;
        }

        private async Task<string?> GetAdminUserIdAsync()
        {
            string adminEmail = _configuration["AppSettings:AdminEmail"] ?? "admin@bazaar.com";
            var adminUser = await _userManager.FindByEmailAsync(adminEmail);
            if (adminUser == null)
            {
                _logger.LogError("ADMIN USER with email {AdminEmail} NOT FOUND in the database. Ticket notifications to admin will fail.", adminEmail);
            }
            return adminUser?.Id;
        }

        public async Task<TicketDto?> CreateTicketAsync(string requestingUserId, CreateTicketDto createDto)
        {
            _logger.LogInformation("User {RequestingUserId} attempting to create ticket: {@CreateTicketDto}", requestingUserId, createDto);

            var userWhoCreates = await _userManager.FindByIdAsync(requestingUserId);
            if (userWhoCreates == null) throw new KeyNotFoundException($"User with ID {requestingUserId} not found.");

            int? storeIdForContext = userWhoCreates.StoreId;
            OrderModel? orderRelatedToTicket = null;

            if (createDto.OrderId.HasValue)
            {
                orderRelatedToTicket = await _orderService.GetOrderByIdAsync(createDto.OrderId.Value);
                if (orderRelatedToTicket == null)
                    throw new KeyNotFoundException($"Order with ID {createDto.OrderId.Value} not found.");

                bool isBuyerOfOrder = orderRelatedToTicket.BuyerId == requestingUserId;
                bool isSellerOfOrderStore = userWhoCreates.StoreId.HasValue && userWhoCreates.StoreId.Value == orderRelatedToTicket.StoreId;

                if (!isBuyerOfOrder && !isSellerOfOrderStore)
                {
                    _logger.LogWarning("User {RequestingUserId} is not authorized to create a ticket for Order {OrderId}.", requestingUserId, createDto.OrderId.Value);
                    throw new UnauthorizedAccessException("You are not authorized to create a ticket for this order.");
                }
                storeIdForContext = orderRelatedToTicket.StoreId;
            }

            if (!storeIdForContext.HasValue && !userWhoCreates.StoreId.HasValue)
            {
                _logger.LogWarning("Ticket creation by Buyer {UserId} without OrderId failed: Store context cannot be determined.", requestingUserId);
                throw new ArgumentException("If you are a buyer, an Order ID is required to determine the store context for the ticket.");
            }
            if (!storeIdForContext.HasValue)
            {
                _logger.LogError("CRITICAL: storeIdForContext could not be determined for ticket creation by User {UserId}.", requestingUserId);
                throw new InvalidOperationException("Could not determine store context for the ticket.");
            }

            string? adminUserId = await GetAdminUserIdAsync();
            if (string.IsNullOrEmpty(adminUserId))
            {
                throw new InvalidOperationException("Admin user for support is not configured or found.");
            }

            try
            {
                var newTicket = new Ticket
                {
                    Title = createDto.Title,
                    Description = createDto.Description,
                    CreatedAt = DateTime.UtcNow,
                    UserId = requestingUserId,
                    AssignedAdminId = adminUserId,
                    OrderId = createDto.OrderId,
                    Status = TicketStatus.Requested.ToString(),
                };
                _context.Tickets.Add(newTicket);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Ticket {TicketId} created by User {UserId}, initially assigned to Admin {AdminId}.", newTicket.Id, requestingUserId, adminUserId ?? "N/A");

                ConversationDto? conversationDto = null;
                try
                {
                    _logger.LogInformation("Attempting to create/get chat conversation for Ticket {TicketId}. User: {UserId}, Admin: {AdminId}, Store: {StoreId}",
                        newTicket.Id, requestingUserId, adminUserId, storeIdForContext.Value);

                    conversationDto = await _chatService.GetOrCreateConversationAsync(
                        requestingUserId,
                        adminUserId,
                        storeIdForContext.Value,
                        newTicket.OrderId,
                        null,
                        newTicket.Id
                    );

                    if (conversationDto == null)
                    {
                        _logger.LogError("Failed to create or retrieve chat conversation for Ticket {TicketId}. Ticket will be created without a linked conversation.", newTicket.Id);
                    }
                }
                catch (Exception chatEx)
                {
                    _logger.LogError(chatEx, "Error during GetOrCreateConversationAsync for Ticket {TicketId}. Ticket will be created without a linked conversation.", newTicket.Id);
                }


                if (conversationDto != null)
                {
                    newTicket.ConversationId = conversationDto.Id;
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Updated Ticket {TicketId} with ConversationId {ConversationId}.", newTicket.Id, conversationDto.Id);
                }
                try
                {
                    var adminUser = await _userManager.FindByIdAsync(adminUserId);
                    if (adminUser != null)
                    {
                        string userIdentifier = userWhoCreates.UserName ?? requestingUserId;
                        string notificationMessage = $"Novi tiket #{newTicket.Id} ('{newTicket.Title.Substring(0, Math.Min(30, newTicket.Title.Length))}...') je kreiran od strane {userIdentifier}.";
                        string pushTitle = "Novi Tiket Podrške";

                        await _dbNotificationService.CreateNotificationAsync(adminUser.Id, notificationMessage, newTicket.Id);
                        _logger.LogInformation("DB Notification created for Admin {AdminUserId} for new Ticket {TicketId}.", adminUser.Id, newTicket.Id);
                    }
                }
                catch (Exception ex) { _logger.LogError(ex, "Failed to send notification to admin for new Ticket {TicketId}.", newTicket.Id); }

                var adminUsernameForDto = (await _userManager.FindByIdAsync(adminUserId))?.UserName;
                return await MapTicketToDtoAsync(newTicket, userWhoCreates.UserName, adminUsernameForDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during CreateTicketAsync (before or during main save operations) for User {RequestingUserId}.", requestingUserId);
                if (ex is ArgumentException || ex is KeyNotFoundException || ex is UnauthorizedAccessException || ex is InvalidOperationException) throw;
                throw new Exception("An error occurred while creating the ticket.", ex);
            }
        }

        public async Task<TicketDto?> GetTicketByIdAsync(int ticketId, string requestingUserId, bool isAdmin)
        {
            _logger.LogInformation("User {RequestingUserId} (IsAdmin: {IsAdmin}) fetching Ticket {TicketId}", requestingUserId, isAdmin, ticketId);
            var ticket = await _context.Tickets.FindAsync(ticketId);
            if (ticket == null) return null;

            if (!isAdmin && ticket.UserId != requestingUserId && ticket.AssignedAdminId != requestingUserId) // Admin ili kreator ili dodijeljeni admin
            {
                // Ako imamo više admina, provjeri da li je requestingUserId jedan od njih
                bool isActuallyAdmin = await _userManager.IsInRoleAsync(await _userManager.FindByIdAsync(requestingUserId), "Admin");
                if (!isActuallyAdmin)
                {
                    _logger.LogWarning("User {RequestingUserId} unauthorized to access Ticket {TicketId}", requestingUserId, ticketId);
                    throw new UnauthorizedAccessException("You are not authorized to view this ticket.");
                }
            }

            var user = await _userManager.FindByIdAsync(ticket.UserId);
            User? admin = null;
            if (!string.IsNullOrEmpty(ticket.AssignedAdminId))
            {
                admin = await _userManager.FindByIdAsync(ticket.AssignedAdminId);
            }
            return await MapTicketToDtoAsync(ticket, user?.UserName, admin?.UserName);
        }

        public async Task<IEnumerable<TicketDto>> GetTicketsForUserAsync(string userId, int pageNumber = 1, int pageSize = 10)
        {
            _logger.LogInformation("Fetching tickets for User {UserId}, Page: {PageNumber}, Size: {PageSize}", userId, pageNumber, pageSize);
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) throw new KeyNotFoundException($"User {userId} not found.");

            var query = _context.Tickets
               .Where(t => t.UserId == userId || t.AssignedAdminId == userId) // Korisnik vidi svoje i one koji su mu dodijeljeni ako je admin
               .OrderByDescending(t => t.CreatedAt)
               .Skip((pageNumber - 1) * pageSize)
               .Take(pageSize)
               .AsNoTracking();

            var tickets = await query.ToListAsync();
            var dtos = new List<TicketDto>();
            foreach (var ticket in tickets)
            {
                var ticketCreatorUser = await _userManager.FindByIdAsync(ticket.UserId);
                User? assignedAdminUser = null;
                if (!string.IsNullOrEmpty(ticket.AssignedAdminId)) assignedAdminUser = await _userManager.FindByIdAsync(ticket.AssignedAdminId);
                dtos.Add(await MapTicketToDtoAsync(ticket, ticketCreatorUser?.UserName, assignedAdminUser?.UserName));
            }
            return dtos;
        }

        public async Task<IEnumerable<TicketDto>> GetAllTicketsAsync(string? status = null, int pageNumber = 1, int pageSize = 20)
        {
            // Samo Admin smije ovo, kontroler bi trebao provjeriti rolu
            _logger.LogInformation("Admin fetching all tickets. Status filter: {Status}, Page: {PageNumber}, Size: {PageSize}", status ?? "All", pageNumber, pageSize);
            var query = _context.Tickets.AsQueryable();

            if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<TicketStatus>(status, true, out var statusEnum))
            {
                query = query.Where(t => t.Status == statusEnum.ToString());
            }

            query = query.OrderByDescending(t => t.CreatedAt)
                         .Skip((pageNumber - 1) * pageSize)
                         .Take(pageSize)
                         .AsNoTracking();

            var tickets = await query.ToListAsync();
            var dtos = new List<TicketDto>();
            foreach (var ticket in tickets)
            {
                var user = await _userManager.FindByIdAsync(ticket.UserId);
                User? admin = null;
                if (!string.IsNullOrEmpty(ticket.AssignedAdminId)) admin = await _userManager.FindByIdAsync(ticket.AssignedAdminId);
                dtos.Add(await MapTicketToDtoAsync(ticket, user?.UserName, admin?.UserName));
            }
            return dtos;
        }

        public async Task<TicketDto?> UpdateTicketStatusAsync(int ticketId, TicketStatus newStatus, string updatingAdminId)
        {
            _logger.LogInformation("Admin {AdminId} attempting to update status of Ticket {TicketId} to {NewStatus}", updatingAdminId, ticketId, newStatus);
            var ticket = await _context.Tickets.FindAsync(ticketId);
            if (ticket == null)
            {
                _logger.LogWarning("UpdateTicketStatus: Ticket {TicketId} not found.", ticketId);
                return null;
            }

            var adminUserPerformingUpdate = await _userManager.FindByIdAsync(updatingAdminId);
            if (adminUserPerformingUpdate == null || !(await _userManager.IsInRoleAsync(adminUserPerformingUpdate, "Admin")))
            {
                _logger.LogWarning("User {UpdatingUserId} is not an Admin. Denying ticket status update for Ticket {TicketId}.", updatingAdminId, ticketId);
                throw new UnauthorizedAccessException("Only admins can update ticket status.");
            }

            string oldStatus = ticket.Status;
            ticket.Status = newStatus.ToString();

            if (newStatus == TicketStatus.Resolved && ticket.ResolvedAt == null)
            {
                ticket.ResolvedAt = DateTime.UtcNow;
                ticket.AssignedAdminId = updatingAdminId;
            }
            else if (newStatus == TicketStatus.Open && string.IsNullOrEmpty(ticket.AssignedAdminId))
            {
                ticket.AssignedAdminId = updatingAdminId;
            }
            else if (oldStatus == TicketStatus.Resolved.ToString() && newStatus != TicketStatus.Resolved)
            {
                ticket.ResolvedAt = null;
            }

            _context.Entry(ticket).State = EntityState.Modified;
            try
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation("Status for Ticket {TicketId} updated to {NewStatus} by Admin {AdminId}", ticketId, newStatus, updatingAdminId);

                var ticketCreator = await _userManager.FindByIdAsync(ticket.UserId);
                if (ticketCreator != null)
                {
                    string message = $"Status Vašeg tiketa #{ticket.Id} ('{ticket.Title.Substring(0, Math.Min(20, ticket.Title.Length))}...') je promijenjen u '{newStatus}'.";
                    string pushTitle = "Status Tiketa Ažuriran";
                    var pushData = new Dictionary<string, string> {
                        { "ticketId", ticket.Id.ToString() },
                        { "conversationId", ticket.ConversationId?.ToString() ?? "" },
                        { "screen", "TicketDetails" }
                    };

                    await _dbNotificationService.CreateNotificationAsync(ticketCreator.Id, message, ticket.Id);
                    _logger.LogInformation("DB Notification created for User {UserId} for Ticket {TicketId} status update.", ticketCreator.Id, ticket.Id);

                    if (!string.IsNullOrWhiteSpace(ticketCreator.FcmDeviceToken))
                    {
                        try
                        {
                            await _pushNotificationService.SendPushNotificationAsync(ticketCreator.FcmDeviceToken, pushTitle, message, pushData);
                            _logger.LogInformation("Push Notification initiated for User {UserId} for Ticket {TicketId} status update.", ticketCreator.Id, ticket.Id);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to send Push Notification to User {UserId} for Ticket {TicketId} status update.", ticketCreator.Id, ticket.Id);
                        }
                    }
                    else { _logger.LogWarning("User {UserId} has no FCM token for Ticket {TicketId} status update notification.", ticketCreator.Id, ticket.Id); }
                }
                return await MapTicketToDtoAsync(ticket, ticketCreator?.UserName, adminUserPerformingUpdate.UserName);
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Error updating status for Ticket {TicketId}", ticketId);
                return null;
            }
        }

        public async Task<bool> DeleteTicketAsync(int ticketId, string requestingAdminId)
        {
            _logger.LogInformation("Admin {AdminId} attempting to delete Ticket {TicketId}", requestingAdminId, ticketId);
            // Već provjereno u kontroleru da je admin
            var ticket = await _context.Tickets.FindAsync(ticketId);
            if (ticket == null) return false;

            _context.Tickets.Remove(ticket);
            try { await _context.SaveChangesAsync(); return true; }
            catch (DbUpdateException ex) { _logger.LogError(ex, "Error deleting Ticket {TicketId}", ticketId); return false; }
        }

        private async Task<TicketDto> MapTicketToDtoAsync(Ticket ticket, string? userUsername, string? adminUsername)
        {
            return new TicketDto
            {
                Id = ticket.Id,
                Title = ticket.Title,
                Description = ticket.Description,
                CreatedAt = ticket.CreatedAt,
                ResolvedAt = ticket.ResolvedAt,
                UserId = ticket.UserId,
                UserUsername = userUsername ?? "Nepoznat",
                AssignedAdminId = ticket.AssignedAdminId,
                AdminUsername = adminUsername ?? "Nije dodijeljen",
                ConversationId = ticket.ConversationId,
                OrderId = ticket.OrderId,
                Status = ticket.Status,
                IsResolved = ticket.Status == TicketStatus.Resolved.ToString() || ticket.ResolvedAt.HasValue
            };
        }
    }
}