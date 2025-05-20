// Delivery/Dtos/DeliveryRouteDataDto.cs
using System.Text.Json;

namespace Delivery.Dtos
{
    public class DeliveryRouteDataDto
    {
        public JsonElement Data { get; set; }
        public string Hash { get; set; } = string.Empty;
    }
}