using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFG_Livescoring.Migrations
{
    /// <inheritdoc />
    public partial class AddFourballScores : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "TeamBScore",
                table: "MatchPlayHoleResults",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<int>(
                name: "TeamAScore",
                table: "MatchPlayHoleResults",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AddColumn<int>(
                name: "TeamAPlayer1Score",
                table: "MatchPlayHoleResults",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TeamAPlayer2Score",
                table: "MatchPlayHoleResults",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TeamBPlayer1Score",
                table: "MatchPlayHoleResults",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TeamBPlayer2Score",
                table: "MatchPlayHoleResults",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
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
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "TeamAScore",
                table: "MatchPlayHoleResults",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);
        }
    }
}
