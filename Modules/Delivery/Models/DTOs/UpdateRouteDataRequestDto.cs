// Delivery/Dtos/UpdateRouteDataRequestDto.cs
using System.ComponentModel.DataAnnotations;

namespace Delivery.Dtos
{
    public class UpdateRouteDataRequestDto
    {
        [Required]
        public DeliveryRouteDataDto RouteData { get; set; } = new DeliveryRouteDataDto();
    }
}