using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PRN222.Repositories.Migrations
{
    /// <inheritdoc />
    public partial class AddCourseIdToConversation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CourseId",
                table: "Conversations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_CourseId",
                table: "Conversations",
                column: "CourseId");

            migrationBuilder.AddForeignKey(
                name: "FK_Conversations_Courses_CourseId",
                table: "Conversations",
                column: "CourseId",
                principalTable: "Courses",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Conversations_Courses_CourseId",
                table: "Conversations");

            migrationBuilder.DropIndex(
                name: "IX_Conversations_CourseId",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "CourseId",
                table: "Conversations");
        }
    }
}
