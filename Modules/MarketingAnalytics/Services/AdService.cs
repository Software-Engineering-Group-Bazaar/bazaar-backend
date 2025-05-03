using Catalog.Interfaces;
using MarketingAnalytics.Models;
using MarketingAnalytics.Services.DTOs;
using Microsoft.EntityFrameworkCore;

namespace MarketingAnalytics.Services
{

    public class AdService
    {
        private readonly AdDbContext _context;
        private readonly IImageStorageService _imageStorageService;
        private readonly ILogger<AdService> _logger;

        public AdService(AdDbContext context, IImageStorageService imageStorageService, ILogger<AdService> logger)
        {
            _context = context;
            _imageStorageService = imageStorageService;
            _logger = logger;
        }

        public async Task<IEnumerable<Advertisment>> GetAllAdvertisementsAsync()
        {
            return await _context.Advertisments
                                 .Include(a => a.AdData)
                                 .AsNoTracking()
                                 .ToListAsync();
        }

        public async Task<Advertisment?> GetAdvertisementByIdAsync(int id)
        {
            return await _context.Advertisments
                                 .Include(a => a.AdData)
                                 .FirstOrDefaultAsync(a => a.Id == id);
        }

        /// <summary>
        /// Creates a new Advertisment and its associated AdData, handling image uploads.
        /// </summary>
        /// <param name="request">The DTO containing Advertisment details and AdData inputs.</param>
        /// <returns>The newly created Advertisment entity.</returns>
        public async Task<Advertisment> CreateAdvertismentAsync(CreateAdvertismentRequestDto request)
        {
            // --- Input Validation ---
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }
            if (request.AdDataItems == null || !request.AdDataItems.Any())
            {
                throw new ArgumentException("At least one AdData item must be provided.", nameof(request.AdDataItems));
            }
            if (request.EndTime <= request.StartTime)
            {
                throw new ArgumentException("EndTime must be after StartTime.", nameof(request.EndTime));
            }
            if (string.IsNullOrWhiteSpace(request.SellerId))
            {
                throw new ArgumentException("SellerId is required.", nameof(request.SellerId));
            }


            // --- Create Advertisment Entity ---
            var newAdvertisment = new Advertisment
            {
                SellerId = request.SellerId,
                StartTime = request.StartTime,
                EndTime = request.EndTime,
                Views = 0, // Initialize counters
                Clicks = 0,
                // Determine IsActive based on current time and dates - or set explicitly if needed
                IsActive = DateTime.UtcNow >= request.StartTime && DateTime.UtcNow < request.EndTime,
                // AdData collection will be populated below
            };

            // --- Process AdData Items and Upload Images ---
            var adDataEntities = new List<AdData>();
            foreach (var adDataItemDto in request.AdDataItems)
            {
                string? imageUrl = null; // Default to null

                // Check if an image file was provided and is valid
                if (adDataItemDto.ImageFile != null && adDataItemDto.ImageFile.Length > 0)
                {
                    try
                    {
                        // Define a subfolder for organization (optional but recommended)
                        string subfolder = $"advertisments/{newAdvertisment.SellerId}";
                        imageUrl = await _imageStorageService.UploadImageAsync(adDataItemDto.ImageFile, subfolder);

                        if (imageUrl == null)
                        {
                            // Handle upload failure: Log it, maybe throw, or continue without image?
                            // Depending on requirements. Here, we log and continue.
                            _logger.LogWarning("Image upload failed for a new AdData item. SellerId: {SellerId}, StoreId: {StoreId}, ProductId: {ProductId}",
                                newAdvertisment.SellerId, adDataItemDto.StoreId, adDataItemDto.ProductId);
                            // Optionally: throw new Exception("Failed to upload image.");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error uploading image for a new AdData item. SellerId: {SellerId}, StoreId: {StoreId}, ProductId: {ProductId}",
                            newAdvertisment.SellerId, adDataItemDto.StoreId, adDataItemDto.ProductId);
                        // Rethrow or handle as per application requirements
                        throw new Exception("An error occurred during image upload.", ex);
                    }
                }
                else
                {
                    _logger.LogInformation("No image provided for AdData item. SellerId: {SellerId}, StoreId: {StoreId}, ProductId: {ProductId}",
                        newAdvertisment.SellerId, adDataItemDto.StoreId, adDataItemDto.ProductId);
                }


                // Create the AdData entity
                var adDataEntity = new AdData
                {
                    StoreId = adDataItemDto.StoreId,
                    ProductId = adDataItemDto.ProductId,
                    ImageUrl = imageUrl, // Assign the uploaded URL or null
                    Advertisment = newAdvertisment // Associate with the parent Advertisment
                    // EF Core will automatically set AdvertismentId when saving
                };
                adDataEntities.Add(adDataEntity);
            }

            // Add the processed AdData entities to the Advertisment's collection
            // Although setting AdData.Advertisment is often enough for EF Core,
            // explicitly adding to the collection ensures the object graph is complete in memory.
            foreach (var adData in adDataEntities)
            {
                newAdvertisment.AdData.Add(adData);
            }


            // --- Save to Database ---
            try
            {
                _context.Advertisments.Add(newAdvertisment); // Add the parent entity (EF Core tracks related AdData)
                await _context.SaveChangesAsync(); // Save changes to the database

                _logger.LogInformation("Successfully created Advertisment with Id {AdvertismentId} and {AdDataCount} AdData items.",
                    newAdvertisment.Id, newAdvertisment.AdData.Count);

                return newAdvertisment; // Return the saved entity with its generated Id
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error occurred while creating Advertisment for SellerId {SellerId}.", request.SellerId);
                // Handle potential database errors (e.g., constraints)
                throw new Exception("An error occurred while saving the advertisment to the database.", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred during Advertisment creation for SellerId {SellerId}.", request.SellerId);
                throw; // Re-throw unexpected errors
            }
        }

    }
}