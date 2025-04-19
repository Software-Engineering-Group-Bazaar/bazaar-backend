using Users.Models.Dtos;

namespace Users.Interfaces
{
    public interface IGoogleSignInService
    {
        Task<string?> SignInAsync(GoogleSignInRequestDto request);
    }
}
