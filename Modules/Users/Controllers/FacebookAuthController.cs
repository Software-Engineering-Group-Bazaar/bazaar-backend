using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using YourAppNamespace.Users.Services;

namespace YourAppNamespace.Users.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FacebookAuthController : ControllerBase
    {
        private readonly FacebookSignInService _facebookService;

        public FacebookAuthController(FacebookSignInService facebookService)
        {
            _facebookService = facebookService;
        }

        [HttpPost("login")]
        public async Task<IActionResult> FacebookLogin([FromBody] string accessToken)
        {
            var result = await _facebookService.ValidateFacebookTokenAsync(accessToken);
            if (result == null)
                return Unauthorized("Nevalidan Facebook token");

            return Ok(result);
        }
    }
}
