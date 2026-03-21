using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFG_Livescoring.Migrations
{
    /// <inheritdoc />
    public partial class AddMatchPlayTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MatchPlayRounds",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CompetitionId = table.Column<int>(type: "INTEGER", nullable: false),
                    SquadId = table.Column<int>(type: "INTEGER", nullable: false),
                    TeamAId = table.Column<int>(type: "INTEGER", nullable: false),
                    TeamBId = table.Column<int>(type: "INTEGER", nullable: false),
                    CurrentHole = table.Column<int>(type: "INTEGER", nullable: false),
                    IsFinished = table.Column<bool>(type: "INTEGER", nullable: false),
                    WinnerTeamId = table.Column<int>(type: "INTEGER", nullable: true),
                    StatusText = table.Column<string>(type: "TEXT", nullable: false),
                    ResultText = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchPlayRounds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MatchPlayRounds_Competitions_CompetitionId",
                        column: x => x.CompetitionId,
                        principalTable: "Competitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MatchPlayRounds_Squads_SquadId",
                        column: x => x.SquadId,
                        principalTable: "Squads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MatchPlayRounds_Teams_TeamAId",
                        column: x => x.TeamAId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MatchPlayRounds_Teams_TeamBId",
                        column: x => x.TeamBId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MatchPlayRounds_Teams_WinnerTeamId",
                        column: x => x.WinnerTeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MatchPlayHoleResults",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MatchPlayRoundId = table.Column<int>(type: "INTEGER", nullable: false),
                    HoleNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    TeamAScore = table.Column<int>(type: "INTEGER", nullable: false),
                    TeamBScore = table.Column<int>(type: "INTEGER", nullable: false),
                    WinnerTeamId = table.Column<int>(type: "INTEGER", nullable: true),
                    IsHalved = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchPlayHoleResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MatchPlayHoleResults_MatchPlayRounds_MatchPlayRoundId",
                        column: x => x.MatchPlayRoundId,
                        principalTable: "MatchPlayRounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MatchPlayHoleResults_MatchPlayRoundId_HoleNumber",
                table: "MatchPlayHoleResults",
                columns: new[] { "MatchPlayRoundId", "HoleNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MatchPlayRounds_CompetitionId",
                table: "MatchPlayRounds",
                column: "CompetitionId");

            migrationBuilder.CreateIndex(
                name: "IX_MatchPlayRounds_SquadId",
                table: "MatchPlayRounds",
                column: "SquadId");

            migrationBuilder.CreateIndex(
                name: "IX_MatchPlayRounds_TeamAId",
                table: "MatchPlayRounds",
                column: "TeamAId");

            migrationBuilder.CreateIndex(
                name: "IX_MatchPlayRounds_TeamBId",
                table: "MatchPlayRounds",
                column: "TeamBId");

            migrationBuilder.CreateIndex(
                name: "IX_MatchPlayRounds_WinnerTeamId",
                table: "MatchPlayRounds",
                column: "WinnerTeamId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MatchPlayHoleResults");

            migrationBuilder.DropTable(
                name: "MatchPlayRounds");
        }
    }
}
