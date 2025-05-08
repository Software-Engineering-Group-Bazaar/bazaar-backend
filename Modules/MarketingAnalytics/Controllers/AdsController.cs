// Controllers/ClicksController.cs
using System.Threading.Tasks;
using MarketingAnalytics.Interfaces;
using MarketingAnalytics.Models;
using Microsoft.AspNetCore.Mvc;
namespace MarketingAnalytics.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AdsController : ControllerBase
    {
        private readonly IAdService _adService;
        private readonly ILogger<AdsController> _logger;

        public AdsController(IAdService adService, ILogger<AdsController> logger)
        {
            _adService = adService;
            _logger = logger;
        }

        // POST: api/clicks
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(Clicks))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> RecordClick([FromBody] AdStatsDto clickDto)
        {
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Neuspjela validacija za bilježenje klika: {@ModelState}", ModelState);
                return BadRequest(ModelState);
            }

            try
            {
                var recordedClick = await _adService.RecordClickAsync(clickDto);

                if (recordedClick == null)
                {
                    // Ovo se događa ako oglas nije pronađen u servisu
                    return NotFound($"Oglas s ID-om {clickDto.AdvertisementId} nije pronađen.");
                }

                // Vraća 201 Created s lokacijom novog resursa (ako imate GetById endpoint)
                // i tijelom novokreiranog klika.
                // Za jednostavnost, možemo samo vratiti objekt.
                // return CreatedAtAction(nameof(GetClickById), new { id = recordedClick.Id }, recordedClick);
                // Ako nemate GetClickById, jednostavniji je Ok() ili Created() bez lokacije.
                return Created($"/api/clicks/{recordedClick.Id}", recordedClick); // Pretpostavka da ćete imati GetById
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Greška prilikom bilježenja klika za UserId: {UserId}, AdId: {AdvertisementId}", clickDto.UserId, clickDto.AdvertisementId);
                return StatusCode(StatusCodes.Status500InternalServerError, "Dogodila se greška na serveru prilikom bilježenja klika.");
            }
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
    }
}