using System;
using AFG_Livescoring.Models;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFG_Livescoring.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260727213000_AddPasswordResetTracking")]
    public partial class AddPasswordResetTracking : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PasswordChangedAt",
                table: "AppUsers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PasswordResetRequired",
                table: "AppUsers",
                type: "bit",
                nullable: false,
                defaultValue: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PasswordChangedAt",
                table: "AppUsers");

            migrationBuilder.DropColumn(
                name: "PasswordResetRequired",
                table: "AppUsers");
        }
    }
}
