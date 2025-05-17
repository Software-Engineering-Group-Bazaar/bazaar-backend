using System.ComponentModel.DataAnnotations;

namespace Delivery.Models
{
    public class DeliveryRoute
    {
        public int Id { get; set; } // Primary Key

        [Required]
        public string OwnerId { get; set; } = string.Empty;
        public List<int> OrderIds { get; set; } = new List<int>();

        [Required]
        public DeliveryRouteData RouteData { get; set; } = new DeliveryRouteData();

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
