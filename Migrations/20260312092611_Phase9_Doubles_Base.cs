using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFG_Livescoring.Migrations
{
    public partial class Phase9_Doubles_Base : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'IX_Scores_RoundId'
      AND object_id = OBJECT_ID('[Scores]')
)
BEGIN
    DROP INDEX [IX_Scores_RoundId] ON [Scores];
END
");

            migrationBuilder.AddColumn<int>(
                name: "CompetitionType",
                table: "Competitions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "Teams",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompetitionId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    SquadId = table.Column<int>(type: "int", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Teams", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Teams_Competitions_CompetitionId",
                        column: x => x.CompetitionId,
                        principalTable: "Competitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Teams_Squads_SquadId",
                        column: x => x.SquadId,
                        principalTable: "Squads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "TeamPlayers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TeamId = table.Column<int>(type: "int", nullable: false),
                    PlayerId = table.Column<int>(type: "int", nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamPlayers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeamPlayers_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TeamPlayers_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TeamRounds",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompetitionId = table.Column<int>(type: "int", nullable: false),
                    TeamId = table.Column<int>(type: "int", nullable: false),
                    SquadId = table.Column<int>(type: "int", nullable: true),
                    IsLocked = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamRounds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeamRounds_Competitions_CompetitionId",
                        column: x => x.CompetitionId,
                        principalTable: "Competitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TeamRounds_Squads_SquadId",
                        column: x => x.SquadId,
                        principalTable: "Squads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_TeamRounds_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TeamScores",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TeamRoundId = table.Column<int>(type: "int", nullable: false),
                    HoleNumber = table.Column<int>(type: "int", nullable: false),
                    Strokes = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamScores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeamScores_TeamRounds_TeamRoundId",
                        column: x => x.TeamRoundId,
                        principalTable: "TeamRounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Scores_RoundId_HoleNumber",
                table: "Scores",
                columns: new[] { "RoundId", "HoleNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TeamPlayers_PlayerId",
                table: "TeamPlayers",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_TeamPlayers_TeamId_Order",
                table: "TeamPlayers",
                columns: new[] { "TeamId", "Order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TeamPlayers_TeamId_PlayerId",
                table: "TeamPlayers",
                columns: new[] { "TeamId", "PlayerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TeamRounds_CompetitionId",
                table: "TeamRounds",
                column: "CompetitionId");

            migrationBuilder.CreateIndex(
                name: "IX_TeamRounds_SquadId",
                table: "TeamRounds",
                column: "SquadId");

            migrationBuilder.CreateIndex(
                name: "IX_TeamRounds_TeamId",
                table: "TeamRounds",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_Teams_CompetitionId",
                table: "Teams",
                column: "CompetitionId");

            migrationBuilder.CreateIndex(
                name: "IX_Teams_SquadId",
                table: "Teams",
                column: "SquadId");

            migrationBuilder.CreateIndex(
                name: "IX_TeamScores_TeamRoundId_HoleNumber",
                table: "TeamScores",
                columns: new[] { "TeamRoundId", "HoleNumber" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TeamPlayers");

            migrationBuilder.DropTable(
                name: "TeamScores");

            migrationBuilder.DropTable(
                name: "TeamRounds");

            migrationBuilder.DropTable(
                name: "Teams");

            migrationBuilder.DropColumn(
                name: "CompetitionType",
                table: "Competitions");

            migrationBuilder.CreateIndex(
                name: "IX_Scores_RoundId",
                table: "Scores",
                column: "RoundId");
        }
    }
}