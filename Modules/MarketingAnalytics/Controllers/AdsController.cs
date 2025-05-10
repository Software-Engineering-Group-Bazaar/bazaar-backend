// Controllers/ClicksController.cs
using System.Security.Claims;
using System.Threading.Tasks;
using MarketingAnalytics.Dtos;
using MarketingAnalytics.DTOs;
using MarketingAnalytics.Hubs;
using MarketingAnalytics.Interfaces;
using MarketingAnalytics.Models;
//using Microsoft.AspNet.SignalR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
namespace MarketingAnalytics.Controllers
{
    [Route("api/[controller]")]
    [Microsoft.AspNetCore.Authorization.Authorize]
    [ApiController]
    public class AdsController : ControllerBase
    {
        private readonly IAdService _adService;
        private readonly IRecommenderAgent _recommender;
        private readonly IHubContext<AdvertisementHub> _advertisementHubContext;
        private readonly ILogger<AdsController> _logger;

        public AdsController(IAdService adService, IRecommenderAgent recommender, IHubContext<AdvertisementHub> advertisementHubContext, ILogger<AdsController> logger)
        {
            _adService = adService;
            _logger = logger;
            _recommender = recommender;
            _advertisementHubContext = advertisementHubContext;
        }

        // POST: api/Ads/clicks/{id}
        [HttpPost("clicks/{id}")]
        [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(Clicks))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> RecordClick(int id)
        {

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("[AdsController] CreateOrder - Could not find user ID claim for the authenticated user.");
                return Unauthorized("User ID claim not found."); // 401 Unauthorized
            }

