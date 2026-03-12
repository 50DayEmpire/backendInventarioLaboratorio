using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using BackendInventario.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;


var builder = WebApplication.CreateBuilder(args);

// 1. Configurar DbContext con SQL Server
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// 2. Configurar Identity
builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 8;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireNonAlphanumeric = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();


// 3. Configurar JWT Authentication
var key = Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!);
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
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(key)
    };
});

//Configuracion de CORS para permitir solicitudes desde el frontend
var frontendUrl = builder.Configuration["Frontend:Url"]!;
builder.Services.AddCors(options =>
{
    options.AddPolicy("CorsPolicyJWT", policy =>
    {
        policy.WithOrigins(frontendUrl) // URL del frontend
              .AllowAnyMethod()
              .AllowAnyHeader(); // Esto permite la cabecera 'Authorization'
    });
});

//Mail dummy para pruebas (no envía correos reales)
builder.Services.AddSingleton<IEmailSender<IdentityUser>, DummyEmailSender>();

builder.Services.AddAuthorization();

// 4. Controllers
builder.Services.AddControllers();

var app = builder.Build();

// 5. Seed Roles
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    await SeedRoles(services);
}

// Middleware
app.UseHttpsRedirection();
app.UseRouting();

app.UseCors("CorsPolicyJWT");

app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();



//Seeding de roles y asignación de rol Admin a un usuario específico

static async Task SeedRoles(IServiceProvider serviceProvider)
{
    var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();

    string[] roleNames = { "Admin", "User", "Editor" };

    foreach (var roleName in roleNames)
    {
        var roleExist = await roleManager.RoleExistsAsync(roleName);
        if (!roleExist)
        {
            await roleManager.CreateAsync(new IdentityRole(roleName));
        }
    }
    
    var userManager = serviceProvider.GetRequiredService<UserManager<IdentityUser>>();
    var user = await userManager.FindByEmailAsync("juanitocasillas101@gmail.com");

    if (user != null)
    {
        await userManager.AddToRoleAsync(user, "Admin");
    }
}