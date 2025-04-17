using System.Collections.Generic;
using System.Threading.Tasks;
using Store.Models; // Koristimo DTOs

namespace Store.Interface
{
    public interface IStoreCategoryService
    {
        // Prima DTO za kreiranje, vraća DTO za prikaz, asinhrono
        Task<StoreCategoryDto> CreateCategoryAsync(StoreCategoryCreateDto createDto);

        // Vraća listu DTO-a za prikaz, asinhrono
        Task<IEnumerable<StoreCategoryDto>> GetAllCategoriesAsync();

        // Vraća DTO za prikaz ili null, asinhrono
        Task<StoreCategoryDto?> GetCategoryByIdAsync(int id);

        // Prima ID i DTO za ažuriranje, vraća ažurirani DTO ili null, asinhrono
        Task<StoreCategoryDto?> UpdateCategoryAsync(int id, StoreCategoryDto updateDto);

        // Vraća bool (uspjeh/neuspjeh - npr. nije nađen), asinhrono
        Task<bool> DeleteCategoryAsync(int id);
    }
}