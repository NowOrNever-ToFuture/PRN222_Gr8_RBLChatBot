using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PRN222.Repositories.Migrations
{
    /// <inheritdoc />
    public partial class AddManagedByIdToCourse : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ManagedById",
                table: "Courses",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Courses_ManagedById",
                table: "Courses",
                column: "ManagedById");

            migrationBuilder.AddForeignKey(
                name: "FK_Courses_Users_ManagedById",
                table: "Courses",
                column: "ManagedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Courses_Users_ManagedById",
                table: "Courses");

            migrationBuilder.DropIndex(
                name: "IX_Courses_ManagedById",
                table: "Courses");

            migrationBuilder.DropColumn(
                name: "ManagedById",
                table: "Courses");
        }
    }
}
