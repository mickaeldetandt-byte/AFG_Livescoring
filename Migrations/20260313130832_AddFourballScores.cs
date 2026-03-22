using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFG_Livescoring.Migrations
{
    public partial class AddFourballScores : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "TeamBScore",
                table: "MatchPlayHoleResults",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<int>(
                name: "TeamAScore",
                table: "MatchPlayHoleResults",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<int>(
                name: "TeamAPlayer1Score",
                table: "MatchPlayHoleResults",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TeamAPlayer2Score",
                table: "MatchPlayHoleResults",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TeamBPlayer1Score",
                table: "MatchPlayHoleResults",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TeamBPlayer2Score",
                table: "MatchPlayHoleResults",
                type: "int",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TeamAPlayer1Score",
                table: "MatchPlayHoleResults");

            migrationBuilder.DropColumn(
                name: "TeamAPlayer2Score",
                table: "MatchPlayHoleResults");

            migrationBuilder.DropColumn(
                name: "TeamBPlayer1Score",
                table: "MatchPlayHoleResults");

            migrationBuilder.DropColumn(
                name: "TeamBPlayer2Score",
                table: "MatchPlayHoleResults");

            migrationBuilder.AlterColumn<int>(
                name: "TeamBScore",
                table: "MatchPlayHoleResults",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "TeamAScore",
                table: "MatchPlayHoleResults",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);
        }
    }
}