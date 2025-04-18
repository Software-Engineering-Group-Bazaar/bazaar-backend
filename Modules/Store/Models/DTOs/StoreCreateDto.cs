public class StoreCreateDto
{
    public required string Name { get; set; }
    public int CategoryId { get; set; }
    public required string Address { get; set; }
    public required string Description { get; set; }
    public required int PlaceId { get; set; }
}
