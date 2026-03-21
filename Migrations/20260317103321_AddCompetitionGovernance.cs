using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFG_Livescoring.Migrations
{
    /// <inheritdoc />
    public partial class AddCompetitionGovernance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Competitions_AppUsers_CreatedByUserId",
                table: "Competitions");

            migrationBuilder.DropForeignKey(
                name: "FK_Competitions_Clubs_ClubId",
                table: "Competitions");

            migrationBuilder.AddForeignKey(
                name: "FK_Competitions_AppUsers_CreatedByUserId",
                table: "Competitions",
                column: "CreatedByUserId",
                principalTable: "AppUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Competitions_Clubs_ClubId",
                table: "Competitions",
                column: "ClubId",
                principalTable: "Clubs",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
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
    }
}
