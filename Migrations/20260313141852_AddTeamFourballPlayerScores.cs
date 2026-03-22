using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFG_Livescoring.Migrations
{
    public partial class AddTeamFourballPlayerScores : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Player1Strokes",
                table: "TeamScores",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Player2Strokes",
                table: "TeamScores",
                type: "int",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Player1Strokes",
                table: "TeamScores");

            migrationBuilder.DropColumn(
                name: "Player2Strokes",
                table: "TeamScores");
        }
    }
}