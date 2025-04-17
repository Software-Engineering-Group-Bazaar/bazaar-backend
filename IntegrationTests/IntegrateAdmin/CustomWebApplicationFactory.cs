

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.IO;
using System.Linq;
using Catalog.Models; // Dodaj namespace tvog DbContext-a

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((context, config) =>
        {
            config.AddJsonFile("appsettings.Testing.json", optional: false, reloadOnChange: true);
        });

        builder.ConfigureServices(services =>
        {
            // Ukloni postojeći DbContext ako postoji
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<CatalogDbContext>));
            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            // Dodaj in-memory DbContext za testiranje
            services.AddDbContext<CatalogDbContext>(options =>
            {
                options.UseInMemoryDatabase("TestCatalogDb");
            });

            // Dodaj i ostale ako ih koristiš (npr. UsersDbContext, StoreDbContext) po istom principu
        });
    }
}



