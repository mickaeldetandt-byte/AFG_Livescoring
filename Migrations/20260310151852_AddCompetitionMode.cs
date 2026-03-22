using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFG_Livescoring.Migrations
{
    public partial class AddCompetitionMode : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Mode",
                table: "Competitions",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "StrokePlay");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Mode",
                table: "Competitions");
        }
    }
}