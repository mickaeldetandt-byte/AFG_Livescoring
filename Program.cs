using Microsoft.EntityFrameworkCore;
using AFG_Livescoring.Models;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/Login";
    });

builder.Services.AddRazorPages();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("La chaîne de connexion 'DefaultConnection' est introuvable.");
}

// SQL Server partout : local + Azure
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    try
    {
        Console.WriteLine("AFG STARTUP TEST V1.0.3");

        db.Database.Migrate();

        if (!db.AppUsers.Any(u => u.Email == "admin@afg.local"))
        {
            db.AppUsers.Add(new AppUser
            {
                Email = "admin@afg.local",
                PasswordHash = "admin123",
                Role = "Admin",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
        }

        if (!db.AppUsers.Any(u => u.Email == "club@afg.local"))
        {
            db.AppUsers.Add(new AppUser
            {
                Email = "club@afg.local",
                PasswordHash = "club123",
                Role = "Club",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
        }

        var roundsWithoutToken = db.Rounds
            .Where(r => string.IsNullOrEmpty(r.PublicToken))
            .ToList();

        foreach (var round in roundsWithoutToken)
        {
            round.PublicToken = Guid.NewGuid().ToString("N");
        }

        db.SaveChanges();
    }
    catch (Exception ex)
    {
        Console.WriteLine("Erreur au démarrage de l'application : " + ex);
        throw;
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();

app.Run();