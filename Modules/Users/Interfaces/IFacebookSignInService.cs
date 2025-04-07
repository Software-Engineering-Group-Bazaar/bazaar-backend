using Users.Dtos;
using Users.Models.Dtos;

namespace Users.Interfaces
{
    public interface IFacebookSignInService
    {
        Task<LoginResponseDto?> SignInAsync(string accessToken, string app);
    }
}
