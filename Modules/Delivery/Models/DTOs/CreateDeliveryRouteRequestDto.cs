// Delivery/Dtos/CreateDeliveryRouteRequestDto.cs
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Delivery.Dtos
{
    public class CreateDeliveryRouteRequestDto
    {
        [Required]
        public string OwnerId { get; set; } = string.Empty;

        [Required]
        [MinLength(1, ErrorMessage = "At least one OrderId must be provided.")]
        public List<int> OrderIds { get; set; } = new List<int>();

        [Required]
        public DeliveryRouteDataDto RouteData { get; set; } = new DeliveryRouteDataDto();
    }
}