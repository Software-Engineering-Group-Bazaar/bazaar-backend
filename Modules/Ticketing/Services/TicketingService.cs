using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Transactions;
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

            int? storeIdForTicket = userWhoCreates.StoreId;
            OrderModel? orderRelatedToTicket = null;

            if (createDto.OrderId.HasValue)
            {
                orderRelatedToTicket = await _orderService.GetOrderByIdAsync(createDto.OrderId.Value);
                if (orderRelatedToTicket == null)
                    throw new KeyNotFoundException($"Order with ID {createDto.OrderId.Value} not found.");

                // Autorizacija: Da li ovaj korisnik smije kreirati tiket za ovu narudžbu?
                // Ako je Buyer, mora biti njegova narudžba.
                // Ako je Seller, narudžba mora biti iz njegove prodavnice.
                bool isBuyerOfOrder = orderRelatedToTicket.BuyerId == requestingUserId;
                bool isSellerOfOrderStore = userWhoCreates.StoreId.HasValue && userWhoCreates.StoreId.Value == orderRelatedToTicket.StoreId;

                if (!isBuyerOfOrder && !isSellerOfOrderStore)
                {
                    _logger.LogWarning("User {RequestingUserId} is not authorized to create a ticket for Order {OrderId}.", requestingUserId, createDto.OrderId.Value);
                    throw new UnauthorizedAccessException("You are not authorized to create a ticket for this order.");
                }
                storeIdForTicket = orderRelatedToTicket.StoreId; // Uzmi StoreId iz narudžbe
            }

            if (!storeIdForTicket.HasValue && userWhoCreates.StoreId == null) // Buyer kreira generalni tiket bez OrderId?
            {
                _logger.LogWarning("Ticket creation attempt by Buyer {UserId} without OrderId, StoreId context cannot be determined.", requestingUserId);
                throw new ArgumentException("Store context could not be determined. If you are a buyer, please specify an Order ID.");
            }
            if (!storeIdForTicket.HasValue) // Ako je Seller, StoreId bi trebao biti postavljen
            {
                _logger.LogError("CRITICAL: StoreId for ticket could not be determined for user {UserId}.", requestingUserId);
                throw new InvalidOperationException("Could not determine store context for the ticket.");
            }

            string? adminUserId = await GetAdminUserIdAsync();
            if (string.IsNullOrEmpty(adminUserId))
            {
                _logger.LogError("Admin user ID could not be retrieved. Cannot create ticket conversation with admin.");
                throw new InvalidOperationException("Admin user for support is not configured correctly.");
            }

            using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
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
                _logger.LogInformation("Ticket {TicketId} created by User {UserId}.", newTicket.Id, requestingUserId);

                // Kreiraj povezanu chat konverzaciju
                _logger.LogInformation("Attempting to create/get chat conversation for Ticket {TicketId}. User: {UserId}, Admin: {AdminId}, Store: {StoreId}",
                    newTicket.Id, requestingUserId, adminUserId, storeIdForTicket.Value);

                var conversationDto = await _chatService.GetOrCreateConversationAsync(
                    requestingUserId,
                    adminUserId,
                    storeIdForTicket.Value,
                    newTicket.OrderId,
                    null,
                    newTicket.Id
                );

                if (conversationDto == null)
                    throw new InvalidOperationException($"Could not establish a chat conversation for ticket {newTicket.Id}.");

                newTicket.ConversationId = conversationDto.Id;
                _context.Entry(newTicket).State = EntityState.Modified;
                await _context.SaveChangesAsync();
                _logger.LogInformation("Updated Ticket {TicketId} with ConversationId {ConversationId}.", newTicket.Id, conversationDto.Id);

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

                        if (!string.IsNullOrWhiteSpace(adminUser.FcmDeviceToken))
                        {
                            var pushData = new Dictionary<string, string> {
                                { "ticketId", newTicket.Id.ToString() },
                                { "conversationId", conversationDto.Id.ToString() },
                                { "screen", "AdminTicketChat" }
                            };
                            await _pushNotificationService.SendPushNotificationAsync(adminUser.FcmDeviceToken, pushTitle, notificationMessage, pushData);
                            _logger.LogInformation("Push Notification initiated for Admin {AdminUserId} for new Ticket {TicketId}.", adminUser.Id, newTicket.Id);
                        }
                        else _logger.LogWarning("Admin {AdminUserId} has no FCM token for new ticket notification.", adminUser.Id);
                    }
                }
                catch (Exception ex) { _logger.LogError(ex, "Failed to send notification to admin for new Ticket {TicketId}.", newTicket.Id); }

                scope.Complete();
                _logger.LogInformation("Transaction completed for Ticket {TicketId}.", newTicket.Id);

                return await MapTicketToDtoAsync(newTicket, userWhoCreates.UserName, (await _userManager.FindByIdAsync(adminUserId))?.UserName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during CreateTicketAsync transaction for User {RequestingUserId}.", requestingUserId);
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
            if (ticket == null) return null;

            var isAdminUser = await _userManager.FindByIdAsync(updatingAdminId);
            if (isAdminUser == null || !(await _userManager.IsInRoleAsync(isAdminUser, "Admin")))
            {
                throw new UnauthorizedAccessException("Only admins can update ticket status.");
            }

            ticket.Status = newStatus.ToString();
            if (newStatus == TicketStatus.Resolved && ticket.ResolvedAt == null)
            {
                ticket.ResolvedAt = DateTime.UtcNow;
                ticket.AssignedAdminId = updatingAdminId;
            }
            else if (newStatus == TicketStatus.Open && ticket.AssignedAdminId == null)
            {
                ticket.AssignedAdminId = updatingAdminId;
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
                    await _dbNotificationService.CreateNotificationAsync(ticketCreator.Id, message, ticket.Id);
                    if (!string.IsNullOrWhiteSpace(ticketCreator.FcmDeviceToken))
                    {
                        await _pushNotificationService.SendPushNotificationAsync(ticketCreator.FcmDeviceToken, "Status Tiketa Ažuriran", message, new Dictionary<string, string> { { "ticketId", ticket.Id.ToString() } });
                    }
                }
                return await MapTicketToDtoAsync(ticket, ticketCreator?.UserName, isAdminUser.UserName);
            }
            catch (DbUpdateException ex) { _logger.LogError(ex, "Error updating status for Ticket {TicketId}", ticketId); return null; }
        }

        public async Task<TicketDto?> ResolveTicketAsync(int ticketId, string resolvingUserId, bool isAdmin)
        {
            var userResolving = await _userManager.FindByIdAsync(resolvingUserId);
            if (userResolving == null) throw new KeyNotFoundException($"User {resolvingUserId} not found.");

            if (isAdmin)
            {
                return await UpdateTicketStatusAsync(ticketId, TicketStatus.Resolved, resolvingUserId);
            }
            else
            { // Korisnik pokušava riješiti svoj tiket
                var ticket = await _context.Tickets.FindAsync(ticketId);
                if (ticket == null) return null;
                if (ticket.UserId != resolvingUserId) throw new UnauthorizedAccessException("You can only resolve your own tickets.");
                if (ticket.Status != TicketStatus.Open.ToString()) // Samo ako je Admin otvorio
                    throw new InvalidOperationException("Ticket can only be resolved by user if its status is 'Open'.");

                ticket.Status = TicketStatus.Resolved.ToString();
                ticket.ResolvedAt = DateTime.UtcNow;
                _context.Entry(ticket).State = EntityState.Modified;
                try
                {
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Ticket {TicketId} resolved by User {ResolvingUserId}", ticketId, resolvingUserId);
                    // Nema notifikacije samom sebi. Možda notifikacija adminu?
                    return await MapTicketToDtoAsync(ticket, userResolving.UserName, (await _userManager.FindByIdAsync(ticket.AssignedAdminId ?? ""))?.UserName);
                }
                catch (DbUpdateException ex) { _logger.LogError(ex, "Error resolving Ticket {TicketId} by user.", ticketId); return null; }
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