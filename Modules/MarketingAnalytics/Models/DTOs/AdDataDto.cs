using System; // Included for potential future use or attributes

// Namespace for Data Transfer Objects
namespace MarketingAnalytics.Dtos
{
    /// <summary>
    /// Data Transfer Object for AdData information.
    /// Represents specific data points associated with an advertisement.
    /// </summary>
    public class AdDataDto
    {
        public int Id { get; set; }
        public string? ImageUrl { get; set; } // Nullable string
        public int? StoreId { get; set; }     // Nullable int
        public int? ProductId { get; set; }   // Nullable int
        public string? Description { get; set; } // Nullable string

        // Note: Foreign key (AdvertismentId) and navigation property (Advertisment)
        // are intentionally omitted in the DTO for common use cases.
    }
}