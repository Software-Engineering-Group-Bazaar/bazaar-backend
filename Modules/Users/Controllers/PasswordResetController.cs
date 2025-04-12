using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Users.Interfaces;
using Users.Models.Dtos;

[ApiController]
[Route("api/[controller]")]
[AllowAnonymous] // Ove metode moraju biti dostupne neulogovanim korisnicima
public class PasswordResetController : ControllerBase
{
    private readonly IPasswordResetService _passwordResetService;
    private readonly ILogger<PasswordResetController> _logger;

    public PasswordResetController(IPasswordResetService passwordResetService, ILogger<PasswordResetController> logger)
    {
        _passwordResetService = passwordResetService;
        _logger = logger;
    }

    [HttpPost("request-reset")]
    [ProducesResponseType(StatusCodes.Status200OK)] // Uvek vraća 200 OK
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> RequestPasswordReset([FromBody] RequestPasswordResetDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            await _passwordResetService.RequestPasswordResetAsync(dto.Email);
            // UVEK vrati OK, čak i ako email ne postoji, da se ne otkriva informacija
            return Ok(new { message = "Ako nalog sa datom email adresom postoji, poslan je kod za resetovanje." });
        }
        catch (InvalidOperationException ex) // Samo za kritične greške poput slanja emaila
        {
            _logger.LogError(ex, "Neočekivana greška prilikom zahtjeva za reset lozinke za email: {Email}", dto.Email);
            // Ne vraćaj detalje greške korisniku
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Došlo je do greške prilikom obrade vašeg zahtjeva." });
        }
        catch (Exception ex) // Hvatanje opštih grešaka
        {
            _logger.LogError(ex, "Neočekivana greška prilikom zahtjeva za reset lozinke za email: {Email}", dto.Email);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Došlo je do greške prilikom obrade vašeg zahtjeva." });
        }
    }

    [HttpPost("reset-password")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var result = await _passwordResetService.ResetPasswordAsync(dto);

            if (result.Succeeded)
            {
                return Ok(new { message = "Lozinka je uspješno resetovana." });
            }

            // Ako IdentityResult nije uspeo, vrati greške
            foreach (var error in result.Errors)
            {
                // Možeš proveriti error.Code ako želiš specifične poruke
                if (error.Code == "InvalidCode")
                {
                    ModelState.AddModelError("Code", error.Description);
                }
                else // Opšta greška (npr. lozinka ne zadovoljava pravila)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }
            return BadRequest(ModelState);
        }
        catch (Exception ex) // Hvatanje opštih grešaka
        {
            _logger.LogError(ex, "Neočekivana greška prilikom resetovanja lozinke za email: {Email}", dto.Email);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Došlo je do greške prilikom resetovanja lozinke." });
        }
    }
}