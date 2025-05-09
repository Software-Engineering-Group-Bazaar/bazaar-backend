using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MarketingAnalytics.Dtos;
using MarketingAnalytics.DTOs;
using MarketingAnalytics.Interfaces;
using MarketingAnalytics.Models;
using MarketingAnalytics.Services.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace MarketingAnalytics.Controllers
{
    // [Authorize] // Applied at the controller level, uncomment if needed globally first
    [ApiController]
    [Authorize(Roles = "Admin")] // Ensures only users with the "Admin" role can access these endpoints
    [Route("api/[controller]")]
    [Produces("application/json")] // Standard practice for APIs
    public class AdminAnalyticsController : ControllerBase
    {
        private readonly IAdService _adService; // Use the provided interface name
        private readonly ILogger<AdminAnalyticsController> _logger;

        public AdminAnalyticsController(IAdService adService, ILogger<AdminAnalyticsController> logger)
        {
            _adService = adService ?? throw new ArgumentNullException(nameof(adService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        // --- Advertisement Endpoints ---

        /// <summary>
        /// Gets all advertisements.
        /// </summary>
        /// <returns>A list of advertisements.</returns>
        [HttpGet("advertisements")] // GET api/adminanalytics/advertisements
        [ProducesResponseType(typeof(IEnumerable<AdvertismentDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<AdvertismentDto>>> GetAllAdvertisements()
        {
            try
            {
                var advertisements = await _adService.GetAllAdvertisementsAsync();
                var dto = advertisements.Select(advertisement => new AdvertismentDto
                {
                    Id = advertisement.Id,
                    SellerId = advertisement.SellerId,
                    StartTime = advertisement.StartTime,
                    EndTime = advertisement.EndTime,
                    IsActive = advertisement.IsActive,
                    Views = advertisement.Views,
                    ViewPrice = advertisement.ViewPrice,
                    Clicks = advertisement.Clicks,
                    ClickPrice = advertisement.ClickPrice,
                    Conversions = advertisement.Conversions,
                    ConversionPrice = advertisement.ConversionPrice,
                    AdType = advertisement.AdType.ToString(),
                    Triggers = advertisement.Triggers,
                    AdData = advertisement.AdData.Select(ad => new AdDataDto
                    {
                        Id = ad.Id,
                        StoreId = ad.StoreId,
                        ImageUrl = ad.ImageUrl,
                        Description = ad.Description,
                        ProductId = ad.ProductId
                    }).ToList()
                });
                return Ok(dto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all advertisements.");
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while retrieving advertisements.");
            }
        }

        /// <summary>
        /// Gets a specific advertisement by its ID.
        /// </summary>
        /// <param name="id">The ID of the advertisement to retrieve.</param>
        /// <returns>The requested advertisement.</returns>
        [HttpGet("advertisements/{id:int}", Name = "GetAdvertisementById")] // GET api/adminanalytics/advertisements/5
        [ProducesResponseType(typeof(AdvertismentDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<AdvertismentDto>> GetAdvertisementById(int id)
        {
            try
            {
                var advertisement = await _adService.GetAdvertisementByIdAsync(id);
                if (advertisement == null)
                {
                    _logger.LogWarning("GetAdvertisementById: Advertisement with ID {AdvertisementId} not found.", id);
                    return NotFound($"Advertisement with ID {id} not found.");
                }
                var dto = new AdvertismentDto
                {
                    Id = advertisement.Id,
                    SellerId = advertisement.SellerId,
                    StartTime = advertisement.StartTime,
                    EndTime = advertisement.EndTime,
                    IsActive = advertisement.IsActive,
                    Views = advertisement.Views,
                    ViewPrice = advertisement.ViewPrice,
                    Clicks = advertisement.Clicks,
                    ClickPrice = advertisement.ClickPrice,
                    Conversions = advertisement.Conversions,
                    ConversionPrice = advertisement.ConversionPrice,
                    AdType = advertisement.AdType.ToString(),
                    Triggers = advertisement.Triggers,
                    AdData = advertisement.AdData.Select(ad => new AdDataDto
                    {
                        Id = ad.Id,
                        StoreId = ad.StoreId,
                        ImageUrl = ad.ImageUrl,
                        Description = ad.Description,
                        ProductId = ad.ProductId
                    }).ToList()
                };

                return Ok(dto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving advertisement with ID {AdvertisementId}.", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while retrieving the advertisement.");
            }
        }

        /// <summary>
        /// Creates a new advertisement with associated ad data (including optional images).
        /// </summary>
        /// <param name="request">The advertisement creation request data (sent as form-data).</param>
        /// <returns>The newly created advertisement.</returns>
        [HttpPost("advertisements")] // POST api/adminanalytics/advertisements
        [Consumes("multipart/form-data")] // Specify consumption type due to IFormFile
        [ProducesResponseType(typeof(AdvertismentDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<AdvertismentDto>> CreateAdvertisement([FromForm] CreateAdvertismentRequestDto request)
        {
            // Basic check, model state validation handles more via [ApiController]
            if (request?.AdDataItems == null)
            {
                return BadRequest("At least one AdData item must be provided.");
            }

            try
            {
                var createdAdvertisement = await _adService.CreateAdvertismentAsync(request);
                // Return 201 Created with a Location header pointing to the new resource
                var dto = new AdvertismentDto
                {
                    Id = createdAdvertisement.Id,
                    SellerId = createdAdvertisement.SellerId,
                    StartTime = createdAdvertisement.StartTime,
                    EndTime = createdAdvertisement.EndTime,
                    IsActive = createdAdvertisement.IsActive,
                    Views = createdAdvertisement.Views,
                    ViewPrice = createdAdvertisement.ViewPrice,
                    Clicks = createdAdvertisement.Clicks,
                    ClickPrice = createdAdvertisement.ClickPrice,
                    Conversions = createdAdvertisement.Conversions,
                    ConversionPrice = createdAdvertisement.ConversionPrice,
                    AdType = createdAdvertisement.AdType.ToString(),
                    Triggers = createdAdvertisement.Triggers,
                    AdData = createdAdvertisement.AdData.Select(ad => new AdDataDto
                    {
                        Id = ad.Id,
                        StoreId = ad.StoreId,
                        ImageUrl = ad.ImageUrl,
                        Description = ad.Description,
                        ProductId = ad.ProductId
                    }).ToList()
                };
                return CreatedAtRoute("GetAdvertisementById", new { id = createdAdvertisement.Id }, dto);
            }
            catch (ArgumentException argEx) // Catch specific validation errors from service
            {
                _logger.LogWarning(argEx, "Invalid argument provided for creating advertisement.");
                // Return the specific error message from the service layer
                return BadRequest(argEx.Message);
            }
            catch (Exception ex) when (ex.InnerException is ArgumentException argExInner) // Handle nested ArgumentException
            {
                _logger.LogWarning(argExInner, "Invalid argument provided for creating advertisement (inner exception).");
                return BadRequest(argExInner.Message);
            }
            catch (Exception ex) // Catch broader exceptions (DB errors, image upload errors)
            {
                _logger.LogError(ex, "Error creating advertisement.");
                // Avoid exposing raw exception details unless intended for development
                return StatusCode(StatusCodes.Status500InternalServerError, $"An internal error occurred: {ex.Message}");
            }
        }

        /// <summary>
        /// Updates an existing advertisement. Can add new AdData items.
        /// </summary>
        /// <param name="advertismentId">The ID of the advertisement to update.</param>
        /// <param name="request">The update request data (sent as form-data if adding AdData with images).</param>
        /// <returns>The updated advertisement.</returns>
        [HttpPut("advertisements/{advertismentId:int}")] // PUT api/adminanalytics/advertisements/5
        [Consumes("multipart/form-data")] // Needed if NewAdDataItems can contain files
        [ProducesResponseType(typeof(AdvertismentDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<AdvertismentDto>> UpdateAdvertisement(int advertismentId, [FromForm] UpdateAdvertismentRequestDto request)
        {
            try
            {
                var updatedAdvertisement = await _adService.UpdateAdvertismentAsync(advertismentId, request);
                if (updatedAdvertisement == null)
                {
                    _logger.LogWarning("UpdateAdvertisement: Advertisement with ID {AdvertisementId} not found.", advertismentId);
                    return NotFound($"Advertisement with ID {advertismentId} not found.");
                }
                var dto = new AdvertismentDto
                {
                    Id = updatedAdvertisement.Id,
                    SellerId = updatedAdvertisement.SellerId,
                    StartTime = updatedAdvertisement.StartTime,
                    EndTime = updatedAdvertisement.EndTime,
                    IsActive = updatedAdvertisement.IsActive,
                    Views = updatedAdvertisement.Views,
                    ViewPrice = updatedAdvertisement.ViewPrice,
                    Clicks = updatedAdvertisement.Clicks,
                    ClickPrice = updatedAdvertisement.ClickPrice,
                    Conversions = updatedAdvertisement.Conversions,
                    ConversionPrice = updatedAdvertisement.ConversionPrice,
                    AdType = updatedAdvertisement.AdType.ToString(),
                    Triggers = updatedAdvertisement.Triggers,
                    AdData = updatedAdvertisement.AdData.Select(ad => new AdDataDto
                    {
                        Id = ad.Id,
                        StoreId = ad.StoreId,
                        ImageUrl = ad.ImageUrl,
                        Description = ad.Description,
                        ProductId = ad.ProductId
                    }).ToList()
                };
                return Ok(dto); // Return the updated entity
            }
            catch (ArgumentException argEx)
            {
                _logger.LogWarning(argEx, "Invalid argument provided for updating advertisement {AdvertisementId}.", advertismentId);
                return BadRequest(argEx.Message);
            }
            catch (KeyNotFoundException) // Or handle if service throws this on not found
            {
                _logger.LogWarning("UpdateAdvertisement: Advertisement with ID {AdvertisementId} not found (KeyNotFoundException).", advertismentId);
                return NotFound($"Advertisement with ID {advertismentId} not found.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating advertisement {AdvertisementId}.", advertismentId);
                return StatusCode(StatusCodes.Status500InternalServerError, $"An internal error occurred: {ex.Message}");
            }
        }

        /// <summary>
        /// Deletes an advertisement and all its associated ad data.
        /// </summary>
        /// <param name="advertismentId">The ID of the advertisement to delete.</param>
        /// <returns>No content if successful.</returns>
        [HttpDelete("advertisements/{advertismentId:int}")] // DELETE api/adminanalytics/advertisements/5
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeleteAdvertisement(int advertismentId)
        {
            try
            {
                var deleted = await _adService.DeleteAdvertismentAsync(advertismentId);
                if (!deleted)
                {
                    _logger.LogWarning("DeleteAdvertisement: Advertisement with ID {AdvertisementId} not found or failed to delete.", advertismentId);
                    return NotFound($"Advertisement with ID {advertismentId} not found or could not be deleted.");
                }
                return NoContent(); // Standard success response for DELETE
            }
            catch (Exception ex) // Catch potential exceptions from service (e.g., DB error during cascade)
            {
                _logger.LogError(ex, "Error deleting advertisement {AdvertisementId}.", advertismentId);
                return StatusCode(StatusCodes.Status500InternalServerError, $"An internal error occurred: {ex.Message}");
            }
        }

        // --- AdData Endpoints ---

        /// <summary>
        /// Updates a specific AdData item (including potentially changing its image).
        /// </summary>
        /// <param name="adDataId">The ID of the AdData item to update.</param>
        /// <param name="request">The AdData update request data (sent as form-data).</param>
        /// <returns>The updated AdData item.</returns>
        [HttpPut("data/{adDataId:int}")] // PUT api/adminanalytics/data/10
        [Consumes("multipart/form-data")] // Needed as DTO contains IFormFile
        [ProducesResponseType(typeof(AdDataDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<AdDataDto>> UpdateAdData(int adDataId, [FromForm] UpdateAdDataRequestDto request)
        {
            try
            {
                var updatedAdData = await _adService.UpdateAdDataAsync(adDataId, request);
                if (updatedAdData == null)
                {
                    _logger.LogWarning("UpdateAdData: AdData with ID {AdDataId} not found.", adDataId);
                    return NotFound($"AdData with ID {adDataId} not found.");
                }
                var dto = new AdDataDto
                {
                    Id = updatedAdData.Id,
                    Description = updatedAdData.Description,
                    StoreId = updatedAdData.StoreId,
                    ProductId = updatedAdData.ProductId,
                    ImageUrl = updatedAdData.ImageUrl
                };
                return Ok(dto);
            }
            catch (ArgumentException argEx)
            {
                _logger.LogWarning(argEx, "Invalid argument provided for updating AdData {AdDataId}.", adDataId);
                return BadRequest(argEx.Message);
            }
            catch (KeyNotFoundException)
            {
                _logger.LogWarning("UpdateAdData: AdData with ID {AdDataId} not found (KeyNotFoundException).", adDataId);
                return NotFound($"AdData with ID {adDataId} not found.");
            }
            catch (InvalidOperationException ioEx) // Catch specific operational errors from service
            {
                _logger.LogWarning(ioEx, "Operation error while updating AdData {AdDataId}: {ErrorMessage}", adDataId, ioEx.Message);
                return BadRequest(ioEx.Message); // Likely a business rule or data integrity issue
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating AdData {AdDataId}.", adDataId);
                return StatusCode(StatusCodes.Status500InternalServerError, $"An internal error occurred: {ex.Message}");
            }
        }

        /// <summary>
        /// Deletes a specific AdData item.
        /// </summary>
        /// <param name="adDataId">The ID of the AdData item to delete.</param>
        /// <returns>No content if successful.</returns>
        [HttpDelete("data/{adDataId:int}")] // DELETE api/adminanalytics/data/10
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)] // If deletion fails due to rules (e.g., last item)
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeleteAdData(int adDataId)
        {
            try
            {
                var deleted = await _adService.DeleteAdDataAsync(adDataId);
                if (!deleted)
                {
                    // The service might return false if not found OR if deleting the last item.
                    // Returning 404 here might be slightly ambiguous if the actual reason was the "last item" rule.
                    // Consider modifying the service to throw a specific exception for the "last item" rule
                    // which could then be caught here and returned as a 400 Bad Request.
                    _logger.LogWarning("DeleteAdData: AdData with ID {AdDataId} not found or failed to delete (possibly last item).", adDataId);
                    return NotFound($"AdData with ID {adDataId} not found or could not be deleted.");
                    // Alternative if service throws InvalidOperationException for last item:
                    // return BadRequest("Cannot delete the last AdData item associated with an advertisement.");
                }
                return NoContent();
            }
            catch (InvalidOperationException ioEx) // Catch specific rule violation from service
            {
                _logger.LogWarning(ioEx, "Business rule violation deleting AdData {AdDataId}.", adDataId);
                return BadRequest(ioEx.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting AdData {AdDataId}.", adDataId);
                return StatusCode(StatusCodes.Status500InternalServerError, $"An internal error occurred: {ex.Message}");
            }
        }

        [HttpGet("advertisement/{advertismentId:int}/clicks")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ICollection<DateTime>))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetClickTimestamps(
        [FromRoute] int advertismentId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to)
        {
            _logger.LogInformation("Pokušaj dohvatanja vremenskih pečata klikova za oglas ID: {AdvertismentId}, od: {FromDate}, do: {ToDate}",
                advertismentId,
                from?.ToString("o") ?? "N/A",
                to?.ToString("o") ?? "N/A");

            // Osnovna validacija ulaznih parametara
            if (advertismentId <= 0)
            {
                _logger.LogWarning("Nevalidan advertismentId ({AdvertismentId}) prosleđen.", advertismentId);
                return BadRequest("ID oglasa mora biti pozitivan broj.");
            }

            // Validacija opsega datuma (ako su oba zadata)
            if (from.HasValue && to.HasValue && from.Value > to.Value)
            {
                _logger.LogWarning("Nevalidan opseg datuma: 'from' ({FromDate}) je posle 'to' ({ToDate}) za oglas ID: {AdvertismentId}.",
                    from.Value, to.Value, advertismentId);
                return BadRequest("Početni datum ne može biti posle krajnjeg datuma.");
            }

            try
            {
                var timestamps = await _adService.GetClicksTimestampsAsync(advertismentId, from, to);

                return Ok(timestamps);
            }
            // Specifični izuzeci iz servisa ako ih ima i želite drugačije da ih tretirate
            // catch (AdvertisementNotFoundException ex) // Primer custom izuzetka
            // {
            //     _logger.LogWarning(ex, "Oglas nije pronađen prilikom dohvatanja klikova za ID: {AdvertismentId}", advertismentId);
            //     return NotFound(ex.Message);
            // }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Greška prilikom dohvatanja vremenskih pečata klikova za oglas ID: {AdvertismentId}.", advertismentId);
                return StatusCode(StatusCodes.Status500InternalServerError, "Dogodila se greška na serveru prilikom dohvatanja podataka o klikovima.");
            }
        }

        [HttpGet("advertisement/{advertismentId:int}/views")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ICollection<DateTime>))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetViewTimestamps(
        [FromRoute] int advertismentId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to)
        {
            _logger.LogInformation("Pokušaj dohvatanja vremenskih pečata klikova za oglas ID: {AdvertismentId}, od: {FromDate}, do: {ToDate}",
                advertismentId,
                from?.ToString("o") ?? "N/A",
                to?.ToString("o") ?? "N/A");

            // Osnovna validacija ulaznih parametara
            if (advertismentId <= 0)
            {
                _logger.LogWarning("Nevalidan advertismentId ({AdvertismentId}) prosleđen.", advertismentId);
                return BadRequest("ID oglasa mora biti pozitivan broj.");
            }

            // Validacija opsega datuma (ako su oba zadata)
            if (from.HasValue && to.HasValue && from.Value > to.Value)
            {
                _logger.LogWarning("Nevalidan opseg datuma: 'from' ({FromDate}) je posle 'to' ({ToDate}) za oglas ID: {AdvertismentId}.",
                    from.Value, to.Value, advertismentId);
                return BadRequest("Početni datum ne može biti posle krajnjeg datuma.");
            }

            try
            {
                var timestamps = await _adService.GetViewsTimestampsAsync(advertismentId, from, to);

                return Ok(timestamps);
            }
            // Specifični izuzeci iz servisa ako ih ima i želite drugačije da ih tretirate
            // catch (AdvertisementNotFoundException ex) // Primer custom izuzetka
            // {
            //     _logger.LogWarning(ex, "Oglas nije pronađen prilikom dohvatanja klikova za ID: {AdvertismentId}", advertismentId);
            //     return NotFound(ex.Message);
            // }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Greška prilikom dohvatanja vremenskih pečata klikova za oglas ID: {AdvertismentId}.", advertismentId);
                return StatusCode(StatusCodes.Status500InternalServerError, "Dogodila se greška na serveru prilikom dohvatanja podataka o klikovima.");
            }
        }

        [HttpGet("advertisement/{advertismentId:int}/conversions")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ICollection<DateTime>))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetConversionTimestamps(
        [FromRoute] int advertismentId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to)
        {
            _logger.LogInformation("Pokušaj dohvatanja vremenskih pečata klikova za oglas ID: {AdvertismentId}, od: {FromDate}, do: {ToDate}",
                advertismentId,
                from?.ToString("o") ?? "N/A",
                to?.ToString("o") ?? "N/A");

            // Osnovna validacija ulaznih parametara
            if (advertismentId <= 0)
            {
                _logger.LogWarning("Nevalidan advertismentId ({AdvertismentId}) prosleđen.", advertismentId);
                return BadRequest("ID oglasa mora biti pozitivan broj.");
            }

            // Validacija opsega datuma (ako su oba zadata)
            if (from.HasValue && to.HasValue && from.Value > to.Value)
            {
                _logger.LogWarning("Nevalidan opseg datuma: 'from' ({FromDate}) je posle 'to' ({ToDate}) za oglas ID: {AdvertismentId}.",
                    from.Value, to.Value, advertismentId);
                return BadRequest("Početni datum ne može biti posle krajnjeg datuma.");
            }

            try
            {
                var timestamps = await _adService.GetConversionsTimestampsAsync(advertismentId, from, to);

                return Ok(timestamps);
            }
            // Specifični izuzeci iz servisa ako ih ima i želite drugačije da ih tretirate
            // catch (AdvertisementNotFoundException ex) // Primer custom izuzetka
            // {
            //     _logger.LogWarning(ex, "Oglas nije pronađen prilikom dohvatanja klikova za ID: {AdvertismentId}", advertismentId);
            //     return NotFound(ex.Message);
            // }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Greška prilikom dohvatanja vremenskih pečata klikova za oglas ID: {AdvertismentId}.", advertismentId);
                return StatusCode(StatusCodes.Status500InternalServerError, "Dogodila se greška na serveru prilikom dohvatanja podataka o klikovima.");
            }
        }
    }
}