using System.ComponentModel.DataAnnotations;
using Ticketing.Models;

namespace Ticketing.Dtos
{
    public class UpdateTicketStatusDto
    {
        [Required(ErrorMessage = "New status is required.")]
        public required string NewStatus { get; set; }
    }
}