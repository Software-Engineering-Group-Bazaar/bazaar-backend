// DTOs/AdDataInputDto.cs
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace MarketingAnalytics.Services.DTOs
{
    // Represents the input for a single AdData item when creating an Advertisment
    public class AdDataInputDto
    {
        public int? StoreId { get; set; }
        public int? ProductId { get; set; }
        public IFormFile? ImageFile { get; set; } // The uploaded image file, if any
        // Add any other relevant properties that are provided during creation
    }
    // Represents the complete request to create a new Advertisment
    public class CreateAdvertismentRequestDto
    {
        [Required]
        public string SellerId { get; set; } = string.Empty; // Make non-nullable in DTO if required

        [Required]
        public DateTime StartTime { get; set; }

        [Required]
        public DateTime EndTime { get; set; }

        // Optional: Add other Advertisment properties if they are set during creation
        // public bool IsActive { get; set; } // Often determined by Start/End time

        [Required]
        [MinLength(1, ErrorMessage = "At least one AdData item is required.")]
        public List<AdDataInputDto> AdDataItems { get; set; } = new List<AdDataInputDto>();
    }
}