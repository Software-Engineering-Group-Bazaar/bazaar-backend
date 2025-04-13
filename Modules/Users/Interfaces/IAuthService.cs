using System.Security.Claims;
using Users.Dtos;
using Users.Models;

namespace Users.Interfaces
{
    public interface IAuthService
    {
        Task<string> RegisterAsync(RegisterDto dto);

        Task<LoginResponseDto> LoginAsync(LoginDto dto);

        Task LogoutAsync(System.Security.Claims.ClaimsPrincipal user);
        Task<DateTime?> GetLastLogout(ClaimsPrincipal user);
    }
}
