using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Languages.Interfaces;
using SharedKernel.Languages.Models;
using SharedKernel.Models;

namespace SharedKernel.Languages.Services
{
    public class LanguageService : ILanguageService
    {
        private readonly ILogger<LanguageService> _logger;
        private readonly LanguageDbContext _context;

        public LanguageService(LanguageDbContext context, ILogger<LanguageService> logger)
        {
            _logger = logger;
            _context = context;
        }

        public async Task<List<Language>> GetAllLanguages()
        {
            var langs = await _context.Languages.ToListAsync();
            return langs;
        }

        public async Task<Language?> GetLanguageByIdAsync(int id)
        {
            var lang = await _context.Languages.FirstOrDefaultAsync(l => l.Id == id);
            return lang;
        }

        public async Task<Language?> GetLanguageAsync(string code)
        {
            var lang = await _context.Languages.FirstOrDefaultAsync(l => l.Code == code);
            return lang;
        }

        public async Task DeleteLanguageByIdAsync(int id)
        {
            var lang = await _context.Languages.FirstOrDefaultAsync(l => l.Id == id);
            if (lang is null)
                throw new InvalidDataException($"language {id} does not exist");
            _context.Languages.Remove(lang);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteLanguageAsync(string code)
        {
            var lang = await _context.Languages.FirstOrDefaultAsync(l => l.Code == code);
            if (lang is null)
                throw new InvalidDataException($"language {code} does not exist");
            _context.Languages.Remove(lang);
            await _context.SaveChangesAsync();
        }

        public async Task<Language> CreateLanguageAsync(
            string code,
            string? name,
            Translation translation
        )
        {
            var lang = new Language
            {
                Code = code,
                Translation = translation,
                Name = name,
            };
            _context.Add(lang);
            await _context.SaveChangesAsync();
            return lang;
        }
    }
}
