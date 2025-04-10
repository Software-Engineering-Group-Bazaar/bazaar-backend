using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Catalog.Interfaces
{
    public interface IImageStorageService
    {
        /// Uploaduje sliku na S3 i vraća javni URL.
        Task<string?> UploadImageAsync(IFormFile imageFile, string? subfolder = null);

        // Opciono: Metoda za brisanje ako bude potrebna kasnije
        // Task<bool> DeleteImageAsync(string fileUrl);
    }
}