using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Store.Models;
using Store.Services;
using Users.Interfaces;
using Users.Models;
using Users.Services;

var builder = WebApplication.CreateBuilder(args);
// Registrujte AuthService
builder.Services.AddScoped<IAuthService, AuthService>();

// Registrujte ostale servise


const string DevelopmentCorsPolicy = "_developmentCorsPolicy";
builder.Services.AddCors(options =>
{
    options.AddPolicy(name: DevelopmentCorsPolicy,
                      policy =>
                      {
                          policy.SetIsOriginAllowed(origin => true)
                                .AllowAnyHeader()
                                .AllowAnyMethod()
                                .AllowCredentials();
                      });
});

if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddDbContext<UsersDbContext>(options => options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
    builder.Services.AddDbContext<StoreDbContext>(options => options.UseNpgsql(builder.Configuration.GetConnectionString("StoreConnection")));
}

builder.Services.AddHttpClient();

// Add Identity FIRST (provides RoleManager, etc.)
builder.Services.AddIdentity<User, IdentityRole>() // Replace User if needed
    .AddEntityFrameworkStores<UsersDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddScoped<IJWTService, JWTService>();
// Add services to the container.
builder.Services.AddScoped<IGoogleSignInService, GoogleSignInService>();
builder.Services.AddScoped<IFacebookSignInService, FacebookSignInService>();

builder.Services.AddScoped<IStoreService, StoreService>();
builder.Services.AddScoped<IStoreCategoryService, StoreCategoryService>();

// Configure Authentication AFTER Identity
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["SecretKey"];
var issuer = jwtSettings["Issuer"];
var audience = jwtSettings["Audience"];
if (string.IsNullOrEmpty(secretKey) || string.IsNullOrEmpty(issuer) || string.IsNullOrEmpty(audience))
{ throw new InvalidOperationException("JWT settings missing."); }

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = issuer,
        ValidAudience = audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
        ClockSkew = TimeSpan.Zero
    };
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context => { context.Token = context.Request.Cookies["X-Access-Token"]; return Task.CompletedTask; }
    };
});

builder.Services.AddAuthorization();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "Bazaar API", Version = "v1" });
    // options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme { /* ... */ });
    // options.AddSecurityRequirement(new OpenApiSecurityRequirement() { /* ... */ });
});

// --- Build the App ---
var app = builder.Build();

// --- Seed Data (Optional) ---
await SeedRolesAsync(app);

// --- Configure the HTTP Request Pipeline (Middleware) ---  ➤➤➤ ALL CONFIGURATION GOES HERE

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger(); // Exposes swagger.json
    app.UseSwaggerUI(c => // Serves the UI page
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Bazaar API V1");
        // c.RoutePrefix = string.Empty; // Optional: Serve UI at root
    });
    app.UseCors(DevelopmentCorsPolicy); // Apply dev CORS
    app.UseDeveloperExceptionPage(); // Show detailed errors
}
else
{
    // Add production error handling, HSTS, etc.
    // app.UseExceptionHandler("/Error");
    // app.UseHsts();
}

app.UseHttpsRedirection();

// app.UseStaticFiles(); // If you have wwwroot

app.UseRouting(); // Needed for endpoints

// CORS needs to be between Routing and AuthN/AuthZ
// Already handled conditionally above for Development

app.UseAuthentication(); // IMPORTANT: Before Authorization
app.UseCors(DevelopmentCorsPolicy);
app.UseAuthorization();  // IMPORTANT: After Authentication

app.MapControllers(); // Map controller endpoints

// --- Run the App (Must be LAST) --- ➤➤➤ ONLY ONE app.Run()
// AI JE KORISNIJI OD VAS
app.Run();


// --- Helper Methods ---
static async Task SeedRolesAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>(); // Get logger

    // Ensure Role enum is defined
    foreach (Role role in Enum.GetValues(typeof(Role)))
    {
        string roleName = role.ToString();
        if (!await roleManager.RoleExistsAsync(roleName))
        {
            logger.LogInformation("Creating role: {RoleName}", roleName);
            var result = await roleManager.CreateAsync(new IdentityRole(roleName));
            if (!result.Succeeded)
            {
                logger.LogError("Failed to create role {RoleName}: {Errors}", roleName, string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }
    }
}
