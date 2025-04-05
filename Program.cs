using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Users.Models;
using YourAppNamespace.Users.Services;

await MainAsync(args); // ➤ Pokrećemo async Main

static async Task MainAsync(string[] args)
{
    var builder = WebApplication.CreateBuilder(args);

    // Registracija FacebookSignInService sa HttpClient
    builder.Services.AddHttpClient<FacebookSignInService>();

    builder.Services.AddDbContext<UsersDbContext>(options =>
        options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

    builder.Services.AddIdentity<User, IdentityRole>()
        .AddEntityFrameworkStores<UsersDbContext>()
        .AddDefaultTokenProviders();

    // Add services to the container.
    builder.Services.AddControllers();

    // Swagger konfiguracija
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new OpenApiInfo { Title = "Bazaar API", Version = "v1" });
    });

    var app = builder.Build();

    // Poziv async metode za seedanje rola
    //await SeedRolesAsync(app);

    // Configure the HTTP request pipeline.
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "Bazaar API V1");
        });
    }

    app.UseHttpsRedirection();

    app.UseAuthorization();

    app.MapControllers();

    app.Run();
}

static async Task SeedRolesAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

    foreach (Role role in Enum.GetValues(typeof(Role)))
    {
        string roleName = role.ToString();
        if (!await roleManager.RoleExistsAsync(roleName))
        {
            await roleManager.CreateAsync(new IdentityRole(roleName));
        }
    }
}
