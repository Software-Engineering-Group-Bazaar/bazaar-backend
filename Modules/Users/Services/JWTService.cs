// Services/JWTService.cs
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;   
using System.Security.Claims;            
using System.Text;                       
using System.Threading.Tasks;            
using Microsoft.Extensions.Configuration; 
using Microsoft.IdentityModel.Tokens;     
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Users.Interfaces;                
using Users.Models;                   

namespace Users.Services 
{

    public class JWTService : IJWTService
    {
        private readonly IConfiguration _configuration; 

        // Constructor Injection: Dobijamo IConfiguration od .NET DI sistema
        public JWTService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        // Implementacija metode iz interfejsa
        public async Task<string> GenerateTokenAsync(User user, IList<string> roles)
        {
            // 1. Dobavi JWT postavke iz konfiguracije (appsettings.json)
            var jwtSettings = _configuration.GetSection("JwtSettings");
            var secretKey = jwtSettings["SecretKey"];
            var issuer = jwtSettings["Issuer"];
            var audience = jwtSettings["Audience"];
            // Pokušaj da pročitaš vrijeme isteka, ako ne uspije, koristi default (npr. 60 min)
            if (!int.TryParse(jwtSettings["ExpiryMinutes"], out int expiryMinutes))
            {
                expiryMinutes = 60; // Default
            }


            // Provjera da li su sve postavke učitane
            if (string.IsNullOrEmpty(secretKey) || string.IsNullOrEmpty(issuer) || string.IsNullOrEmpty(audience))
            {
                // U realnoj aplikaciji, ovdje bi trebalo logovati grešku
                throw new InvalidOperationException("JWT settings (SecretKey, Issuer, Audience) are not configured properly in appsettings.");
            }

            // 2. Kreiraj listu "Claim"-ova (tvrdnji) koje želimo u tokenu
            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id),             // Subject = ID korisnika (iz IdentityUser)
                new Claim(JwtRegisteredClaimNames.Email, user.Email),           // Email korisnika
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()) // Jedinstveni ID samog tokena (za npr. opoziv)
                // Opciono: new Claim(ClaimTypes.NameIdentifier, user.UserName) // Korisničko ime
            };

            // 3. Dodaj role korisnika kao zasebne claim-ove
            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            // 4. Kreiraj sigurnosni ključ od naše tajne fraze
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));

            // 5. Kreiraj kredencijale za potpisivanje koristeći ključ i algoritam
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            // 6. Odredi vrijeme isteka tokena
            var expiry = DateTime.UtcNow.AddMinutes(expiryMinutes); 

            // 7. Kreiraj JWT token objekat sa svim definisanim podacima
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims), 
                Expires = expiry,                     
                Issuer = issuer,                     
                Audience = audience,                
                SigningCredentials = creds           
            };

            // 8. Kreiraj handler i ispiši token kao string
            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.CreateToken(tokenDescriptor);
            var tokenString = tokenHandler.WriteToken(token);

            // 9. Vrati generisani token string
            // Koristimo Task.FromResult jer samo generisanje nije asinhrono,
            // ali vraćamo Task<string> zbog interfejsa.
            return await Task.FromResult(tokenString);
        }
    }
}