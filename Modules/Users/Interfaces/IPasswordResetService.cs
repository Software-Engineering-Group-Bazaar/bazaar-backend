using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Users.Models.Dtos;

namespace Users.Interfaces
{
    public interface IPasswordResetService
    {
        /// <summary>
        /// Pokreće proces resetovanja lozinke za dati email.
        /// Generiše kod, čuva ga (hashiranog) i šalje email korisniku.
        /// </summary>
        /// <param name="email">Email adresa korisnika.</param>
        /// <returns>Task koji predstavlja asinhronu operaciju.</returns>
        /// <exception cref="NotFoundException">Ako korisnik nije pronađen (opciono, može i da ne baca grešku).</exception>
        /// <exception cref="InvalidOperationException">Ako slanje emaila ne uspe.</exception>
        Task RequestPasswordResetAsync(string email);

        /// <summary>
        /// Verifikuje kod za resetovanje i postavlja novu lozinku.
        /// </summary>
        /// <param name="dto">Podaci potrebni za reset (email, kod, nova lozinka).</param>
        /// <returns>IdentityResult koji ukazuje na uspeh ili neuspeh operacije.</returns>
        Task<IdentityResult> ResetPasswordAsync(ResetPasswordDto dto);
    }
}