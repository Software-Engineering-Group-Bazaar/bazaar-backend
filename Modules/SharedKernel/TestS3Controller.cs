using Catalog.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace SharedKernel
{
    [ApiController]
    [Route("api/[controller]")]
    public class TestS3Controller : ControllerBase
    {
        IImageStorageService imageStorageService;


        public TestS3Controller(IImageStorageService istrg)
        {
            imageStorageService = istrg;
        }
        [HttpPost("upload")]
        public async Task<IActionResult> Spasi(IFormFile slicica)
        {
            await imageStorageService.UploadImageAsync(slicica, "test");
            return Ok("AAAAAAAAAAA");
        }
    }
}