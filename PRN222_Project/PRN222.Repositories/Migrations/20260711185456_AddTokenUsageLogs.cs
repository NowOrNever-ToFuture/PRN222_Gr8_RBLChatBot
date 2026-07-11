using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PRN222.Repositories.Migrations
{
    /// <inheritdoc />
    public partial class AddTokenUsageLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TokenUsageLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PromptTokens = table.Column<int>(type: "int", nullable: false),
                    CompletionTokens = table.Column<int>(type: "int", nullable: false),
                    TotalTokens = table.Column<int>(type: "int", nullable: false),
                    ModelName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Feature = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TokenUsageLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TokenUsageLogs_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TokenUsageLogs_UserId",
                table: "TokenUsageLogs",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TokenUsageLogs");
        }
    }
}
