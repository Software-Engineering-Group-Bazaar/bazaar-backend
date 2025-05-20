// Delivery/Dtos/DeliveryRouteResponseDto.cs
using System;
using System.Collections.Generic;

namespace Delivery.Dtos
{
    public class DeliveryRouteResponseDto
    {
        public int Id { get; set; }
        public string OwnerId { get; set; } = string.Empty;
        public List<int> OrderIds { get; set; } = new List<int>();
        public DeliveryRouteDataDto RouteData { get; set; } = new DeliveryRouteDataDto();
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}