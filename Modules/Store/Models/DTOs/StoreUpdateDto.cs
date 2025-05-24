public class StoreUpdateDto
{
    public string? Name { get; set; }
    public int? CategoryId { get; set; }
    public string? Address { get; set; }
    public string? Description { get; set; }
    public bool? IsActive { get; set; }
    public required int Id { get; set; }
    public double? Tax { get; set; }
}
