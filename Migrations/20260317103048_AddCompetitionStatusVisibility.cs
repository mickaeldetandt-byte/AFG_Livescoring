using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFG_Livescoring.Migrations
{
    /// <inheritdoc />
    public partial class AddCompetitionStatusVisibility : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ClubId",
                table: "Competitions",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CreatedByUserId",
                table: "Competitions",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "Competitions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Visibility",
                table: "Competitions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Competitions_ClubId",
                table: "Competitions",
                column: "ClubId");

            migrationBuilder.CreateIndex(
                name: "IX_Competitions_CreatedByUserId",
                table: "Competitions",
                column: "CreatedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Competitions_AppUsers_CreatedByUserId",
                table: "Competitions",
                column: "CreatedByUserId",
                principalTable: "AppUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Competitions_Clubs_ClubId",
                table: "Competitions",
                column: "ClubId",
                principalTable: "Clubs",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Competitions_AppUsers_CreatedByUserId",
                table: "Competitions");

            migrationBuilder.DropForeignKey(
                name: "FK_Competitions_Clubs_ClubId",
                table: "Competitions");

            migrationBuilder.DropIndex(
                name: "IX_Competitions_ClubId",
                table: "Competitions");

            migrationBuilder.DropIndex(
                name: "IX_Competitions_CreatedByUserId",
                table: "Competitions");

            migrationBuilder.DropColumn(
                name: "ClubId",
                table: "Competitions");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "Competitions");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Competitions");

            migrationBuilder.DropColumn(
                name: "Visibility",
                table: "Competitions");
        }
    }
}
