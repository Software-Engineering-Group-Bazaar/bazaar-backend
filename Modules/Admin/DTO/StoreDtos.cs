namespace Admin.Dto
{
    public class StoreDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = default!;
        public string Address { get; set; } = default!;
        public string? Description { get; set; }
        public bool IsActive { get; set; }
        public string CategoryName { get; set; } = default!;
        public int? CategoryId { get; set; } = null;
    }
}