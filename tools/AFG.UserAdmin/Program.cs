using AFG_Livescoring.Models;
using AFG_Livescoring.Services;
using AFG.UserAdmin;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

var parsedCommand = UserAdminCommandLine.Parse(args);

if (!parsedCommand.IsValid)
{
    Console.Error.WriteLine(parsedCommand.Error);
    UserAdminCommandLine.WriteUsage(Console.Error);
    return 2;
}

var configuration = new ConfigurationBuilder()
    .AddEnvironmentVariables()
    .Build();

var connectionString = configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine(
        "La variable ConnectionStrings__DefaultConnection est obligatoire.");
    return 2;
}

var options = new DbContextOptionsBuilder<AppDbContext>()
    .UseSqlServer(connectionString)
    .Options;

await using var db = new AppDbContext(options);
var passwordService = new AppUserPasswordService(new PasswordHasher<AppUser>());
var adminService = new UserAdminService(db, passwordService);

try
{
    if (parsedCommand.Command == UserAdminCommand.Audit)
    {
        var users = await adminService.AuditAsync();
        UserAuditWriter.Write(Console.Out, users);
        return 0;
    }

    var passwordReader = new MaskedConsolePasswordReader();
    var password = passwordReader.ReadPassword("Nouveau mot de passe : ");
    var confirmation = passwordReader.ReadPassword("Confirmez le mot de passe : ");

    if (!string.Equals(password, confirmation, StringComparison.Ordinal))
    {
        Console.Error.WriteLine("Les deux mots de passe ne correspondent pas.");
        return 2;
    }

    var result = await adminService.EnsureAdminAsync(parsedCommand.Email!, password);
    Console.WriteLine(
        result.Created
            ? $"Compte administrateur créé : ID {result.UserId}, {result.Email}"
            : $"Compte administrateur rétabli : ID {result.UserId}, {result.Email}");
    return 0;
}
catch (UserAdminException ex)
{
    Console.Error.WriteLine(ex.Message);
    return 2;
}
catch (DbUpdateException)
{
    Console.Error.WriteLine(
        "La mise à jour a échoué. Aucune information sensible n'a été affichée.");
    return 1;
}
