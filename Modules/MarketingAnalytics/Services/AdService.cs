using Catalog.Interfaces;
using Catalog.Services;
using MarketingAnalytics.DTOs;
using MarketingAnalytics.Hubs;
using MarketingAnalytics.Interfaces;
using MarketingAnalytics.Models;
using MarketingAnalytics.Services.DTOs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualBasic;
using Store.Interface;
using Users.Interface;

namespace MarketingAnalytics.Services
{

    public class AdService : IAdService
    {
        private readonly AdDbContext _context;
        private readonly IImageStorageService _imageStorageService;
        private readonly IStoreService _storeService;
        private readonly IProductService _productService;
        private readonly IUserService _userService;
        private readonly ILogger<AdService> _logger;
        private readonly IProductCategoryService _productCategoryService;
        private readonly IHubContext<AdvertisementHub> _hubContext; // <<< ADD THIS FIELD


        public AdService(AdDbContext context, IImageStorageService imageStorageService,
                        IStoreService storeService, IProductService productService,
                        IUserService userService,
                        ILogger<AdService> logger,
                        IProductCategoryService productCategoryService,
                        IHubContext<AdvertisementHub> hubContext)

        {
            _context = context;
            _imageStorageService = imageStorageService;
            _storeService = storeService;
            _productService = productService;
            _userService = userService;
            _logger = logger;

            _productCategoryService = productCategoryService;

            _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext)); // <<< ASSIGN hubContext TO FIELD
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
            var s = await _userService.GetUserWithIdAsync(request.SellerId);
            if (s is null)
                throw new ArgumentException("SellerId is invalid.", nameof(request.SellerId));

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
                if (adDataItemDto.ProductId is not null)
                {
                    var p = await _productService.GetProductByIdAsync((int)adDataItemDto.ProductId);
                    if (p is null)
                        throw new InvalidDataException("Product does not exist");
                }
                if (adDataItemDto.StoreId is not null)
                {
                    var store = _storeService.GetStoreById((int)adDataItemDto.StoreId);
                    if (store is null)
                        throw new InvalidDataException("Product does not exist");
                }

