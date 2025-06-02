using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json; // For JsonSerializer
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SharedKernel.Api.Dtos; // Your DTO namespace
using SharedKernel.Languages.Interfaces;
using SharedKernel.Languages.Models;
using SharedKernel.Languages.Services;
using SharedKernel.Models;

namespace SharedKernel.Controllers // Adjust namespace accordingly
{
    [ApiController]
    [Route("api/translations")]
    public class LanguageController : ControllerBase
    {
        private readonly ILanguageService _languageService;
        private readonly ILogger<LanguageController> _logger;
        private const string MasterLanguageCode = "en"; // Define your master language code

        public LanguageController(ILanguageService languageService, ILogger<LanguageController> logger)
        {
            _languageService = languageService;
            _logger = logger;
        }

        // GET /api/translations/languages - izlista sve jezike spasene
        [HttpGet("languages")]
        [ProducesResponseType(typeof(List<Language>), 200)]
        public async Task<IActionResult> GetAllLanguages()
        {
            _logger.LogInformation("Fetching all languages.");
            var languages = await _languageService.GetAllLanguages();
            return Ok(languages);
        }

        // GET /api/translations/{langCode} - dohvaca prevod
        [HttpGet("{langCode}")]
        // ProducesResponseType sada može biti typeof(object) ili direktno application/json
        [ProducesResponseType(typeof(object), 200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> GetTranslation(string langCode)
        {
            _logger.LogInformation("Fetching translation for language code: {LanguageCode}", langCode);
            var language = await _languageService.GetLanguageAsync(langCode);

            if (language == null)
            {
                _logger.LogWarning("Language with code {LanguageCode} not found.", langCode);
                return NotFound(new { message = $"Language '{langCode}' not found." });
            }

            if (language.Translation == null || string.IsNullOrWhiteSpace(language.Translation.Data))
            {
                _logger.LogInformation("Language {LanguageCode} found, but has no translation data.", langCode);
                // Vratite prazan JSON objekat ako nema prevoda
                return Ok(new Dictionary<string, string>());
            }

            try
            {
                // Vraćamo sirovi JSON string kao ContentResult sa odgovarajućim content type-om.
                // Frontend (i18next) će ga parsirati.
                // Alternativno, možete deserijalizovati u JsonDocument da proverite validnost
                // i vratiti JsonDocument.RootElement kao OkObjectResult.
                // var jsonDocument = JsonDocument.Parse(language.Translation.Data);
                // return Ok(jsonDocument.RootElement);
                return Content(language.Translation.Data, "application/json");
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to parse or return translation data for language {LanguageCode}.", langCode);
                return Problem($"Error processing translation data for language '{langCode}'. Data might be corrupt.");
            }
        }

        // POST /api/translations/languages - kreiranje novog jezika
        [HttpPost("languages")]
        [ProducesResponseType(typeof(Language), 201)]
        [ProducesResponseType(400)]
        [ProducesResponseType(409)] // Conflict if language code already exists
        public async Task<IActionResult> CreateLanguage([FromBody] CreateLanguageRequestDto request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            _logger.LogInformation("Attempting to create language with code: {LanguageCode}", request.Code);

            if (request.Translations == null || !request.Translations.Any())
            {
                _logger.LogWarning("Attempted to create language {LanguageCode} with no translation data.", request.Code);
                // return BadRequest("Translations are required."); // Or allow empty if business logic permits
            }

            if (await _languageService.GetLanguageAsync(request.Code) is not null)
            {
                return Conflict(new { message = "Lele ima već jezik." });
            }

            var translationData = request.Translations ?? new Dictionary<string, string>();

            var translation = new Translation
            {
                Data = JsonSerializer.Serialize(translationData)
            };

            try
            {
                var createdLanguage = await _languageService.CreateLanguageAsync(request.Code, request.Name, translation);
                _logger.LogInformation("Language {LanguageCode} created successfully with ID {LanguageId}.", createdLanguage.Code, createdLanguage.Id);

                // Return the created language object, which includes the ID and the serialized translation data.
                // The client can then decide if it needs to re-fetch the deserialized version via GET /{langCode}
                return CreatedAtAction(nameof(GetTranslation), new { langCode = createdLanguage.Code }, createdLanguage);
            }
            catch (InvalidOperationException ex) // Catch specific exception for existing language
            {
                _logger.LogWarning("Failed to create language: {ErrorMessage}", ex.Message);
                return Conflict(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating language with code {LanguageCode}.", request.Code);
                return Problem("An unexpected error occurred while creating the language.");
            }
        }

        // GET /api/translations/master-keys - dohvaca template za novi jezik
        [HttpGet("master-keys")]
        [ProducesResponseType(typeof(List<string>), 200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> GetMasterKeys()
        {
            _logger.LogInformation("Fetching master keys from language code: {MasterLanguageCode}", MasterLanguageCode);
            var masterLanguage = await _languageService.GetLanguageAsync(MasterLanguageCode);

            if (masterLanguage == null || masterLanguage.Translation == null || string.IsNullOrWhiteSpace(masterLanguage.Translation.Data))
            {
                _logger.LogWarning("Master language {MasterLanguageCode} or its translations not found.", MasterLanguageCode);
                return NotFound($"Master language '{MasterLanguageCode}' or its translations not found. Cannot provide master keys.");
            }

            try
            {
                var translations = JsonSerializer.Deserialize<Dictionary<string, string>>(masterLanguage.Translation.Data);
                return Ok(translations?.Keys.ToList() ?? new List<string>());
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to deserialize master language ({MasterLanguageCode}) data for keys.", MasterLanguageCode);
                return Problem($"Error deserializing master language '{MasterLanguageCode}' data. Data might be corrupt.");
            }
        }

        // POST /api/translations/template - primi json za defaultni jezik (engleski)
        [HttpPost("template")]
        [ProducesResponseType(200)] // Or 201 if creating, 204 if updating with no content
        [ProducesResponseType(400)]
        public async Task<IActionResult> SetMasterTemplate([FromBody] Dictionary<string, string> masterTranslations)
        {
            if (masterTranslations == null) // Removed !masterTranslations.Any() to allow clearing translations
            {
                return BadRequest("Master translation data cannot be null.");
            }

            _logger.LogInformation("Setting/Updating master template for language code: {MasterLanguageCode}", MasterLanguageCode);

            var translationJson = JsonSerializer.Serialize(masterTranslations);
            var translationObject = new Translation { Data = translationJson };

            try
            {
                await _languageService.CreateLanguageAsync(MasterLanguageCode, "English (Master)", translationObject);
                _logger.LogInformation("Master template for {MasterLanguageCode} created.", MasterLanguageCode);
                // It's common to return the created resource or a 201 Created
                return CreatedAtAction(nameof(GetTranslation), new { langCode = MasterLanguageCode },
                                       new { message = $"Master template for '{MasterLanguageCode}' created successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting master template for {MasterLanguageCode}.", MasterLanguageCode);
                return Problem("An unexpected error occurred while setting the master template.");
            }
        }

        // Optional: DELETE /api/translations/languages/{langCode}
        [HttpDelete("languages/{langCode}")]
        [ProducesResponseType(204)] // No Content
        [ProducesResponseType(404)]
        public async Task<IActionResult> DeleteLanguage(string langCode)
        {
            _logger.LogInformation("Attempting to delete language with code: {LanguageCode}", langCode);
            try
            {
                await _languageService.DeleteLanguageAsync(langCode);
                _logger.LogInformation("Language with code {LanguageCode} deleted successfully.", langCode);
                return NoContent();
            }
            catch (InvalidDataException ex) // From LanguageService
            {
                _logger.LogWarning("Failed to delete language {LanguageCode}: {ErrorMessage}", langCode, ex.Message);
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting language with code {LanguageCode}.", langCode);
                return Problem("An unexpected error occurred while deleting the language.");
            }
        }
    }
}