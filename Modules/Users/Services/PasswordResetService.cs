using System;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SharedKernel.Interfaces; // Za IMailService
using SharedKernel.Models;   // Za MailRequest
using Users.Interfaces;
using Users.Models;
using Users.Models.Dtos;

namespace Users.Services
{
    public class PasswordResetService : IPasswordResetService
    {
        private readonly UserManager<User> _userManager;
        private readonly UsersDbContext _context; // Vaš DbContext gde je DbSet<PasswordResetRequest>
        private readonly IMailService _mailService;
        private readonly IPasswordHasher<PasswordResetRequest> _codeHasher; // Za hashiranje koda
        private readonly ILogger<PasswordResetService> _logger;

        // Koliko dugo kod važi (npr. 15 minuta)
        private readonly TimeSpan _codeValidityDuration = TimeSpan.FromMinutes(15);
        // Minimalni interval između dva zahteva (npr. 5 minuta)
        private readonly TimeSpan _minRequestInterval = TimeSpan.FromMinutes(5);


        public PasswordResetService(
            UserManager<User> userManager,
            UsersDbContext context,
            IMailService mailService,
            IPasswordHasher<PasswordResetRequest> codeHasher, // Injektuj hasher
            ILogger<PasswordResetService> logger)
        {
            _userManager = userManager;
            _context = context;
            _mailService = mailService;
            _codeHasher = codeHasher;
            _logger = logger;
        }

        public async Task RequestPasswordResetAsync(string email)
        {
            var user = await _userManager.FindByEmailAsync(email);

            // NE OTKRIVAJ da li korisnik postoji. Uvek vrati kao da je uspeh.
            if (user == null)
            {
                _logger.LogWarning("Zahtev za reset lozinke za nepostojeći email: {Email}", email);
                // Ne bacaj grešku ovde, samo izađi.
                return;
            }

            // Provera rate limiting-a: Da li je korisnik prečesto tražio?
            var latestRequest = await _context.PasswordResetRequests
                .Where(r => r.UserId == user.Id)
                .OrderByDescending(r => r.CreatedDateTimeUtc)
                .FirstOrDefaultAsync();

            if (latestRequest != null && (DateTime.UtcNow - latestRequest.CreatedDateTimeUtc) < _minRequestInterval)
            {
                _logger.LogWarning("Prečesti zahtevi za reset lozinke za korisnika: {UserId}", user.Id);
                // Možeš vratiti specifičnu grešku ili samo izaći
                // throw new InvalidOperationException($"Molimo sačekajte {_minRequestInterval.TotalMinutes} minuta pre novog zahteva.");
                return; // Tiho izađi
            }

            // Generisanje koda (npr. 6 cifara)
            var code = RandomNumberGenerator.GetInt32(100000, 999999).ToString("D6");

            // Hashiranje koda pre čuvanja
            var hashedCode = _codeHasher.HashPassword(null!, code); // Prvi argument može biti null za jednostavan hash

            // Kreiranje zapisa u bazi
            var resetRequest = new PasswordResetRequest
            {
                UserId = user.Id,
                HashedCode = hashedCode,
                ExpiryDateTimeUtc = DateTime.UtcNow.Add(_codeValidityDuration),
                IsUsed = false,
                CreatedDateTimeUtc = DateTime.UtcNow
            };

            // Opciono: Označi prethodne zahteve kao iskorišćene/nevažeće
            var previousRequests = await _context.PasswordResetRequests
                .Where(r => r.UserId == user.Id && !r.IsUsed)
                .ToListAsync();
            foreach (var req in previousRequests)
            {
                req.IsUsed = true; // Ili ih obriši
            }

            _context.PasswordResetRequests.Add(resetRequest);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Greška pri čuvanju PasswordResetRequest za UserId: {UserId}", user.Id);
                throw new InvalidOperationException("Došlo je do greške prilikom obrade vašeg zahteva.", ex);
            }

