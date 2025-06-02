using SharedKernel.Languages.Models;
using SharedKernel.Models;

namespace SharedKernel.Languages.Interfaces
{
    public interface ILanguageService
    {
        Task<Language> CreateLanguageAsync(string code, string? name, Translation translation);
        Task DeleteLanguageAsync(string code);
        Task DeleteLanguageByIdAsync(int id);
        Task<Language?> GetLanguageAsync(string code);
        Task<Language?> GetLanguageByIdAsync(int id);
        Task<List<Language>> GetAllLanguages();
    }
}
