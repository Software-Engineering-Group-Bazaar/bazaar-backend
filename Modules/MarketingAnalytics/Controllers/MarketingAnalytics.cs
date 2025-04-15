using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;


namespace MarketingAnalytics.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class MarketingAnalyticsController : ControllerBase
    {

    }
}