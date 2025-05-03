using System;
using System.Collections.Generic;
using System.Threading.Tasks;
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
        [ProducesResponseType(typeof(IEnumerable<Advertisment>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<Advertisment>>> GetAllAdvertisements()
        {
            try
            {
                var advertisements = await _adService.GetAllAdvertisementsAsync();
                return Ok(advertisements);
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
        [ProducesResponseType(typeof(Advertisment), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<Advertisment>> GetAdvertisementById(int id)
        {
            try
            {
                var advertisement = await _adService.GetAdvertisementByIdAsync(id);
                if (advertisement == null)
                {
                    _logger.LogWarning("GetAdvertisementById: Advertisement with ID {AdvertisementId} not found.", id);
                    return NotFound($"Advertisement with ID {id} not found.");
                }
                return Ok(advertisement);
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
        [ProducesResponseType(typeof(Advertisment), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<Advertisment>> CreateAdvertisement([FromForm] CreateAdvertismentRequestDto request)
        {
            // Basic check, model state validation handles more via [ApiController]
            if (request?.AdDataItems == null || !request.AdDataItems.Any())
            {
                return BadRequest("At least one AdData item must be provided.");
            }

            try
            {
                var createdAdvertisement = await _adService.CreateAdvertismentAsync(request);
                // Return 201 Created with a Location header pointing to the new resource
                return CreatedAtRoute("GetAdvertisementById", new { id = createdAdvertisement.Id }, createdAdvertisement);
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
        [ProducesResponseType(typeof(Advertisment), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<Advertisment>> UpdateAdvertisement(int advertismentId, [FromForm] UpdateAdvertismentRequestDto request)
        {
            try
            {
                var updatedAdvertisement = await _adService.UpdateAdvertismentAsync(advertismentId, request);
                if (updatedAdvertisement == null)
                {
                    _logger.LogWarning("UpdateAdvertisement: Advertisement with ID {AdvertisementId} not found.", advertismentId);
                    return NotFound($"Advertisement with ID {advertismentId} not found.");
                }
                return Ok(updatedAdvertisement); // Return the updated entity
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
        [ProducesResponseType(typeof(AdData), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<AdData>> UpdateAdData(int adDataId, [FromForm] UpdateAdDataRequestDto request)
        {
            try
            {
                var updatedAdData = await _adService.UpdateAdDataAsync(adDataId, request);
                if (updatedAdData == null)
                {
                    _logger.LogWarning("UpdateAdData: AdData with ID {AdDataId} not found.", adDataId);
                    return NotFound($"AdData with ID {adDataId} not found.");
                }
                return Ok(updatedAdData);
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
    }
}