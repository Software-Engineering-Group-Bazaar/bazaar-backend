using Users.Dtos;
using Users.Models;

namespace Users.Interfaces
{
    public interface IAuthService
    {
        Task<string> RegisterAsync(RegisterDto dto);

        Task<LoginResponseDto> LoginAsync(LoginDto dto);

        Task LogoutAsync();
    }
}
