namespace AdminApi.DTOs
{
    public class StoreGetDto
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public required string CategoryName { get; set; }

        // --- NOVA ADRESA ---
        public required string StreetAndNumber { get; set; }
        public required string City { get; set; }
        public required string Municipality { get; set; }
        public string? PostalCode { get; set; }
        public string? Country { get; set; }
        // --------------------

        public string? Description { get; set; }
        public bool IsActive { get; set; }
    }
}
