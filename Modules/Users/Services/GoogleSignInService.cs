using Users.Interfaces;
using Google.Apis.Auth;
using Microsoft.AspNetCore.Identity;
using System.Net;
using Users.Models.Dtos;
using Users.Models;

namespace Users.Services
{
    public class GoogleSignInService : IGoogleSignInService
    {
        private readonly UserManager<User> _userManager;
        private readonly SignInManager<User> _signInManager;
        private readonly IJWTService _jwtService;
        private readonly string[] _validClientIds;

        public GoogleSignInService(IConfiguration configuration, UserManager<User> userManager, SignInManager<User> signInManager, IJWTService jwtService)
        {
            _validClientIds = new[]
            {
                configuration["GoogleAuth:AndroidClientId"]!,
                configuration["GoogleAuth:iOSClientId"]!,
                configuration["GoogleAuth:webClientId"]!,
                configuration["GoogleAuth:AndroidClientSellerId"]!,
                configuration["GoogleAuth:iOSClientSellerId"]!,
                configuration["GoogleAuth:webClientSellerId"]!
            };
            _userManager = userManager;
            _signInManager = signInManager;
            _jwtService = jwtService;
        }

        public async Task<string> SignInAsync(GoogleSignInRequestDto request)
        {
            //validate token
            GoogleUserInfoDto? googleUser = null;
            try
            {
                var payload = await GoogleJsonWebSignature.ValidateAsync(
                    request.IdToken,
                    new GoogleJsonWebSignature.ValidationSettings
                    {
                        Audience = _validClientIds
                    });

                googleUser = new GoogleUserInfoDto
                {
                    Email = payload.Email,
                    Name = payload.Name,
                    Picture = payload.Picture,
                    Subject = payload.Subject
                };
            }
            catch
            {
                return null;
            }

            if (googleUser != null)
            {
                //check if user exists
                var user = await _userManager.FindByEmailAsync(googleUser.Email);
                if (user != null)
                {
                    var roles = await _userManager.GetRolesAsync(user);
                    var (token, _) = await _jwtService.GenerateTokenAsync(user, roles);
                    return token;
                }
                else
                {
                    user = new User
                    {
                        Email = googleUser.Email,
                        UserName = googleUser.Email,
                        EmailConfirmed = true,
                        IsApproved = false
                    };

                    var result = await _userManager.CreateAsync(user);
                    if (!result.Succeeded)
                    {
                        return null;
                    }

                    if(request.App.Equals("buyer"))
                    {
                        await _userManager.AddToRoleAsync(user, Role.Buyer.ToString());
                    } 
                    else
                    {
                        await _userManager.AddToRoleAsync(user, Role.Seller.ToString());
                    }
                        var roles = await _userManager.GetRolesAsync(user);
                    var (token, _) = await _jwtService.GenerateTokenAsync(user, roles);

                    return token;
                }
            }
           
            return null;

        }
    }
}
