using System.Collections.Generic;
using System.Threading.Tasks;
using Catalog.Models;

namespace Catalog.Services
{
    public interface IProductCategoryService
    {
        Task<IEnumerable<ProductCategory>> GetAllCategoriesAsync();
        Task<ProductCategory?> GetCategoryByIdAsync(int id);
        Task<ProductCategory?> GetCategoryByNameAsync(string name);
        Task<ProductCategory> CreateCategoryAsync(ProductCategory category);
        Task<bool> UpdateCategoryAsync(ProductCategory category);
        Task<bool> DeleteCategoryAsync(int id);
    }
}