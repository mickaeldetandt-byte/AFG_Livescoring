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

builder.Services.AddDbContext<AppDbContext>(options =>
{
    if (builder.Environment.IsDevelopment())
    {
        options.UseSqlite(connectionString);
    }
    else
    {
        options.UseSqlServer(connectionString);
    }
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    // 🔹 Création des comptes par défaut
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

    db.SaveChanges();

    // 🔥 NOUVEAU : génération des tokens invités pour les rounds
    var roundsWithoutToken = db.Rounds
        .Where(r => r.PublicToken == null || r.PublicToken == "")
        .ToList();

    foreach (var round in roundsWithoutToken)
    {
        round.PublicToken = Guid.NewGuid().ToString("N");
    }

    if (roundsWithoutToken.Any())
    {
        db.SaveChanges();
    }
}

// Configure pipeline
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