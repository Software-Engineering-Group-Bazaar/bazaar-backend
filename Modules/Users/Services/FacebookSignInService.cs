using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Users.Dtos;
using Users.Interfaces;
using Users.Models;
using Users.Models.Dtos;

namespace Users.Services
{
    public class FacebookSignInService : IFacebookSignInService
    {
        private readonly HttpClient _httpClient;
        private readonly UserManager<User> _userManager;
        private readonly IJWTService _jwtService;

        public FacebookSignInService(
            HttpClient httpClient,
            UserManager<User> userManager,
            IJWTService jwtService)
        {
            _httpClient = httpClient;
            _userManager = userManager;
            _jwtService = jwtService;
        }

        public async Task<LoginResponseDto?> SignInAsync(string accessToken, string app)
        {
            var userInfo = await ValidateFacebookTokenAsync(accessToken);

            if (userInfo == null || string.IsNullOrEmpty(userInfo.Email))
                return null;

            var user = await _userManager.FindByEmailAsync(userInfo.Email);

            if (user == null)
            {
                user = new User
                {
                    Email = userInfo.Email,
                    UserName = userInfo.Email,
                    EmailConfirmed = true,
                    IsApproved = false
                };

                var result = await _userManager.CreateAsync(user);
                if (!result.Succeeded)
                    return null;

                if (app == "buyer")
                    await _userManager.AddToRoleAsync(user, Role.Buyer.ToString());
                else
                    await _userManager.AddToRoleAsync(user, Role.Seller.ToString());
            }

            var roles = await _userManager.GetRolesAsync(user);
            var (token, _) = await _jwtService.GenerateTokenAsync(user, roles);

            return new LoginResponseDto
            {
                Email = user.Email,
                Token = token
            };
        }

        private async Task<FacebookUserInfo?> ValidateFacebookTokenAsync(string accessToken)
        {
            var url = $"https://graph.facebook.com/me?fields=id,name,email&access_token={accessToken}";

            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
                return null;

            var content = await response.Content.ReadAsStringAsync();

            var userInfo = JsonSerializer.Deserialize<FacebookUserInfo>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return userInfo;
        }
    }

    public class FacebookUserInfo
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? Email { get; set; }
    }
}