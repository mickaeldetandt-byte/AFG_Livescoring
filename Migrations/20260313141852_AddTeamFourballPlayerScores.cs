using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFG_Livescoring.Migrations
{
    /// <inheritdoc />
    public partial class AddTeamFourballPlayerScores : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Player1Strokes",
                table: "TeamScores",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Player2Strokes",
                table: "TeamScores",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
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
