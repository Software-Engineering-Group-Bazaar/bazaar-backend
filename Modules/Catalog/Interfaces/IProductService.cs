using System.Collections.Generic;
using System.Threading.Tasks;
using Catalog.Models;

namespace Catalog.Services
{
    public interface IProductService
    {
        Task<IEnumerable<Product>> GetAllProductsAsync();
        Task<Product?> GetProductByIdAsync(int id);
        Task<IEnumerable<Product>> GetProductsByCategoryIdAsync(int categoryId);
        Task<IEnumerable<Product>> GetProductsByStoreIdAsync(int storeId);
        Task<Product> CreateProductAsync(Product product, List<IFormFile>? files);
        Task<bool> UpdateProductAsync(Product product);
        Task<bool> DeleteProductAsync(int id);
    }
}