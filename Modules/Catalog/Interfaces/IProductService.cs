using Microsoft.AspNetCore.Http;
using Modules.Catalog.Models;

namespace Modules.Catalog.Services
{
    public interface IProductService
    {
        Task<ProductDto?> AddProductToStoreAsync(
            string sellerUserId,
            int storeId,
            CreateProductRequestDto productData,
            List<IFormFile> imageFiles);
    }
}
