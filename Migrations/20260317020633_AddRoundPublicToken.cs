using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFG_Livescoring.Migrations
{
    public partial class AddRoundPublicToken : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PublicToken",
                table: "Rounds",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PublicToken",
                table: "Rounds");
        }
    }
}