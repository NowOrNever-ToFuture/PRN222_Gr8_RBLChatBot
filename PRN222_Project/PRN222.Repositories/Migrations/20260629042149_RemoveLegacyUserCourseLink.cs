using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PRN222.Repositories.Migrations
{
    /// <inheritdoc />
    public partial class RemoveLegacyUserCourseLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                INSERT INTO [CourseLecturers] ([Id], [CourseId], [LecturerId])
                SELECT NEWID(), [u].[CourseId], [u].[Id]
                FROM [Users] AS [u]
                WHERE [u].[Role] = 'Lecturer'
                  AND [u].[CourseId] IS NOT NULL
                  AND NOT EXISTS (
                      SELECT 1
                      FROM [CourseLecturers] AS [cl]
                      WHERE [cl].[CourseId] = [u].[CourseId]
                        AND [cl].[LecturerId] = [u].[Id]
                  );
            ");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_Courses_CourseId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_CourseId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "CourseId",
                table: "Users");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CourseId",
                table: "Users",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.Sql(@"
                ;WITH FirstAssignment AS (
                    SELECT [LecturerId], MIN([CourseId]) AS [CourseId]
                    FROM [CourseLecturers]
                    GROUP BY [LecturerId]
                )
                UPDATE [u]
                SET [u].[CourseId] = [fa].[CourseId]
                FROM [Users] AS [u]
                INNER JOIN FirstAssignment AS [fa] ON [fa].[LecturerId] = [u].[Id]
                WHERE [u].[Role] = 'Lecturer';
            ");

            migrationBuilder.CreateIndex(
                name: "IX_Users_CourseId",
                table: "Users",
                column: "CourseId");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Courses_CourseId",
                table: "Users",
                column: "CourseId",
                principalTable: "Courses",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
