using System.ComponentModel.DataAnnotations;
using MarketingAnalytics.Models;
using Microsoft.AspNetCore.Http;

namespace MarketingAnalytics.Services.DTOs
{
    // Represents the input for a single AdData item when creating an Advertisment
    public class AdDataInputDto
    {
        public int? StoreId { get; set; }
        public int? ProductId { get; set; }
        public string? Description { get; set; }
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
        public List<AdDataInputDto> AdDataItems { get; set; } = new List<AdDataInputDto>();
    }

    // Represents the request to update an existing Advertisment
    public class UpdateAdvertismentRequestDto
    {
        // Only include fields that are allowed to be updated
        [Required]
        public DateTime StartTime { get; set; }

        [Required]
        public DateTime EndTime { get; set; }

        public bool? IsActive { get; set; } // Optional: Allow explicit setting

        // Allows adding NEW AdData items during an Advertisment update
        public List<AdDataInputDto>? NewAdDataItems { get; set; } = null;
    }
    // Represents the request to update an existing AdData item
    public class UpdateAdDataRequestDto
    {
        // Include fields that can be updated for an AdData
        public int? StoreId { get; set; }
        public int? ProductId { get; set; }
        public string? Description { get; set; }
        // Optional: Provide a new image file to replace the existing one (if any)
        public IFormFile? ImageFile { get; set; }

        // Optional flag to explicitly remove the image without replacing it
        public bool RemoveCurrentImage { get; set; } = false;
    }

    public class AdFeaturePair
    {
        public Advertisment Ad { get; set; }
        public double[] FeatureVec { get; set; }
    }
}