                // Create the AdData entity
                var adDataEntity = new AdData
                {
                    StoreId = adDataItemDto.StoreId,
                    ProductId = adDataItemDto.ProductId,
                    ImageUrl = imageUrl, // Assign the uploaded URL or null
                    Advertisment = newAdvertisment, // Associate with 
                    // the parent Advertisment
                    Description = adDataItemDto.Description
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

        public async Task<Advertisment?> UpdateAdvertismentAsync(int advertismentId, UpdateAdvertismentRequestDto request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (request.EndTime <= request.StartTime)
            {
                throw new ArgumentException("EndTime must be after StartTime.", nameof(request.EndTime));
            }

            // Fetch the existing Advertisment
            var advertisment = await _context.Advertisments.FindAsync(advertismentId);

            if (advertisment == null)
            {
                _logger.LogWarning("UpdateAdvertismentAsync: Advertisment with Id {AdvertismentId} not found.", advertismentId);
                return null; // Or throw new KeyNotFoundException("Advertisment not found.");
            }

            // Update Advertisment properties
            advertisment.StartTime = request.StartTime;
            advertisment.EndTime = request.EndTime;
            advertisment.IsActive = request.IsActive ?? (DateTime.UtcNow >= request.StartTime && DateTime.UtcNow < request.EndTime);

            // Process NEW AdData items if provided
            if (request.NewAdDataItems != null && request.NewAdDataItems.Any())
            {
                // Create a list to hold the validated and processed new AdData entities
                var newAdDataEntities = new List<AdData>();
                await _context.AdData.Where(a => a.AdvertismentId == advertismentId).ExecuteDeleteAsync();

                foreach (var newItemDto in request.NewAdDataItems)
                {
                    // *** START: VALIDATE StoreId and ProductId ***
                    Store.Models.StoreModel? storeExists = null; // Use StoreModel from Store.Models
                    if (newItemDto.StoreId.HasValue)
                    {
                        // Use the injected IStoreService. Assuming GetStoreById returns StoreModel or null.
                        storeExists = _storeService.GetStoreById(newItemDto.StoreId.Value);
                        // If you have an async version like GetStoreByIdAsync, use await:
                        // storeExists = await _storeService.GetStoreByIdAsync(newItemDto.StoreId.Value);
                        if (storeExists == null)
                        {
                            _logger.LogWarning("Validation failed: Store with ID {StoreId} provided in NewAdDataItems not found.", newItemDto.StoreId.Value);
                            throw new ArgumentException($"Invalid StoreId provided in new AdData: {newItemDto.StoreId.Value}. Store not found.");
                        }
                    }

                    Catalog.Models.Product? productExists = null; // Use Product from Catalog.Models
                    if (newItemDto.ProductId.HasValue)
                    {
                        // Use the injected IProductService
                        productExists = await _productService.GetProductByIdAsync(newItemDto.ProductId.Value);
                        if (productExists == null)
                        {
                            _logger.LogWarning("Validation failed: Product with ID {ProductId} provided in NewAdDataItems not found.", newItemDto.ProductId.Value);
                            throw new ArgumentException($"Invalid ProductId provided in new AdData: {newItemDto.ProductId.Value}. Product not found.");
                        }
                        // Optional further check: Does product belong to the *correct* store if StoreId is also specified?
                        if (storeExists != null && productExists.StoreId != storeExists.id) // Check against storeExists.id
                        {
                            _logger.LogWarning("Validation failed: Product with ID {ProductId} does not belong to Store ID {StoreId}.", newItemDto.ProductId.Value, newItemDto.StoreId.Value);
                            throw new ArgumentException($"Product {newItemDto.ProductId.Value} does not belong to Store {newItemDto.StoreId.Value}.");
                        }
                    }
                    // *** END: VALIDATE StoreId and ProductId ***

                    // --- Proceed only if validation passed ---

                    string? imageUrl = null;
                    if (newItemDto.ImageFile != null && newItemDto.ImageFile.Length > 0)
                    {
                        try
                        {
                            string subfolder = $"advertisments/{advertisment.SellerId}/{advertismentId}/{Guid.NewGuid()}"; // Use advertisementId consistently
                            imageUrl = await _imageStorageService.UploadImageAsync(newItemDto.ImageFile, subfolder);
                            if (imageUrl == null)
                            {
                                _logger.LogWarning("Image upload failed for new AdData during update. AdvertismentId: {AdvertismentId}, StoreId: {StoreId}, ProductId: {ProductId}",
                                  advertismentId, newItemDto.StoreId, newItemDto.ProductId);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error uploading image for new AdData during update. AdvertismentId: {AdvertismentId}, StoreId: {StoreId}, ProductId: {ProductId}",
                                advertismentId, newItemDto.StoreId, newItemDto.ProductId);
                            throw new Exception("An error occurred during image upload for new AdData.", ex);
                        }
                    }

                    // Create the AdData entity
                    var newAdData = new AdData
                    {
                        StoreId = newItemDto.StoreId,
                        ProductId = newItemDto.ProductId,
                        ImageUrl = imageUrl,
                        AdvertismentId = advertismentId, // Explicitly set FK
                        Description = newItemDto.Description
                    };
                    newAdDataEntities.Add(newAdData); // Add to temporary list
                }

                // Add all validated and processed entities to the context *after* the loop
                _context.AdData.AddRange(newAdDataEntities);
            }

            // --- Save Changes ---
            try
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation("Successfully updated Advertisment with Id {AdvertismentId}.", advertismentId);

                var updatedEntity = await _context.Advertisments.Include(a => a.AdData).AsNoTracking().FirstOrDefaultAsync(a => a.Id == advertismentId);
                return updatedEntity;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogError(ex, "Concurrency error occurred while updating Advertisment {AdvertismentId}.", advertismentId);
                throw new Exception("The advertisment data has been modified since you loaded it. Please refresh and try again.", ex);
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error occurred while updating Advertisment {AdvertismentId}.", advertismentId);
                throw new Exception("An error occurred while saving the advertisment updates to the database.", ex);
            }
            catch (Exception ex) // Catches ArgumentExceptions from validation
            {
                _logger.LogError(ex, "An error occurred during Advertisment update for Id {AdvertismentId}.", advertismentId);
                throw; // Re-throw to be caught by the controller
            }
        }


