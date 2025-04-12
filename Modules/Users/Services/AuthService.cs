using Microsoft.AspNetCore.Identity;
using Users.Dtos;
using Users.Interfaces;
using Users.Models;

namespace Users.Services
{
    public class AuthService : IAuthService
    {
        private readonly UserManager<User> _userManager;
        private readonly SignInManager<User> _signInManager;
        private readonly IJWTService _jwtService;

        public AuthService(UserManager<User> userManager, SignInManager<User> signInManager, IJWTService jwtService)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _jwtService = jwtService;
        }

        public async Task<string> RegisterAsync(RegisterDto dto)
        {
            // Provjera da li korisnik već postoji sa istim emailom
            var userExists = await _userManager.FindByEmailAsync(dto.Email);
            if (userExists != null)
                return "Korisnik sa ovim emailom već postoji.";
            if (dto.App is null)
                return "Nema role";
            // Kreiranje novog korisnika
            var user = new User
            {
                UserName = dto.Username,
                Email = dto.Email,
                IsApproved = false  // korisnik nije odobren odmah
            };

            // Kreiranje korisnika sa passwordom
            var result = await _userManager.CreateAsync(user, dto.Password);
            if (!result.Succeeded)
                return string.Join(", ", result.Errors.Select(e => e.Description));

            // Dodavanje korisnika u defaultnu rolu (Buyer)
            await _userManager.AddToRoleAsync(user, dto.App);

            return "Registracija uspješna.";
        }

        public async Task<LoginResponseDto> LoginAsync(LoginDto dto)
        {
            // Pronaći korisnika po emailu
            var user = await _userManager.FindByEmailAsync(dto.Email);
            if (user == null || !user.IsApproved)
                return null;

            // Provjera ispravnosti lozinke
            var result = await _signInManager.CheckPasswordSignInAsync(user, dto.Password, false);
            if (!result.Succeeded)
                return null;

            // Dobijanje uloga korisnika
            var roles = await _userManager.GetRolesAsync(user);
            var (token, _) = await _jwtService.GenerateTokenAsync(user, roles);

            return new LoginResponseDto
            {
                Email = user.Email,
                Token = token
            };
        }

        public async Task LogoutAsync()
        {
            await _signInManager.SignOutAsync(); // Izloguj korisnika
        }
    }
}
