using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Users.Dtos;
using Users.Interfaces;
using Users.Models;

namespace Users.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        // api/auth/register
        [AllowAnonymous]
        [HttpPost("register")]
        public async Task<ActionResult<string>> Register([FromBody] RegisterDto registerDto)
        {
            // Pozivamo metodu iz AuthService za registraciju
            var result = await _authService.RegisterAsync(registerDto);
            if (result == "Registracija uspješna.")
            {
                return Ok(new { message = result });
            }
            else
            {
                return BadRequest(new { message = result });
            }
        }

        // api/auth/login
        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<ActionResult<LoginResponseDto>> Login([FromBody] LoginDto loginDto)
        {
            var response = await _authService.LoginAsync(loginDto);
            if (response == null)
            {
                return Unauthorized(new { message = "Neispravni podaci" });
            }

            // Vraćamo odgovor sa tokenom
            return Ok(response);
        }

        // api/auth/logout
        [HttpPost("logout")]
        public async Task<ActionResult> Logout()
        {
            await _authService.LogoutAsync();
            return Ok(new { message = "Izlogovani ste." });
        }
    }
}