        // --- DELETE Advertisment ---
        public async Task<bool> DeleteAdvertismentAsync(int advertismentId)
        {
            // Important: Include related AdData to delete associated images
            var advertisment = await _context.Advertisments
                                            .Include(a => a.AdData) // Load related AdData
                                            .FirstOrDefaultAsync(a => a.Id == advertismentId);

            if (advertisment == null)
            {
                _logger.LogWarning("DeleteAdvertismentAsync: Advertisment with Id {AdvertismentId} not found.", advertismentId);
                return false;
            }
            int idToDelete = advertisment.Id;

            // --- Delete associated images first ---
            var imageDeletionTasks = new List<Task>();
            foreach (var adData in advertisment.AdData)
            {
                if (!string.IsNullOrEmpty(adData.ImageUrl))
                {
                    // Launch deletion task, don't await immediately to parallelize
                    imageDeletionTasks.Add(DeleteImageInternalAsync(adData.ImageUrl, $"AdData Id {adData.Id} associated with Advertisment Id {advertismentId}"));
                }
            }
            // Wait for all image deletion attempts to complete
            await Task.WhenAll(imageDeletionTasks);
            // Note: We proceed with DB deletion even if some image deletions failed (they are logged in DeleteImageInternalAsync)

            try
            {
                // Remove the parent Advertisment. EF Core cascade delete (if configured, which is default for required relationships)
                // should handle removing the AdData rows automatically.
                _context.Advertisments.Remove(advertisment);
                var result = await _context.SaveChangesAsync();
                _logger.LogInformation("Successfully deleted Advertisment with Id {AdvertismentId} and its associated AdData.", advertismentId);
                if (result > 0)
                {
                    // --- Send SignalR ---
                    try
                    {
                        await _hubContext.Clients.All.SendAsync("AdvertisementDeleted", idToDelete); // <<< Send ID
                    }
                    catch (Exception ex) { _logger.LogError(ex, "Error sending SignalR delete notification for Ad {AdId}", idToDelete); }
                    // --------------------
                    return true;
                }
                return false;
            }
            catch (DbUpdateException ex)
            {
                // This might happen if cascade delete is not configured or fails
                _logger.LogError(ex, "Database error occurred while deleting Advertisment {AdvertismentId}.", advertismentId);
                throw new Exception("An error occurred while deleting the advertisment from the database.", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred during Advertisment deletion for Id {AdvertismentId}.", advertismentId);
                throw;
            }
        }

        // --- UPDATE AdData ---
        public async Task<AdData?> UpdateAdDataAsync(int adDataId, UpdateAdDataRequestDto request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            var adData = await _context.AdData.FindAsync(adDataId);

            if (adData == null)
            {
                _logger.LogWarning("UpdateAdDataAsync: AdData with Id {AdDataId} not found.", adDataId);
                return null; // Or throw new KeyNotFoundException("AdData not found.");
            }

            // --- Handle Image Update/Removal ---
            string? oldImageUrl = adData.ImageUrl;
            bool imageChanged = false;

            if (request.RemoveCurrentImage)
            {
                if (!string.IsNullOrEmpty(oldImageUrl))
                {
                    adData.ImageUrl = null; // Remove URL from entity
                    imageChanged = true;
                    // Deletion of old image happens after SaveChangesAsync succeeds
                }
            }
            else if (request.ImageFile != null && request.ImageFile.Length > 0)
            {
                // Upload new image
                string? newImageUrl = null;
                try
                {
                    // Fetch related Advertisment info for subfolder naming consistency
                    var advertisment = await _context.Advertisments
                        .Where(adv => adv.Id == adData.AdvertismentId)
                        .Select(adv => new { adv.SellerId, adv.Id })
                        .FirstOrDefaultAsync();

                    if (advertisment == null)
                    {
                        // Should not happen due to FK constraint, but good practice to check
                        _logger.LogError("Could not find parent Advertisment {AdvertismentId} for AdData {AdDataId} during update.", adData.AdvertismentId, adDataId);
                        throw new InvalidOperationException("Parent advertisment not found for AdData.");
                    }

                    string subfolder = $"advertisments/{advertisment.SellerId}/{advertisment.Id}/{Guid.NewGuid()}";
                    newImageUrl = await _imageStorageService.UploadImageAsync(request.ImageFile, subfolder);

                    if (newImageUrl != null)
                    {
                        adData.ImageUrl = newImageUrl; // Set new URL on entity
                        imageChanged = true;
                        // Deletion of old image happens after SaveChangesAsync succeeds
                    }
                    else
                    {
                        _logger.LogWarning("New image upload failed for AdData Id {AdDataId}. Existing image (if any) will be kept.", adDataId);
                        // Decide if failure to upload new image should stop the update. Here we let other field updates proceed.
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error uploading new image for AdData Id {AdDataId}.", adDataId);
                    // Let other field updates proceed, or re-throw if image update is critical
                    throw new Exception("An error occurred while uploading the new image.", ex);
                }
            }
            // --- Validation for StoreId/ProductId ---
            Catalog.Models.Product? product = null; // Store the fetched product
            Store.Models.StoreModel? store = null;   // Store the fetched store

            if (request.ProductId.HasValue) // Check ProductId validity first
            {
                product = await _productService.GetProductByIdAsync(request.ProductId.Value);
                if (product is null)
                {
                    _logger.LogWarning("UpdateAdDataAsync: Product with ID {ProductId} not found.", request.ProductId.Value);
                    throw new ArgumentException($"Product with ID {request.ProductId.Value} does not exist");
                }
                if (!product.IsActive) // Optional: check if product is active
                {
                    _logger.LogWarning("UpdateAdDataAsync: Product with ID {ProductId} is not active.", request.ProductId.Value);
                    // Decide if inactive products can be linked, throw if not:
                    // throw new ArgumentException($"Product with ID {request.ProductId.Value} is not active.");
                }
            }

            if (request.StoreId.HasValue) // Check StoreId validity
            {
                // Assuming sync GetStoreById or adapt if async exists
                store = _storeService.GetStoreById(request.StoreId.Value);
                // var store = await _storeService.GetStoreByIdAsync(request.StoreId.Value); // Use if async version available
                if (store is null)
                {
                    _logger.LogWarning("UpdateAdDataAsync: Store with ID {StoreId} not found.", request.StoreId.Value);
                    throw new ArgumentException($"Store with ID {request.StoreId.Value} does not exist");
                }
                if (!store.isActive) // Optional: check if store is active
                {
                    _logger.LogWarning("UpdateAdDataAsync: Store with ID {StoreId} is not active.", request.StoreId.Value);
                    // Decide if inactive stores can be linked, throw if not:
                    // throw new ArgumentException($"Store with ID {request.StoreId.Value} is not active.");
                }
            }

            // *** ADDED CHECK: If both Product and Store are specified, verify product belongs to store ***
            if (product != null && store != null) // Checks if both IDs were provided *and* entities were found
            {
                if (product.StoreId != store.id)
                {
                    _logger.LogWarning("Validation failed: Product ID {ProductId} (Store: {ProductStoreId}) does not belong to the specified Store ID {RequestedStoreId}.",
                        product.Id, product.StoreId, store.id);
                    throw new ArgumentException($"Product {product.Id} ('{product.Name}') does not belong to Store {store.id} ('{store.name}').");
                }
            }
            // *** END OF ADDED CHECK ***
            // Update other AdData properties
            adData.StoreId = request.StoreId;
            adData.ProductId = request.ProductId;
            adData.Description = request.Description;

            try
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation("Successfully updated AdData with Id {AdDataId}.", adDataId);

                // --- Delete old image AFTER successful save ---
                if (imageChanged && !string.IsNullOrEmpty(oldImageUrl))
                {
                    _logger.LogInformation("Attempting to delete old image '{OldImageUrl}' for AdData Id {AdDataId}.", oldImageUrl, adDataId);
                    await DeleteImageInternalAsync(oldImageUrl, $"AdData Id {adDataId}");
                }

                return adData;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogError(ex, "Concurrency error occurred while updating AdData {AdDataId}.", adDataId);
                throw new Exception("The AdData has been modified since you loaded it. Please refresh and try again.", ex);
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error occurred while updating AdData {AdDataId}.", adDataId);
                // If image was uploaded but DB save failed, we might have an orphaned image.
                // Consider adding logic to delete the newly uploaded image if the transaction fails.
                // This often requires a more complex transaction/compensation pattern.
                throw new Exception("An error occurred while saving the AdData updates to the database.", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred during AdData update for Id {AdDataId}.", adDataId);
                throw;
            }
        }


        // --- DELETE AdData ---
        public async Task<bool> DeleteAdDataAsync(int adDataId)
        {
            var adData = await _context.AdData.FindAsync(adDataId);

            if (adData == null)
            {
                _logger.LogWarning("DeleteAdDataAsync: AdData with Id {AdDataId} not found.", adDataId);
                return false;
            }

            string? imageUrlToDelete = adData.ImageUrl; // Store URL before removing entity
            int parentAdId = adData.AdvertismentId;

            // Check if this AdData is the *last* one associated with its Advertisment
            var siblingCount = await _context.AdData.CountAsync(ad => ad.AdvertismentId == adData.AdvertismentId && ad.Id != adDataId);
            if (siblingCount == 0)
            {
                // Prevent deleting the last AdData if business rules require at least one per Advertisment
                _logger.LogWarning("Attempted to delete the last AdData (Id: {AdDataId}) for Advertisment Id {AdvertismentId}. Deletion aborted.", adDataId, adData.AdvertismentId);
                // Consider throwing a specific exception or returning a custom result object instead of just false
                // throw new InvalidOperationException("Cannot delete the last AdData associated with an Advertisment.");
                return false; // Or indicate failure more specifically
            }


            try
            {
                _context.AdData.Remove(adData);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Successfully deleted AdData with Id {AdDataId}.", adDataId);

                // --- Delete associated image AFTER successful DB deletion ---
                if (!string.IsNullOrEmpty(imageUrlToDelete))
                {
                    _logger.LogInformation("Attempting to delete image '{ImageUrlToDelete}' for deleted AdData Id {AdDataId}.", imageUrlToDelete, adDataId);
                    await DeleteImageInternalAsync(imageUrlToDelete, $"AdData Id {adDataId}");
                }

                return true;
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error occurred while deleting AdData {AdDataId}.", adDataId);
                throw new Exception("An error occurred while deleting the AdData from the database.", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred during AdData deletion for Id {AdDataId}.", adDataId);
                throw;
            }
        }

        // --- Helper method for image deletion with logging ---
        private async Task DeleteImageInternalAsync(string fileUrl, string contextDescription)
        {
        }

        public async Task<UserActivity> CreateUserActivityAsync(UserActivity userActivity)
        {
            if (userActivity == null)
            {
                _logger.LogWarning("Pokušaj kreiranja null UserActivity objekta.");
                throw new ArgumentNullException(nameof(userActivity));
            }

            userActivity.TimeStamp = DateTime.Now;

            if (string.IsNullOrWhiteSpace(userActivity.UserId))
            {
                _logger.LogWarning("Pokušaj kreiranja UserActivity bez UserId. {@UserActivityInput}", new { userActivity.ProductCategoryId, userActivity.InteractionType });
                throw new ArgumentException("UserId ne može biti prazan.", nameof(userActivity.UserId));
            }

            var category = _productCategoryService.GetCategoryByIdAsync(userActivity.ProductCategoryId);

            if (category == null)
            {
                _logger.LogWarning("Pokušaj kreiranja UserActivity sa nevalidnim ProductCategoryId: {ProductCategoryId}", userActivity.ProductCategoryId);
                throw new ArgumentException("ProductCategoryId mora biti validan.", nameof(userActivity.ProductCategoryId));
            }

            var user = _userService.GetUserWithIdAsync(userActivity.UserId);

            if (category == null)
            {
                _logger.LogWarning("Pokušaj kreiranja UserActivity sa nevalidnim UserId: {UserId}", userActivity.UserId);
                throw new ArgumentException("UserId mora biti validan.", nameof(userActivity.UserId));
            }

            try
            {
                _context.UserActivities.Add(userActivity);
                int recordsAffected = await _context.SaveChangesAsync();

                if (recordsAffected > 0)
                {
                    _logger.LogInformation("UserActivity (Id: {ActivityId}) uspešno kreiran za korisnika {UserId}.", userActivity.Id, userActivity.UserId);
                    return userActivity;
                }
                else
                {
                    // Ovo je neočekivano ako Add() nije bacio izuzetak i SaveChangesAsync nije bacio izuzetak.
                    // Može ukazivati na problem sa EF Core konfiguracijom ili vrlo specifičan scenario konkurentnosti.
                    _logger.LogError("UserActivity za korisnika {UserId} je dodat u context, ali SaveChangesAsync nije napravio izmjene i nije bacio izuzetak. {@UserActivity}", userActivity.UserId, userActivity);
                    throw new InvalidOperationException($"Nije bilo moguće sačuvati UserActivity za korisnika {userActivity.UserId}, nijedan zapis nije izmijenjen.");
                }
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "DbUpdateException prilikom čuvanja UserActivity za korisnika {UserId}. {@UserActivity}", userActivity.UserId, userActivity);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Neočekivana greška prilikom kreiranja UserActivity za korisnika {UserId}. {@UserActivity}", userActivity.UserId, userActivity);
                throw;
            }
        }

        public async Task<ICollection<UserActivity>> GetUserActivitiesByUserId(string id)
        {
            return await _context.UserActivities
                            .Where(u => u.UserId == id)
                            .AsNoTracking()
                            .ToListAsync();
        }


        public async Task<Clicks?> RecordClickAsync(AdStatsDto clickDto)
        {
            // Provjera postoji li oglas
            var advertisementExists = await _context.Advertisments.AnyAsync(a => a.Id == clickDto.AdvertisementId);
            if (!advertisementExists)
            {
                _logger.LogWarning("Pokušaj bilježenja klika za nepostojeći oglas ID: {AdvertisementId}", clickDto.AdvertisementId);
                return null;
            }

            var newClick = new Clicks
            {
                UserId = clickDto.UserId,
                AdvertismentId = clickDto.AdvertisementId,
                Timestamp = DateTime.Now
            };

            _context.Clicks.Add(newClick);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Zabilježen klik ID: {ClickId} za korisnika: {UserId} na oglas ID: {AdvertisementId}",
                newClick.Id, newClick.UserId, newClick.AdvertismentId);

            // Opcionalno: Učitati navigacijsko svojstvo ako je potrebno za povratnu vrijednost
            // await _context.Entry(newClick).Reference(c => c.Advertisement).LoadAsync();

            return newClick;
        }

        public async Task<Views?> RecordViewAsync(AdStatsDto viewDto)
        {
            // Provjera postoji li oglas
            var advertisementExists = await _context.Advertisments.AnyAsync(a => a.Id == viewDto.AdvertisementId);
            if (!advertisementExists)
            {
                _logger.LogWarning("Pokušaj bilježenja pregleda za nepostojeći oglas ID: {AdvertisementId}", viewDto.AdvertisementId);
                return null;
            }

            var newView = new Views
            {
                UserId = viewDto.UserId,
                AdvertismentId = viewDto.AdvertisementId,
                Timestamp = DateTime.Now
            };

            _context.Views.Add(newView);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Zabilježen pregled ID: {ViewId} za korisnika: {UserId} na oglas ID: {AdvertisementId}",
                newView.Id, newView.UserId, newView.AdvertismentId);

            return newView;
        }

        public async Task<Conversions?> RecordConversionAsync(AdStatsDto conversionDto)
        {
            // Provjera postoji li oglas
            var advertisementExists = await _context.Advertisments.AnyAsync(a => a.Id == conversionDto.AdvertisementId);
            if (!advertisementExists)
            {
                _logger.LogWarning("Pokušaj bilježenja conversiona za nepostojeći oglas ID: {AdvertisementId}", conversionDto.AdvertisementId);
                return null;
            }

            var newConversion = new Conversions
            {
                UserId = conversionDto.UserId,
                AdvertismentId = conversionDto.AdvertisementId,
                Timestamp = DateTime.Now
            };

            _context.Conversions.Add(newConversion);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Zabilježen klik ID: {ClickId} za korisnika: {UserId} na oglas ID: {AdvertisementId}",
                newConversion.Id, newConversion.UserId, newConversion.AdvertismentId);

            return newConversion;
        }

    }
}