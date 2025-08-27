using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;

namespace GetSportAPI.Controllers
{
    [Route("api/images/view")]
    [ApiController]
    public class ImageHelperController : ControllerBase
    {
        private readonly IWebHostEnvironment _environment;

        public ImageHelperController(IWebHostEnvironment environment)
        {
            _environment = environment;
        }

        [HttpGet("{filename}")]
        public IActionResult GetImage(string filename)
        {
            var path = Path.Combine(_environment.WebRootPath, "images", filename);
            if (!System.IO.File.Exists(path))
                return NotFound(new { message = "Image not found." });

            var provider = new FileExtensionContentTypeProvider();
            if (!provider.TryGetContentType(path, out string contentType))
            {
                contentType = "application/octet-stream"; 
            }

            var image = System.IO.File.OpenRead(path);
            return File(image, contentType);
        }
    }
}
