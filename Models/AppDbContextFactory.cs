using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;

namespace AFG_Livescoring.Models
{
    public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";

            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
                .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: false)
                .AddEnvironmentVariables()
                .Build();

            var cs = configuration.GetConnectionString("DefaultConnection");

            if (string.IsNullOrWhiteSpace(cs))
            {
                throw new InvalidOperationException("La chaîne de connexion 'DefaultConnection' est introuvable.");
            }

            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();

            // SQL Server partout : local + Azure
            optionsBuilder.UseSqlServer(cs);

            return new AppDbContext(optionsBuilder.Options);
        }
    }
}