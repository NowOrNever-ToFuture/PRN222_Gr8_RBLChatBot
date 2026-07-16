using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PRN222.Repositories.Migrations
{
    /// <inheritdoc />
    public partial class AddMultiLlmBenchmark : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BenchmarkBatchId",
                table: "BenchmarkRuns",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_BenchmarkRuns_BenchmarkBatchId",
                table: "BenchmarkRuns",
                column: "BenchmarkBatchId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BenchmarkRuns_BenchmarkBatchId",
                table: "BenchmarkRuns");

            migrationBuilder.DropColumn(
                name: "BenchmarkBatchId",
                table: "BenchmarkRuns");
        }
    }
}