            // Slanje emaila sa PLAIN TEXT kodom
            try
            {
                var mailRequest = new MailData
                {
                    EmailToId = user.Email,
                    EmailToName = user.UserName,
                    EmailSubject = "Vaš kod za resetovanje lozinke",
                    EmailBody = $"<h1>Resetovanje Lozinke</h1><p>Vaš kod za resetovanje lozinke je: <strong>{code}</strong></p><p>Ovaj kod ističe za {_codeValidityDuration.TotalMinutes} minuta.</p><p>Ako niste zatražili resetovanje, ignorišite ovaj email.</p>"
                };
                await _mailService.SendMailAsync(mailRequest);
                _logger.LogInformation("Kod za reset lozinke poslat na email: {Email}", email);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Greška pri slanju emaila za reset lozinke na: {Email}", email);
                // Razmisliti o rollback-u (brisanje resetRequest-a) ili logovanju za ručnu intervenciju.
                // Možda ne treba bacati grešku ka korisniku, već samo logovati.
                // throw new InvalidOperationException("Došlo je do greške prilikom slanja emaila.");
            }
        }

        public async Task<IdentityResult> ResetPasswordAsync(ResetPasswordDto dto)
        {
            var user = await _userManager.FindByEmailAsync(dto.Email);
            if (user == null)
            {
                // Ne otkrivaj da li korisnik postoji
                return IdentityResult.Failed(new IdentityError { Code = "InvalidCode", Description = "Kod za resetovanje nije važeći ili je istekao." });
            }

            // Pronađi poslednji, neiskorišćen i neistekli zahtev
            var resetRequest = await _context.PasswordResetRequests
                .Where(r => r.UserId == user.Id && !r.IsUsed && r.ExpiryDateTimeUtc > DateTime.UtcNow)
                .OrderByDescending(r => r.CreatedDateTimeUtc) // Uvek uzmi najnoviji validni
                .FirstOrDefaultAsync();

            if (resetRequest == null)
            {
                _logger.LogWarning("Nije pronađen validan PasswordResetRequest za UserId: {UserId} i Email: {Email}", user.Id, dto.Email);
                return IdentityResult.Failed(new IdentityError { Code = "InvalidCode", Description = "Kod za resetovanje nije važeći ili je istekao." });
            }

            // Verifikuj hash koda
            var verificationResult = _codeHasher.VerifyHashedPassword(null!, resetRequest.HashedCode, dto.Code);

            if (verificationResult == PasswordVerificationResult.Failed)
            {
                _logger.LogWarning("Neuspješna verifikacija koda za reset za UserId: {UserId}", user.Id);
                // Opciono: Implementiraj brojač neuspešnih pokušaja pre nego što se kod invalidira
                return IdentityResult.Failed(new IdentityError { Code = "InvalidCode", Description = "Kod za resetovanje nije važeći ili je istekao." });
            }

            // Ako je verifikacija uspela (Success ili SuccessRehashNeeded)
            // Resetuj lozinku koristeći UserManager (zahteva interni token)
            var identityToken = await _userManager.GeneratePasswordResetTokenAsync(user); // Generiši token potreban za ResetPasswordAsync
            var resetResult = await _userManager.ResetPasswordAsync(user, identityToken, dto.NewPassword);

            if (resetResult.Succeeded)
            {
                _logger.LogInformation("Lozinka uspješno resetovana za UserId: {UserId}", user.Id);
                // Označi kod kao iskorišćen
                resetRequest.IsUsed = true;
                try
                {
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateException ex)
                {
                    _logger.LogError(ex, "Greška pri označavanju PasswordResetRequest kao iskorišćenog za UserId: {UserId}", user.Id);
                    // Reset lozinke je uspeo, ali kod nije označen. Nije kritično ali treba logovati.
                }
            }
            else
            {
                _logger.LogError("Neuspješno resetovanje lozinke putem UserManager za UserId: {UserId}. Greške: {Errors}", user.Id, string.Join(", ", resetResult.Errors.Select(e => e.Description)));
            }

            return resetResult;
        }
    }
}