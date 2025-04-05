using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Facebook;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Users.Models;
using YourAppNamespace.Users.Services;

await MainAsync(args); // ➤ Pokrećemo async Main

static async Task MainAsync(string[] args)
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = FacebookDefaults.AuthenticationScheme;
        })
        .AddCookie(options =>
            {
                options.Cookie.SecurePolicy = CookieSecurePolicy.None; // allow non-HTTPS
                options.Cookie.SameSite = SameSiteMode.Lax;             // allow redirects
                options.Cookie.HttpOnly = false; // just to be safe in dev

            })
        .AddFacebook(options =>
        {
            options.ClientId = builder.Configuration["Authentication:Facebook:AppId"];
            options.ClientSecret = builder.Configuration["Authentication:Facebook:AppSecret"];
            options.CallbackPath = "/signin-facebook"; // this should match what you set on Facebook
        });


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

    // app.UseHttpsRedirection();

    app.UseAuthentication();
    app.UseAuthorization();
    
    app.MapGet("/login/facebook", async context =>
    {
        await context.ChallengeAsync(FacebookDefaults.AuthenticationScheme, new AuthenticationProperties
        {
            RedirectUri = "/swagger" // where to send the user after successful login
        });
    });

    app.MapGet("/auth/callback", async context =>
    {
        if (context.User.Identity?.IsAuthenticated ?? false)
        {
            await context.Response.WriteAsync($"Welcome, {context.User.Identity.Name}!");
        }
        else
        {
            await context.Response.WriteAsync("Login failed.");
        }
    });



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
