using Catalog.Interfaces;
using Catalog.Models;


namespace Catalog.Services
{
    public class FileImageStorageService : IImageStorageService
    {
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<FileImageStorageService> _logger;
        private readonly string _storagePath;
        private readonly string _baseUrl;

        public FileImageStorageService(IWebHostEnvironment env, ILogger<FileImageStorageService> logger, IConfiguration configuration)
        {
            _env = env;
            _logger = logger;

            // Get path from config, default to 'images/products' inside wwwroot
            string relativePath = configuration.GetValue<string>("LocalStorage:ProductImagePath") ?? "images/products";
            _storagePath = Path.Combine(_env.WebRootPath, relativePath);
            _baseUrl = "/" + relativePath.Replace('\\', '/'); // Ensure forward slashes for URL

            // Ensure the directory exists
            if (!Directory.Exists(_storagePath))
            {
                Directory.CreateDirectory(_storagePath);
            }
        }


        public Task<string?> UploadImageAsync(IFormFile imageFile, string? subfolder = null)
        {
            if (imageFile == null || imageFile.Length == 0)
            {
                return null;
            }
            try
            {
                var originalFileName = Path.GetFileName(imageFile.FileName);
                var uniqueFileName = Guid.NewGuid().ToString() + Path.GetExtension(originalFileName);
                var filePath = Path.Combine(_storagePath, uniqueFileName);

                var stream = new FileStream(filePath, FileMode.Create);
                await imageFile.CopyToAsync(stream);

                var relativeUrlPath = Path.Combine(_baseUrl, uniqueFileName).Replace('\\', '/');

                _logger.LogInformation("Saved image locally to {FilePath}. Relative URL: {RelativeUrlPath}", filePath, relativeUrlPath);

                return new ProductPicture
                {
                    Path = relativeUrlPath, // e.g., /images/products/guid.jpg
                    Name = originalFileName // Store the original name for reference
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving image locally for file {FileName}", imageFile.FileName);
                // Optionally rethrow or handle specific exceptions
                return null; // Indicate failure
            }
        }
    }

}