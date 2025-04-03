using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Users.Models;

namespace Users.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<User> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IConfiguration _configuration;

        public AuthController(UserManager<User> userManager, RoleManager<IdentityRole> roleManager, IConfiguration configuration)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _configuration = configuration;
        }

        // api/auth/register
        [AllowAnonymous]
        [HttpPost("register")]
        public async Task<ActionResult<string>> Register()
        {
            //await _userManager.CreateAsync(); // preferirajte async metode
            return Ok(new { Ovdje = "neki dto a ne ovako", Message = "Nmp" });
        }

    }
}