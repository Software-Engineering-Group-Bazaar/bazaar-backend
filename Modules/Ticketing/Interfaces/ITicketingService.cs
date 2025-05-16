using System.Collections.Generic;
using System.Threading.Tasks;
using Ticketing.Dtos;
using Ticketing.Models;

namespace Ticketing.Interfaces
{
    public interface ITicketService
    {

        Task<TicketDto?> CreateTicketAsync(string requestingUserId, CreateTicketDto createDto);
        Task<TicketDto?> GetTicketByIdAsync(int ticketId, string requestingUserId, bool isAdmin);
        Task<IEnumerable<TicketDto>> GetTicketsForUserAsync(string userId, int pageNumber = 1, int pageSize = 10);
        Task<IEnumerable<TicketDto>> GetAllTicketsAsync(string? status = null, int pageNumber = 1, int pageSize = 20);
        Task<TicketDto?> UpdateTicketStatusAsync(int ticketId, TicketStatus newStatus, string updatingAdminId);
        Task<TicketDto?> ResolveTicketAsync(int ticketId, string resolvingUserId, bool isAdmin);
        Task<bool> DeleteTicketAsync(int ticketId, string requestingAdminId);
    }
}