using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Users.Interfaces;
using Users.Models;

namespace Users.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class TestAuthController : ControllerBase
    {
        private readonly UserManager<User> _userManager;
        private readonly SignInManager<User> _signInManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IConfiguration _configuration;
        private readonly IJWTService _jwtService;

        public TestAuthController(UserManager<User> userManager, SignInManager<User> signInManager, RoleManager<IdentityRole> roleManager, IConfiguration configuration, IJWTService jWTService)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _roleManager = roleManager;
            _configuration = configuration;
            _jwtService = jWTService;
        }

        // api/auth/register
        [AllowAnonymous]
        [HttpPost("register")]
        public async Task<ActionResult<string>> Register()
        {
            //await _userManager.CreateAsync(); // preferirajte async metode
            return Ok(new { Ovdje = "neki dto a ne ovako", Message = "Nmp" });
        }

        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<ActionResult<string>> Login([FromBody] LoginDTO loginDTO)
        {
            var user = await _userManager.FindByNameAsync(loginDTO.Username)
                ?? await _userManager.FindByEmailAsync(loginDTO.Username);
            if (user == null)
            {
                return Unauthorized(new { message = "Invalid credentials" });
            }
            var result = await _signInManager.CheckPasswordSignInAsync(user, loginDTO.Password, lockoutOnFailure: true);

            if (!result.Succeeded)
            {
                if (result.IsLockedOut)
                {
                    return Unauthorized(new { message = "Account locked out. Too many failed login attempts." });
                }
                return Unauthorized(new { message = "Invalid credentials" }); // Generic message for wrong password
            }

            try
            {
                var roles = await _userManager.GetRolesAsync(user);

                // This line should now work correctly!
                (string token, DateTime expiry) = await _jwtService.GenerateTokenAsync(user, roles);

                // 4. Return the token
                var response = new LoginResponseDto
                {
                    Token = token,
                    Email = user.Email
                };

                /// OBRATITE PAZNJU NA OVO
                /// OVAKO SE NAMJESTI DA BROWSER AUTOMATSKI SALJE TOKEN NAZAD
                /// AKO POKRECETE SA dotnet run --launch-profile https
                /// POTREBNO JE DA ILI dotnet dev-certs https --trust uradite
                /// ILI DA U BROWSERU KLIKNETE ADVANCED DA VJERUJETE SAJTU
                /// OVO JE ZATO STO NEMAM CERTIFIKAT JOS ALI OVAKO NAMJESTAMO TOKEN KROZ LOGIN I REGISSTRACIJU
                Response.Cookies.Append("X-Access-Token", token, new CookieOptions // Choose a cookie name
                {
                    HttpOnly = true, // Crucial for preventing XSS access
                    Secure = true,   // Crucial: Only send over HTTPS
                    Expires = expiry, // Match token expiration
                    SameSite = SameSiteMode.None // Good default for CSRF protection, allows top-level navigation GETs. Use Strict if possible.
                                                 // Domain = "yourdomain.com" // Optional: If needed for subdomains
                                                 // Path = "/" // Optional: Usually root path
                });

                return Ok(new { Message = "Login successful", Username = user.Email });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while processing your request." });
            }

        }

        public class LoginDTO
        {
            [Required]
            public string Username { get; set; }
            [Required]
            public string Password { get; set; }
        };

        public class LoginResponseDto
        {
            public string Email { get; set; }
            public string Token { get; set; }

        }

    }
}