            try
            {
                var recordedClick = await _adService.RecordClickAsync(new AdStatsDto
                {
                    UserId = userId,
                    AdvertisementId = id
                });

                if (recordedClick == null)
                {
                    // Ovo se događa ako oglas nije pronađen u servisu
                    return NotFound($"Oglas s ID-om {id} nije pronađen.");
                }
                await _advertisementHubContext.Clients
                        .Group(AdvertisementHub.AdminGroup)
                        .SendAsync("ReceiveClickTimestamp", System.DateTime.UtcNow);
                var ad = await _adService.GetAdvertisementByIdAsync(id);
                await SendAdHelperAsync(ad);
                return StatusCode(StatusCodes.Status201Created, recordedClick);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Greška prilikom bilježenja klika za AdId: {AdvertisementId}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "Dogodila se greška na serveru prilikom bilježenja klika.");
            }
        }

        // POST: api/Ads/conversions/{id}
        [HttpPost("conversions/{id}")]
        [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(Conversions))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> RecordConversion(int id)
        {

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("[AdsController] CreateOrder - Could not find user ID claim for the authenticated user.");
                return Unauthorized("User ID claim not found."); // 401 Unauthorized
            }

            try
            {
                var recordedConversion = await _adService.RecordConversionAsync(new AdStatsDto
                {
                    UserId = userId,
                    AdvertisementId = id
                });

                if (recordedConversion == null)
                {
                    // Ovo se događa ako oglas nije pronađen u servisu
                    return NotFound($"Oglas s ID-om {id} nije pronađen.");
                }
                await _advertisementHubContext.Clients
                        .Group(AdvertisementHub.AdminGroup)
                        .SendAsync("ReceiveConversionTimestamp", System.DateTime.UtcNow);
                var p = _advertisementHubContext.Clients.Group(AdvertisementHub.AdminGroup);
                var ad = await _adService.GetAdvertisementByIdAsync(id);
                await SendAdHelperAsync(ad);
                return StatusCode(StatusCodes.Status201Created, recordedConversion);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Greška prilikom bilježenja konverzije za AdId: {AdvertisementId}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "Dogodila se greška na serveru prilikom bilježenja konverzije.");
            }
        }


        [HttpPost("activity/view/{id}")]
        [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(UserActivity))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> CreateUserActivityView(int id)
        {
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("CreateUserActivity - ModelState nije validan. {@ModelState}", ModelState);
                return BadRequest(ModelState);
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("[UserActivitiesController] CreateUserActivity - Nije pronađen User ID claim za autentifikovanog korisnika.");
                return Unauthorized("User ID claim nije pronađen."); // 401 Unauthorized
            }

            try
            {
                // Mapiranje DTO na UserActivity model
                var userActivity = new UserActivity
                {
                    UserId = userId,
                    ProductCategoryId = id,
                    InteractionType = InteractionType.View,
                };

                var createdActivity = await _adService.CreateUserActivityAsync(userActivity);

                return StatusCode(StatusCodes.Status201Created, createdActivity);
            }
            catch (ArgumentNullException ex) // Uhvaćeno iz servisa ako se prosledi null userActivity
            {
                _logger.LogWarning(ex, "Greška ArgumentNullException prilikom kreiranja UserActivityView za korisnika {UserId}.", userId);
                return BadRequest(new { message = ex.Message, paramName = ex.ParamName });
            }
            catch (ArgumentException ex) // Uhvaćeno iz servisa (npr. nevalidan ProductCategoryId, UserId)
            {
                _logger.LogWarning(ex, "Greška ArgumentException prilikom kreiranja UserActivityView za korisnika {UserId}.", userId);
                // Vratite poruku izuzetka koja je informativna
                return BadRequest(new { message = ex.Message, paramName = ex.ParamName });
            }
            catch (InvalidOperationException ex) // Uhvaćeno iz servisa (npr. SaveChangesAsync nije napravio izmene)
            {
                _logger.LogError(ex, "Greška InvalidOperationException prilikom kreiranja UserActivityView za korisnika {UserId}.", userId);
                return StatusCode(StatusCodes.Status500InternalServerError, $"Dogodila se greška na serveru: {ex.Message}");
            }
            // DbUpdateException je već logiran u servisu, ali ga možemo uhvatiti i ovde za specifičan HTTP odgovor
            catch (Microsoft.EntityFrameworkCore.DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "Greška DbUpdateException prilikom kreiranja UserActivityView za korisnika {UserId} u kontroleru.", userId);
                return StatusCode(StatusCodes.Status500InternalServerError, "Dogodila se greška prilikom čuvanja podataka.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Neočekivana greška prilikom kreiranja UserActivityView za korisnika {UserId}.", userId);
                return StatusCode(StatusCodes.Status500InternalServerError, "Dogodila se neočekivana greška na serveru.");
            }
        }
        [HttpGet("ads")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<AdvertismentFeatureDto>))]
        public async Task<ActionResult<List<AdvertismentFeatureDto>>> GetAds()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var ads = await _recommender.RecommendAsync(userId);
            var dto = ads.Select(adVec =>
            {
                var advertisement = adVec.Ad;
                return new AdvertismentFeatureDto
                {
                    Advertisment = new AdvertismentDto
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
                        Triggers = _adService.AdTriggerToString(advertisement.Triggers),
                        AdData = advertisement.AdData.Select(ad => new AdDataDto
                        {
                            Id = ad.Id,
                            StoreId = ad.StoreId,
                            ImageUrl = ad.ImageUrl,
                            Description = ad.Description,
                            ProductId = ad.ProductId
                        }).ToList()
                    },
                    FeatureVec = adVec.FeatureVec
                };
            }
            );
            return Ok(dto);
        }

        [HttpPost("reward")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(object))]
        public async Task<ActionResult> Reward([FromBody] RewardDto rewardDto)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            await _recommender.RecordRewardAsync(rewardDto.FeatureVec, rewardDto.Reward, userId);
            return Ok("Weights updated!");
        }


        // // GET: api/clicks/advertisement/{advertisementId}
        // [HttpGet("advertisement/{advertisementId:int}")]
        // [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<Clicks>))]
        // [ProducesResponseType(StatusCodes.Status404NotFound)]
        // public async Task<IActionResult> GetClicksForAdvertisement(int advertisementId)
        // {
        //     var clicks = await _adService.GetClicksByAdvertisementAsync(advertisementId);
        //     if (!clicks.Any())
        //     {
        //         // Možete odlučiti hoćete li vratiti 404 ili prazan niz 200 OK
        //         // return NotFound($"Nema klikova za oglas s ID-om {advertisementId}.");
        //     }
        //     return Ok(clicks);
        // }

        // // GET: api/clicks/user/{userId}
        // [HttpGet("user/{userId}")]
        // [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<Clicks>))]
        // public async Task<IActionResult> GetClicksForUser(string userId)
        // {
        //     var clicks = await _adService.GetClicksByUserAsync(userId);
        //     return Ok(clicks);
        // }

        // Opcionalno: GET: api/clicks/{id}
        // Ako ga dodate, možete koristiti CreatedAtAction u POST metodi.
        /*
        [HttpGet("{id:int}")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(Clicks))]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetClickById(int id)
        {
            var click = await _context.Clicks.Include(c => c.Advertisement).FirstOrDefaultAsync(c => c.Id == id); // Primjer direktnog pristupa DbContextu
            if (click == null)
            {
                return NotFound();
            }
            return Ok(click);
        }
        */

        private async Task SendAdHelperAsync(Advertisment ad)
        {
            var dto = new AdvertismentDto
            {
                Id = ad.Id,
                SellerId = ad.SellerId,
                StartTime = ad.StartTime,
                EndTime = ad.EndTime,
                IsActive = ad.IsActive,
                Views = ad.Views,
                ViewPrice = ad.ViewPrice,
                Clicks = ad.Clicks,
                ClickPrice = ad.ClickPrice,
                Conversions = ad.Conversions,
                ConversionPrice = ad.ConversionPrice,
                AdType = ad.AdType.ToString(),
                Triggers = _adService.AdTriggerToString(ad.Triggers), // Use the helper
                AdData = ad.AdData.Select(a => new AdDataDto
                {
                    Id = a.Id,
                    StoreId = a.StoreId,
                    ImageUrl = a.ImageUrl,
                    Description = a.Description,
                    ProductId = a.ProductId
                }).ToList()
            };

            await _advertisementHubContext.Clients
                .Group(AdvertisementHub.AdminGroup)
                .SendAsync("ReceiveAdUpdate", dto);
        }
    }
}