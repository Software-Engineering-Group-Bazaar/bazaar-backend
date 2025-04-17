namespace Catalog.DTO
{
    public class UpdateProductAvailabilityRequestDto
    {
        public int ProductId { get; set; }
        public bool IsAvailable { get; set; }
    }
}
