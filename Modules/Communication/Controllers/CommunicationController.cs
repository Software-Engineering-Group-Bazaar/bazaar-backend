using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;


namespace Communication.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class CommunicationController : ControllerBase
    {

    }
}