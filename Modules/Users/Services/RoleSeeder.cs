using Microsoft.AspNetCore.Identity;

namespace Users.Services
{


    public static class RoleSeeder
    {
        public static async Task SeedRolesAsync(RoleManager<IdentityRole> roleManager)
        {
            if (!await roleManager.RoleExistsAsync("Admin"))
            {
                await roleManager.CreateAsync(new IdentityRole("Admin"));
            }

            if (!await roleManager.RoleExistsAsync("Buyer"))
            {
                await roleManager.CreateAsync(new IdentityRole("Buyer"));
            }

            if (!await roleManager.RoleExistsAsync("Seller"))
            {
                await roleManager.CreateAsync(new IdentityRole("Seller"));
            }
        }
    }
}